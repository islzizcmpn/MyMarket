# PcMarket

A C#/.NET clone of an Uzbek PC & electronics store ([pcmarket.uz](https://pcmarket.uz/)),
delivered as three clients over one backend:

- **Web storefront** — Blazor Web App
- **Mobile app** — .NET MAUI (Android + iOS)
- **Telegram bot** — in-process webhook

with an **admin panel** and Uzbekistan payment rails (Click, Payme, Uzcard/Humo, cash on delivery).

The system is a **modular monolith**: one ASP.NET Core backend exposing a versioned REST API
that every client consumes through a shared contracts library. See the design and build plan in
[docs/specs/pcmarket_clone/](docs/specs/pcmarket_clone/) — [architecture.md](docs/specs/pcmarket_clone/architecture.md)
is the source of truth, and [plan.md](docs/specs/pcmarket_clone/plan.md) tracks progress phase by phase.

> **Status:** all nine build phases complete — backend, storefront, admin panel, Telegram bot, mobile
> app, and the containerized deployment. `docker compose up -d --build` brings up the whole system
> behind Nginx. Known gaps are recorded in [plan.md](docs/specs/pcmarket_clone/plan.md): no live
> payment-gateway or BotFather credentials, and the mobile app's FCM client SDK is stubbed pending an
> AndroidX/.NET 10 compatibility fix.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0.x** | `dotnet --version` should print `10.x` |
| [Docker](https://www.docker.com/) + Compose | any recent | Runs the whole stack, or just PostgreSQL/Redis/MinIO for development |
| .NET MAUI workloads | — | Only for the mobile app: `dotnet workload install maui` |

No local PostgreSQL/Redis/MinIO install is needed — Docker Compose provides them. To run the system
without the .NET SDK at all, use the [full-stack quickstart](#quick-start--the-whole-stack) below.

---

## Solution layout

```
PcMarket.slnx
├─ src/
│  ├─ PcMarket.Domain          # Entities, enums, domain events (no external deps)
│  ├─ PcMarket.Contracts       # DTOs shared by every client
│  ├─ PcMarket.Application      # Use-cases + abstractions (cache, storage, tokens, …)
│  ├─ PcMarket.Infrastructure   # EF Core + PostgreSQL, Identity, Redis, MinIO, seeding
│  ├─ PcMarket.Payments         # Payment providers (Click/Payme/Uzcard/Humo/Cash)
│  ├─ PcMarket.Api              # ASP.NET Core host: REST API, auth, Hangfire, health
│  ├─ PcMarket.Web              # Blazor storefront
│  ├─ PcMarket.Admin            # Blazor admin panel
│  ├─ PcMarket.Bot              # Telegram bot handlers
│  └─ PcMarket.Mobile           # .NET MAUI app (Android + iOS)
├─ shared/
│  ├─ PcMarket.ApiClient        # Typed HTTP client (used by Mobile & Bot)
│  └─ PcMarket.Mobile.Core      # Mobile session/token logic, platform-neutral so it is testable
└─ tests/                       # Unit + Testcontainers integration tests
```

---

## Quick start — the whole stack

Runs storefront, admin panel, API, PostgreSQL, Redis, MinIO, and Nginx as containers. Nothing but
Docker is required.

```bash
cp .env.example .env          # defaults work as-is for local use
docker compose up -d --build  # first build takes a few minutes
```

| URL | What |
|-----|------|
| <http://localhost:8080> | Storefront |
| <http://admin.localhost:8080> | Admin panel |
| <http://api.localhost:8080/health> | API health (postgres / redis / minio) |
| <http://api.localhost:8080/media/…> | Media, served straight from MinIO |

Nginx routes by hostname; Chrome and Firefox resolve `*.localhost` to 127.0.0.1 with no hosts-file
edit. Only Nginx is published — the app containers and data services stay on the internal network.
A request with an unrecognised `Host` is closed (444) rather than served by whichever vhost is first.

The stack seeds a demo catalog and an admin user on first start (`DB_SEED_DEMO_CATALOG=true`), so the
storefront has something to show immediately.

```bash
docker compose ps                       # health of every service
docker compose logs -f api              # follow the API
docker compose down                     # stop (volumes survive)
docker compose down -v                  # stop and delete all data
```

> **Ports in use?** `HTTP_PORT`, `POSTGRES_PORT`, `REDIS_PORT`, and `MINIO_PORT` are all `.env` settings.

For deploying this to a server — TLS, gated migrations, rollback — see
[docs/runbooks/deploy.md](docs/runbooks/deploy.md).

---

## Quick start — local development

For working on the code, run the backing services in Docker and the apps from the SDK.

### 1. Start the infrastructure only

```bash
cp .env.example .env
docker compose up -d postgres redis minio minio-init   # PostgreSQL :5432, Redis :6379, MinIO :9000 (console :9001)
```

On Windows, `scripts\start-dev-services.cmd` does the same thing and additionally waits for the
Docker engine (starting Docker Desktop if it is not up) and for the health checks to pass.

These must be running **before** the API starts. Hangfire builds its PostgreSQL storage while the DI
container is being constructed, so with nothing on 5432 the API does not start degraded — it
terminates with `Npgsql … Failed to connect to 127.0.0.1:5432`, and the admin panel shows only its
generic "unexpected error" banner.

### 2. Run the API

```bash
dotnet run --project src/PcMarket.Api
```

On startup the API **applies EF Core migrations and seeds** roles + an admin user automatically.
It prints the listening URL (e.g. `http://localhost:5055`). Then:

| Endpoint | What |
|----------|------|
| `/` | Redirects to the API reference |
| `/scalar/v1` | Interactive API docs (Scalar) |
| `/openapi/v1.json` | OpenAPI document |
| `/api/v1/ping` | Liveness sanity check |
| `/health` | Dependency health (postgres / redis / minio) |
| `/hangfire` | Background-jobs dashboard (Admin role required) |

Verify everything is wired:

```bash
curl http://localhost:5055/health
# {"status":"Healthy","checks":[{"name":"postgres",...},{"name":"redis",...},{"name":"minio",...}]}
```

### 3. Seeded admin

A default admin is created on first run (override via `.env`):

- **Phone / username:** `+998900000000`
- **Password:** `Admin!23456`

### 4. (Optional) load a demo catalog

```bash
# PowerShell
$env:SEED_CATALOG_PATH="src/PcMarket.Infrastructure/Persistence/Seed/demo-catalog.json"
dotnet run --project src/PcMarket.Api
```

```bash
# bash
SEED_CATALOG_PATH=src/PcMarket.Infrastructure/Persistence/Seed/demo-catalog.json \
  dotnet run --project src/PcMarket.Api
```

---

## Running from Visual Studio

Same model as above — the backing services run in Docker, the apps run from the IDE. Nothing extra
needs configuring: `appsettings.json` already points the API at `localhost:5432/6379/9000`, and the
storefront and admin panel already point at `http://localhost:5055`.

1. Run `scripts\start-dev-services.cmd` (or the `docker compose` line above) and let it finish.
2. Open `PcMarket.slnx`.
3. Pick a launch profile from the Startup Projects dropdown on the toolbar, then F5.

`PcMarket.slnLaunch` defines three multi-project profiles:

| Profile | Starts | Opens |
|---------|--------|-------|
| `API + Web + Admin` | all three | storefront `:5193`, admin `:5146` |
| `API + Admin` | API and admin panel | admin `:5146` |
| `API only` | API | nothing (API's `launchBrowser` is false) |

All three use each project's `http` profile, so the apps talk to each other over plain HTTP and no
dev certificate is involved. Switch to `https` per project if you need TLS locally
(API `:7241`, storefront `:7030`, admin `:7035`) — set `Api:BaseUrl` to the HTTPS API address to match.

The API applies migrations and seeds on startup in Development, so the first F5 against an empty
database is enough to get a working catalog and the seeded admin from step 3 above.

> The full Docker stack (`docker compose up -d`) can run at the same time — it is reachable through
> Nginx on `:8080`/`:8443` and does not collide with the IDE's ports. Both share one PostgreSQL, so
> data written from either side is visible to the other.

---

## Build & test

```bash
# Build the whole solution (skip the MAUI head, which needs Android/iOS SDKs)
dotnet build src/PcMarket.Api
dotnet build src/PcMarket.Web
dotnet build src/PcMarket.Admin

# Unit tests (fast, no Docker)
dotnet test tests/PcMarket.UnitTests

# Integration tests (spin up throwaway PostgreSQL via Testcontainers — needs Docker running)
dotnet test tests/PcMarket.IntegrationTests
```

The **mobile app** builds via its workload; on Windows/macOS:

```bash
dotnet build src/PcMarket.Mobile -f net10.0-android
```

---

## Database migrations

The API applies migrations on startup in Development. That is convenient for one instance and wrong for
a rolling deploy, where several would race, so it is gated by `Database:MigrateOnStartup` — set it false
in production and run the schema change as its own step, using the same image the app runs from:

```bash
docker compose run --rm migrate     # `dotnet PcMarket.Api.dll --migrate`: migrate, seed, exit
```

Driving EF Core directly:

```bash
# Add a migration
dotnet ef migrations add <Name> \
  --project src/PcMarket.Infrastructure --startup-project src/PcMarket.Infrastructure \
  --output-dir Persistence/Migrations

# Apply to a database (defaults to the dev connection; override with POSTGRES_CONNECTION)
POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=pcmarket;Username=pcmarket;Password=pcmarket" \
  dotnet ef database update \
  --project src/PcMarket.Infrastructure --startup-project src/PcMarket.Infrastructure
```

---

## Configuration

Settings live in [src/PcMarket.Api/appsettings.json](src/PcMarket.Api/appsettings.json) and can be
overridden per environment or via environment variables (double-underscore syntax). Key sections:

| Section / variable | Purpose |
|--------------------|---------|
| `ConnectionStrings:Postgres` / `ConnectionStrings__Postgres` | Primary database |
| `Redis:Configuration` / `Redis__Configuration` | Redis endpoint |
| `Minio:*` / `Minio__*` | Object storage endpoint & credentials |
| `Jwt:SigningKey` / `Jwt__SigningKey` | **Override in production** (min 32 bytes) |
| `Payments:*` / `Payments__*` | Per-rail feature flags + gateway credentials |
| `Telegram:*` / `Telegram__*` | Telegram bot (see below); off by default |
| `Database:MigrateOnStartup` | Apply migrations when the API boots. Default: true in Development. **False in production** — use the gated `migrate` step |
| `Database:SeedDemoCatalog` | Import the bundled demo catalog (idempotent). Default: true in Development |
| `Api:BaseUrl` (Web/Admin) | Where the storefront and admin panel reach the API |
| `SEED_ADMIN_PHONE`, `SEED_ADMIN_PASSWORD` | Initial admin credentials |

### Compose settings (`.env`)

Read by `docker-compose.yml`; see [.env.example](.env.example) for the annotated list.

| Variable | Purpose |
|----------|---------|
| `JWT_SIGNING_KEY` | **Required** — compose refuses to start without it. Min 32 chars; `openssl rand -base64 48` |
| `POSTGRES_*`, `MINIO_*`, `REDIS_PORT` | Credentials and published ports for the data services |
| `MINIO_BUCKET` | Media bucket; also what Nginx serves `/media/` from |
| `STOREFRONT_HOST`, `ADMIN_HOST`, `API_HOST` | Hostnames Nginx routes on |
| `HTTP_PORT`, `HTTPS_PORT` | Ports Nginx publishes (80/443 in production) |
| `DB_MIGRATE_ON_STARTUP`, `DB_SEED_DEMO_CATALOG` | Map to the `Database:*` settings above |
| `IMAGE_REGISTRY`, `IMAGE_TAG` | Which images to run; defaults build locally, CI publishes to GHCR |
| `PUBLIC_STOREFRONT_URL`, `PUBLIC_API_URL` | Public URLs for payment return links and webhook registration |

Example — run the API against non-default infra ports:

```bash
# bash
ConnectionStrings__Postgres="Host=localhost;Port=5544;Database=pcmarket;Username=pcmarket;Password=pcmarket" \
Redis__Configuration="localhost:6380" \
Minio__Endpoint="localhost:9002" \
  dotnet run --project src/PcMarket.Api
```

> **Security note:** the dev `Jwt:SigningKey` and MinIO credentials in `appsettings.json` are
> placeholders. Supply real secrets via environment variables or Docker secrets in staging/production.

### Telegram bot

The bot is hosted in-process by the API and driven by a webhook, so it needs a **public HTTPS URL** —
use a tunnel (ngrok/cloudflared) locally or the staging host. It is disabled until a token is set;
with `Telegram:Enabled=false` the webhook returns 404 and every outbound Telegram message is a no-op,
so the rest of the system runs unchanged.

| Setting | Purpose |
|---------|---------|
| `Telegram__Enabled` | Master switch; maps the webhook endpoint |
| `Telegram__BotToken` | BotFather token |
| `Telegram__WebhookSecretToken` | Shared secret Telegram echoes in `X-Telegram-Bot-Api-Secret-Token`; **required** — a blank value rejects every update |
| `Telegram__AdminChatId` | Chat that receives new-order alerts (omit to disable them) |
| `Telegram__PublicApiUrl` | Public HTTPS base of this API, used to register the webhook |
| `Telegram__StorefrontUrl` | Storefront base, for "open in store" buttons |

Point Telegram at the running API (as an Admin/Manager, so the call is authorized):

```bash
curl -X POST https://<public-api>/api/v1/bot/telegram/set-webhook -H "Authorization: Bearer <token>"
curl     https://<public-api>/api/v1/bot/telegram/webhook-info    -H "Authorization: Bearer <token>"
```

Customers use `/start` → **Link my account** (phone + OTP) to attach their Telegram account; managers
whose linked account has the Admin or Manager role can advance order status straight from the alert.

### Mobile app

`PcMarket.Mobile` is a .NET MAUI storefront (catalog, cart, phone+OTP auth, checkout, order tracking,
profile/addresses) over the same `PcMarket.ApiClient` the bot uses. It needs the `android` (and, for the
iOS head, `ios`) workloads — `dotnet workload install maui` covers both.

```bash
# Compile both heads
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-ios

# Deploy and launch on a running emulator (add -p:AdbTarget="-s <device>" if several are attached)
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android -t:Run
```

Start the API first (`dotnet run --project src/PcMarket.Api`). The app resolves its backend in
`Services/AppConfig.cs`, and **the two device types resolve differently**:

| Target | Backend URL | Artwork URL | Extra setup |
|--------|-------------|-------------|-------------|
| Emulator | `http://10.0.2.2:5055` | `http://10.0.2.2:5193` | none — `10.0.2.2` is the emulator's alias for the host |
| Physical device | `http://localhost:5055` | `http://localhost:5193` | **an `adb reverse` tunnel is required, for both ports** |

The second column is `AppConfig.MediaRootUrl`: the app's decorative artwork (hero banner, category
tiles) is **fetched from the running storefront** rather than bundled, because the photography under
`PcMarket.Web/wwwroot/images` is an order of magnitude larger than the whole package. `Services/Artwork.cs`
resolves the paths and hands back cached image sources. Without the storefront running, or without its
tunnel, those images simply do not load — every one of them sits on a token-coloured panel, so the
screen degrades to flat surfaces rather than breaking. Product photography is unaffected: those URLs
come from the catalogue and are already absolute.

On a phone `localhost` is *the phone*, so without the tunnel every request fails at the transport
layer and the app shows "Can't reach the store. Check your connection and try again." That reads
like a Wi-Fi fault and is not one. On Windows:

```bat
scripts\start-mobile-dev.cmd
```

It brings up the backing services, runs the API on `:5055`, opens the tunnel, and then verifies the
path from both ends (host `/health`, plus a TCP connect from the device itself) so a silent failure
cannot be mistaken for success. Add `-WithStorefront` to also serve the storefront on `:5193` for
the app's remote artwork, and `-Launch` to deploy and start the app. It is idempotent — re-run it
whenever the error reappears, because reverse tunnels are bound to the adb transport and are lost
whenever it resets (cable unplugged, `adb kill-server`, or a device reconnect).

By hand it is:

```bash
adb reverse tcp:5055 tcp:5055     # and tcp:5193 for storefront artwork
```

Note that the `docker compose` stack cannot serve a device directly: it publishes only nginx on
`:8080`, and nginx routes by **Host header** (`localhost` to the storefront, `api.localhost` to the
API). A tunnel pointed at `:8080` would send the app's `/api/v1` calls to the Blazor storefront,
which cannot answer them, so the API is run on the host instead.

Android blocks cleartext HTTP by default, so debug builds carry a narrow exception for both
`10.0.2.2` and `localhost` in `Platforms/Android/Resources/xml/network_security_config.xml`.

Sessions persist in the platform keystore (`SecureStorage`) and a guest cart survives restarts via a
token in `Preferences`; signing in merges the guest cart into the account. Push notifications are wired
end to end but the Firebase pieces are stubbed — the app runs and logs that push is unavailable. See
[docs/specs/pcmarket_clone/mobile_app/push-setup.md](docs/specs/pcmarket_clone/mobile_app/push-setup.md)
to enable them, and [ios-build.md](docs/specs/pcmarket_clone/mobile_app/ios-build.md) for the iOS
build/signing steps (the iOS head compiles but has never been run — no Mac or Apple account here).

---

## Deployment & operations

Images are built by CI and promoted unchanged — what reaches production is byte-identical to what was
tested.

| Workflow | Trigger | Does |
|----------|---------|------|
| [ci.yml](.github/workflows/ci.yml) | push / PR to `master`, tags | Builds + tests the server projects, builds the Android head, and on `master`/tags pushes `api`/`web`/`admin` images to GHCR |
| [deploy.yml](.github/workflows/deploy.yml) | manual | Pulls a chosen tag on the VPS, runs the **gated migration step**, rolls the app containers, verifies `/health` |

Runbooks:

- **[deploy.md](docs/runbooks/deploy.md)** — first-time VPS setup, normal deploy, rollback, enabling
  TLS with Let's Encrypt, and a troubleshooting table.
- **[restore.md](docs/runbooks/restore.md)** — what `scripts/backup.sh` captures, the cron schedule,
  and the destructive restore procedure.
- **[gateway-onboarding.md](docs/runbooks/gateway-onboarding.md)** — taking Click/Payme from sandbox to
  live, including the callback-replay check.

Backups (`scripts/backup.sh`) capture the two things the repository cannot rebuild — the PostgreSQL
database and the MinIO media bucket — and fail loudly rather than keep an incomplete archive:

```bash
BACKUP_DIR=/var/backups/pcmarket ./scripts/backup.sh
./scripts/restore.sh /var/backups/pcmarket/<timestamp>    # destructive; asks for confirmation
```

---

## Tech stack

.NET 10 · ASP.NET Core · EF Core + PostgreSQL 17 · Redis · MinIO · Hangfire · Serilog ·
ASP.NET Core Identity + JWT · Blazor · .NET MAUI · Telegram.Bot · Docker.
