# PCMarket — Developer Runbook

How to get the project running locally: start the infrastructure (Docker), then
run the API, Web (storefront) and Admin apps. Follow the sections in order.

---

## 1. Prerequisites

Make sure these are installed:

- **.NET SDK 10** — check with `dotnet --version`
- **Docker Desktop** — must be running before you start
- **Git** — to clone / pull the repo

The project lives at `C:\Store_Project\Market` in these instructions. Adjust the
path if yours differs.

---

## 2. Start the infrastructure (Docker)

The app needs three services running in containers: **PostgreSQL** (database),
**Redis** (cache) and **MinIO** (file storage). These are defined in the
project's `docker-compose` file.

From the project root:

```bash
cd C:\Store_Project\Market
docker compose up -d postgres redis minio
```

`-d` runs them in the background. Confirm all three are up and healthy:

```bash
docker ps
```

You should see `pcmarket-postgres`, `pcmarket-redis` and `pcmarket-minio`, each
with status **Up ... (healthy)**. If a container shows as *Exited*, start it again
with the `docker compose up -d` command above.

> **Note:** The database keeps its data in a persistent volume, so your data
> survives restarts. You don't re-seed every time.

---

## 3. Run the applications

The project has **three** apps that run at the same time, each in its **own**
terminal window (each `dotnet run` keeps running and blocks that window):

| App | Folder | Purpose |
|-----|--------|---------|
| **API** | `src\PcMarket.Api` | Backend — must start first |
| **Web** | `src\PcMarket.Web` | Customer storefront |
| **Admin** | `src\PcMarket.Admin` | Back-office panel |

Start the **API first** — the Web and Admin apps pull their data from it.

### 3.1 — API (terminal 1)

```bash
cd C:\Store_Project\Market\src\PcMarket.Api
dotnet run
```

Wait until it prints `Now listening on: http://localhost:XXXX`. Leave this
window open and running.

### 3.2 — Web / storefront (terminal 2)

```bash
cd C:\Store_Project\Market\src\PcMarket.Web
dotnet run
```

Open the URL it prints (e.g. `http://localhost:5155`) in a browser.

### 3.3 — Admin panel (terminal 3)

```bash
cd C:\Store_Project\Market\src\PcMarket.Admin
dotnet run
```

Open the URL it prints (e.g. `http://localhost:5146`) in a browser.

---

## 4. Log in to the Admin panel

Default seeded credentials (from `DbSeeder.cs`):

- **Phone:** `+998900000000`
- **Password:** `Admin!23456`

The password can be overridden by the `SEED_ADMIN_PASSWORD` environment variable;
if it isn't set, the default above applies.

> The admin panel interface is intentionally English-only. It is the back-office
> tool, not the customer storefront — the RU/UZ/EN language switcher is a
> storefront feature.

---

## 5. Common problem: "The file is locked by another process"

If a build fails with `error MSB3021 / MSB3027 ... file is locked by:
"PcMarket.Api (PID)"`, it means a previous instance of that app is still running
and holding its files.

**Fix (Windows Command Prompt):** stop the stale process(es), then run again.

```cmd
taskkill /F /IM PcMarket.Api.exe
taskkill /F /IM PcMarket.Web.exe
taskkill /F /IM PcMarket.Admin.exe
```

Messages like *"process not found"* just mean that one wasn't running — safe to
ignore. Then re-run `dotnet run`.

> `taskkill /F /IM` is the Command Prompt way to force-kill by process name. The
> PowerShell equivalent is
> `Get-Process -Name PcMarket.Api | Stop-Process -Force`.

---

## 6. Quick reference

```bash
# Start infrastructure
docker compose up -d postgres redis minio
docker ps                       # verify healthy

# Run apps (each in its own terminal, API first)
cd src\PcMarket.Api    && dotnet run
cd src\PcMarket.Web    && dotnet run
cd src\PcMarket.Admin  && dotnet run

# If files are locked
taskkill /F /IM PcMarket.Api.exe
taskkill /F /IM PcMarket.Web.exe
taskkill /F /IM PcMarket.Admin.exe
```

**Startup order that matters:** Docker containers → API → Web / Admin.
