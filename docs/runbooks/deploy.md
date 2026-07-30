# Runbook: deploy

How a build reaches the VPS, and how to roll it back. Assumes the [first-time setup](#first-time-vps-setup)
below has been done once.

## Normal deploy

1. **CI builds and publishes.** Merging to `master` runs `.github/workflows/ci.yml`: it builds and tests
   the server projects, builds the Android head, then pushes `api`, `web`, and `admin` images to GHCR
   tagged `latest` and `sha-<commit>`. Nothing is deployed automatically.
2. **Pick the tag.** Prefer `sha-<commit>` over `latest` — it names exactly one build, which matters when
   you later need to know what is running or roll back to it.
3. **Run the Deploy workflow** (Actions → Deploy → Run workflow), entering the tag and leaving
   *Apply database migrations* checked. It will, over SSH:
   - `docker compose pull` the new images,
   - `docker compose run --rm migrate` — the **gated schema step**, which applies migrations with the
     same image the app will run and exits; a failure here stops the deploy before any container is
     replaced,
   - `docker compose up -d --no-deps api web admin nginx` to roll the app containers,
   - poll `/health` until it passes, dumping API logs and failing if it never does.

To deploy by hand instead:

```bash
cd /opt/pcmarket
export IMAGE_TAG=sha-<commit>
docker compose pull
docker compose run --rm migrate          # gated schema step — must succeed first
docker compose up -d --no-deps api web admin nginx
docker compose exec api curl -fsS http://localhost:8080/health
```

### Why migrations are a separate step

`Database__MigrateOnStartup` should be `false` on the VPS. With it on, every API instance tries to
migrate as it boots — harmless with one container, a race as soon as there are two, and it couples a
schema change to a container restart so a bad migration takes the app down with it. Running `migrate`
first makes the schema change its own reversible decision.

Write migrations **expand/contract** (add the new column, backfill, deploy code that uses it, drop the
old one in a later release) so the previous image still runs against the new schema — that is what makes
the rollback below safe.

## Rollback

```bash
cd /opt/pcmarket
export IMAGE_TAG=sha-<previous-commit>
docker compose pull
docker compose up -d --no-deps api web admin nginx
```

Do **not** re-run `migrate` when rolling back. If the bad release included a migration that the previous
image cannot tolerate, you are restoring, not rolling back — see [restore.md](restore.md).

## First-time VPS setup

1. Install Docker Engine and the compose plugin.
2. Create `/opt/pcmarket`, copy in `docker-compose.yml`, the `nginx/` directory, and `scripts/`.
3. Copy `.env.example` to `.env` and set, at minimum:
   - `JWT_SIGNING_KEY` — `openssl rand -base64 48`. Compose refuses to start without it.
   - `POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD` — not the defaults.
   - `STOREFRONT_HOST`, `ADMIN_HOST`, `API_HOST` — the real domains.
   - `HTTP_PORT=80`, `HTTPS_PORT=443`.
   - `DB_MIGRATE_ON_STARTUP=false`, `DB_SEED_DEMO_CATALOG=false`.
   - `IMAGE_REGISTRY=ghcr.io/<owner>/<repo>`, `IMAGE_TAG=<tag>`.
4. Remove the published `ports:` from `postgres`, `redis`, and `minio` in `docker-compose.yml`. They are
   there for local development; on a public host only nginx should be reachable.
5. Point the three DNS A records at the VPS.
6. `docker compose up -d` and confirm `curl -H "Host: $API_HOST" http://localhost/health`.
7. Issue certificates and enable TLS (below).
8. Change the seeded admin password — `SEED_ADMIN_PHONE` / `SEED_ADMIN_PASSWORD` default to a known
   value, which is fine for development and unacceptable in production.
9. Add the backup cron job (see [restore.md](restore.md)).

## Enabling TLS

Certificates must exist before nginx is told to load them, so issue them over plain HTTP first — the
port-80 vhosts already serve `/.well-known/acme-challenge/` from a volume shared with certbot.

```bash
cd /opt/pcmarket
for host in "$STOREFRONT_HOST" "$ADMIN_HOST" "$API_HOST"; do
  docker compose run --rm certbot certonly --webroot -w /var/www/certbot \
    -d "$host" --agree-tos -m ops@example.com --non-interactive
done

cp nginx/tls.conf.example nginx/templates/tls.conf.template
# In nginx/templates/default.conf.template, replace each `location / { proxy_pass ... }` with
#   return 301 https://$host$request_uri;
# keeping the /.well-known/acme-challenge/ location so renewals keep working.

docker compose restart nginx
```

Renewal (certbot only rewrites certificates within 30 days of expiry, so this is safe to run daily):

```
0 4 * * *  cd /opt/pcmarket && docker compose run --rm certbot renew --webroot -w /var/www/certbot --quiet && docker compose exec nginx nginx -s reload
```

## Verifying a deploy

- `curl -fsS https://$API_HOST/health` — expect `postgres`, `redis`, and `minio` all `Healthy`.
- Storefront loads and shows products; a Blazor page reacts to a click (proves the WebSocket circuit
  survived the proxy — a misconfigured `Upgrade` header shows up as a page that renders but does nothing).
- Admin panel logs in and the dashboard's new-order feed reads "connected".
- `docker compose logs --tail 100 api` — no repeated exceptions.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| nginx exits at start, `host not found in upstream` | An app container is not running. nginx resolves upstreams at startup; `docker compose up -d` all services. |
| Pages render but nothing is interactive | The WebSocket upgrade is not reaching Kestrel. Check `proxy_params.conf` is mounted and `$connection_upgrade` is mapped in `nginx.conf`. |
| API exits immediately, Hangfire/storage error | Postgres unreachable. Hangfire builds its storage during startup and throws if it cannot connect. |
| `JWT_SIGNING_KEY` error from compose | It is unset in `.env`; compose is deliberately strict about this rather than booting with a dev key. |
| Media 403 through `/media/` | The bucket lost its anonymous download policy — re-run `docker compose up -d minio-init`. |
| 444 on every request | The `Host` header does not match any configured vhost. Check `STOREFRONT_HOST`/`ADMIN_HOST`/`API_HOST`. |
