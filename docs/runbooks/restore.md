# Runbook: backup & restore

Two things cannot be rebuilt from the repository: the PostgreSQL database and the MinIO media bucket.
Everything else — images, configuration, schema — comes from git and the registry.

## Backups

`scripts/backup.sh` writes a timestamped directory containing:

| File | What it is |
| --- | --- |
| `postgres.dump` | `pg_dump -Fc` of the application database — compressed, selectively restorable |
| `media/` | mirror of the MinIO media bucket |
| `manifest.txt` | when it was taken, which database/bucket, image tag, git commit |

```bash
cd /opt/pcmarket
BACKUP_DIR=/var/backups/pcmarket ./scripts/backup.sh
```

Nightly, with a log:

```
0 3 * * *  cd /opt/pcmarket && BACKUP_DIR=/var/backups/pcmarket ./scripts/backup.sh >> /var/log/pcmarket-backup.log 2>&1
```

The script exits non-zero on any failure — an empty dump or a media mirror that did not reach the host
is treated as a failed backup rather than kept — so a cron wrapper can alert on it.

`BACKUP_RETENTION_DAYS` (default 14) prunes older runs. It only removes directories matching the
timestamp pattern, so anything else in `BACKUP_DIR` is left alone.

### Get them off the box

A backup on the same disk as the database does not survive the failure you are most worried about. Sync
the directory somewhere else — another host, object storage, whatever you already trust:

```
30 3 * * *  rsync -a --delete /var/backups/pcmarket/ backup-host:/srv/pcmarket-backups/
```

### Check them

An untested backup is a guess. Once a quarter, restore the latest one into a scratch stack (a second
compose project on a dev box, `docker compose -p pcmarket-drill`) and confirm the storefront lists
products and an order opens. That also keeps the operator familiar with the procedure below.

## Restore

**This is destructive**: it drops the application database and overwrites the media bucket. Everything
written since the backup is lost. `scripts/restore.sh` requires you to type the database name to confirm.

```bash
cd /opt/pcmarket
./scripts/restore.sh /var/backups/pcmarket/20260726T031500Z
```

It stops `api`, `web`, `admin`, and `nginx` (they hold connections that would block `DROP DATABASE`),
terminates leftover backends, recreates the database, `pg_restore`s the dump, mirrors the media back and
re-applies the bucket's anonymous download policy, then brings the stack up again.

Afterwards:

- `curl -fsS https://$API_HOST/health` — all three dependencies `Healthy`.
- Storefront lists products; an order opens with its status history.
- `docker compose logs --tail 100 api` — no repeated exceptions.

### Restoring onto an older schema

The dump carries the schema it was taken with. If the running images are **newer** than the backup, run
the gated schema step after restoring so migrations are applied on top:

```bash
docker compose run --rm migrate
```

If the running images are **older** than the backup, deploy the matching image tag first —
`manifest.txt` records the tag and commit the backup was taken at — and only then restore.

## Partial recovery

Full restore is a blunt instrument. For a single table or a handful of rows, restore the dump into a
throwaway database and copy across, rather than rolling the whole store back:

```bash
docker compose exec -T postgres createdb -U pcmarket pcmarket_scratch
docker compose exec -T postgres pg_restore -U pcmarket -d pcmarket_scratch --no-owner < postgres.dump
docker compose exec -it postgres psql -U pcmarket -d pcmarket_scratch
# inspect, then copy what you need across with INSERT ... SELECT via dblink or a manual export
docker compose exec -T postgres dropdb -U pcmarket pcmarket_scratch
```

## What is not backed up

- **Redis** — only caches, guest-cart *tokens* (the carts themselves live in PostgreSQL), and bot
  conversation state. Losing it costs a cold cache and any half-finished bot checkout.
- **Hangfire job state** — stored in PostgreSQL, so it comes back with the dump. Recurring jobs
  re-register at startup regardless.
- **`.env`** — deliberately not in git and not in the backup. Keep it in a password manager; losing
  `JWT_SIGNING_KEY` signs every user out, and losing the Postgres/MinIO credentials locks you out of
  your own data.
