<#
.SYNOPSIS
    Brings up the PcMarket dev stack, opens fresh Cloudflare quick tunnels, and re-registers the
    Telegram webhook against the new URLs. Safe to re-run any time - e.g. after a PC restart, or
    whenever webhook-info shows a stale/dead URL.

.PARAMETER Build
    Pass -Build to rebuild images first (docker compose ... up -d --build). Not needed on a normal
    restart - only after pulling code changes.

.PARAMETER SkipStorefrontTunnel
    Skip the second tunnel (tunnel-web) and leave PUBLIC_STOREFRONT_URL untouched. The bot still
    works; only the "Open in store" button is affected (see docs/runbooks/telegram-bot.md).

.EXAMPLE
    .\scripts\start-telegram-bot.ps1
#>
param(
    [switch]$Build,
    [switch]$SkipStorefrontTunnel
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "WARNING: $msg" -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host $msg -ForegroundColor Green }

$envPath = Join-Path $repoRoot ".env"
if (-not (Test-Path $envPath)) {
    throw ".env not found at $envPath. Copy .env.example to .env first, fill in TELEGRAM_* and re-run."
}

# .env may contain non-ASCII characters (e.g. an em dash in a comment). Read/write it as UTF-8
# explicitly - Get-Content/Set-Content default to the system codepage on Windows PowerShell 5.1 and
# will silently mangle those bytes on a round trip.
function Read-EnvLines() {
    return [System.IO.File]::ReadAllLines($envPath, [System.Text.Encoding]::UTF8)
}

function Get-EnvValue([string]$key) {
    $line = Read-EnvLines | Where-Object { $_ -match "^\s*$key\s*=" } | Select-Object -Last 1
    if (-not $line) { return $null }
    return ($line -split '=', 2)[1].Trim()
}

function Set-EnvValue([string]$key, [string]$value) {
    $lines = Read-EnvLines
    $pattern = "^\s*$key\s*="
    $found = $false
    $newLines = foreach ($line in $lines) {
        if ($line -match $pattern) { $found = $true; "$key=$value" } else { $line }
    }
    if (-not $found) { $newLines += "$key=$value" }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($envPath, $newLines, $utf8NoBom)
}

if ((Get-EnvValue "TELEGRAM_ENABLED") -ne "true") {
    Write-Warn "TELEGRAM_ENABLED is not 'true' in .env. Set TELEGRAM_ENABLED, TELEGRAM_BOT_TOKEN and TELEGRAM_WEBHOOK_SECRET first (docs/runbooks/telegram-bot.md)."
    exit 1
}

# Probe via cmd so docker's stderr never reaches PowerShell: with $ErrorActionPreference = "Stop",
# redirecting a native command's stderr inside PS 5.1 raises a terminating NativeCommandError even
# when we only care about the exit code.
function Test-DockerReady() {
    cmd /c "docker info >nul 2>&1"
    return $LASTEXITCODE -eq 0
}

if (-not (Test-DockerReady)) {
    Write-Step "Docker engine not responding - starting Docker Desktop"
    $dockerDesktopExe = @(
        "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\Docker Desktop.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $dockerDesktopExe) {
        throw "Docker engine is not running and Docker Desktop.exe was not found. Start Docker Desktop manually and re-run."
    }

    Start-Process $dockerDesktopExe

    $timeoutSec = 180
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while (-not (Test-DockerReady)) {
        if ((Get-Date) -ge $deadline) {
            throw "Docker Desktop did not become ready within $timeoutSec s. It may need a manual start (or a PC restart if it's crash-looping) - check the Docker Desktop window for errors."
        }
        Start-Sleep -Seconds 5
    }
    Write-Ok "Docker engine is ready"
}

# Docker pins a container to the network *ID* it was created against, not the name. If the compose
# network is ever replaced - a `docker compose down`, a Docker Desktop reset, or two projects taking
# turns owning these fixed container_names - it comes back under the same name with a new ID, and any
# stopped container still holding the old ID can never start again:
#     failed to set up container networking: network <old-id> not found
# `up -d` reports that as a hard failure, so the run dies here, before reaching the --force-recreate
# below that would have rebuilt the offending container anyway. Clearing the corpses first is what
# makes this script self-healing; compose recreates them attached to the live network.
function Remove-StaleNetworkContainers() {
    Write-Step "Checking for containers pinned to a deleted network"

    # --no-trunc: `docker network ls` truncates to 12 chars by default, but a container records the
    # full 64-char ID, so the two would never compare equal.
    $liveNetworks = @{}
    foreach ($netId in (docker network ls --no-trunc --format '{{.ID}}')) {
        $trimmed = "$netId".Trim()
        if ($trimmed) { $liveNetworks[$trimmed] = $true }
    }
    # An empty list means the docker query itself failed. Treating that as "every network is gone"
    # would delete the whole stack, so bail out and let `up` report the real problem.
    if ($liveNetworks.Count -eq 0) {
        Write-Warn "Could not list docker networks - skipping the stale-container check."
        return
    }

    # Deliberately NOT `docker compose ps`: compose reconciles that listing against the current config
    # and silently omits containers that have drifted from it - which is exactly the set we are hunting.
    # (Measured: a stale tunnel container was missing from `compose ps -aq` but present here.) Filtering
    # on working_dir scopes this to containers compose created from *this* checkout, so a second project
    # sharing these container_names is never touched.
    $containerIds = docker ps -aq --filter "label=com.docker.compose.project.working_dir=$repoRoot"
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Could not list this project's containers - skipping the stale-container check."
        return
    }

    $removed = 0
    foreach ($rawId in $containerIds) {
        $cid = "$rawId".Trim()
        if (-not $cid) { continue }

        # Only ever touch a container that is already stopped: a running one demonstrably has a
        # working network, and a healthy stack must survive this function untouched.
        $running = (docker inspect -f '{{.State.Running}}' $cid | Out-String).Trim()
        if ($running -ne "false") { continue }

        $refs = (docker inspect -f '{{range .NetworkSettings.Networks}}{{.NetworkID}} {{end}}' $cid | Out-String)
        $isStale = $false
        foreach ($ref in ($refs -split '\s+')) {
            $r = $ref.Trim()
            if ($r -and -not $liveNetworks.ContainsKey($r)) { $isStale = $true }
        }
        if (-not $isStale) { continue }

        $name = (docker inspect -f '{{.Name}}' $cid | Out-String).Trim().TrimStart('/')
        Write-Warn "Removing stopped container '$name' - it is pinned to a network that no longer exists. Compose will recreate it."
        docker rm $cid | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not remove the stale container '$name'. Remove it manually with: docker rm $name"
        }
        $removed++
    }

    if ($removed -eq 0) { Write-Ok "No stale containers" } else { Write-Ok "Removed $removed stale container(s)" }
}

Remove-StaleNetworkContainers

Write-Step "Starting the dev stack (postgres, redis, minio, api, web, admin, nginx, tunnels)"
$upArgs = @("compose", "--profile", "dev", "up", "-d")
if ($Build) { $upArgs += "--build" }
docker @upArgs
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed" }

# A quick tunnel that has been up for hours can lose its edge connection and never get it back: cloudflared
# keeps retrying, the container stays "Up" (it has no healthcheck, and a failed tunnel is not a failed
# process), and the hostname it was given stops resolving. `up -d` is a no-op for a running container, so
# without this the script would happily read that dead hostname out of the log and register it.
# Recreating the tunnels each run also makes the --since scoping below true by construction: one run, one
# hostname.
$tunnelServices = @("tunnel")
if (-not $SkipStorefrontTunnel) { $tunnelServices += "tunnel-web" }

Write-Step "Recreating the quick tunnel(s) so this run gets a live hostname"
docker compose --profile dev up -d --force-recreate @tunnelServices
if ($LASTEXITCODE -ne 0) { throw "docker compose up --force-recreate $($tunnelServices -join ' ') failed" }

# `docker compose logs` keeps output from earlier runs of a container, and a quick tunnel mints a new
# hostname on every start. Reading the whole log would match a URL from a previous run and register a
# hostname that is already dead, so every log read is scoped to the container's current run via --since.
function Get-ContainerSince([string]$containerName) {
    $startedAtRaw = (docker inspect -f '{{.State.StartedAt}}' $containerName | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $startedAtRaw) { throw "Could not inspect $containerName" }
    # A second of slack absorbs clock skew between the daemon and the host.
    return ([datetime]::Parse($startedAtRaw)).ToUniversalTime().AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
}

function Wait-TunnelUrl([string]$service, [string]$containerName, [int]$timeoutSec = 90) {
    Write-Step "Waiting for the '$service' quick tunnel to come up"

    $since = Get-ContainerSince $containerName

    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $logs = docker compose logs --since $since $service
        $match = $logs |
            Select-String -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' -AllMatches |
            ForEach-Object { $_.Matches.Value } |
            Select-Object -Last 1
        if ($match) { return $match }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for '$service' to publish a trycloudflare.com URL. Check: docker compose logs $service"
}

# Telegram accepts any well-formed HTTPS URL at registration and only reports a problem later, the first
# time it tries to deliver - so a hostname that leads nowhere looks like a clean run and leaves a silent
# bot. The check for that is cloudflared's own "Registered tunnel connection": printed once the edge has
# accepted the connection, which is exactly what a hostname needs to route anywhere. A tunnel stuck in a
# retry loop never prints it, and that is the failure this guards against.
function Assert-TunnelConnected([string]$service, [string]$containerName, [string]$since, [int]$timeoutSec = 60) {
    Write-Step "Waiting for the '$service' tunnel to register with Cloudflare's edge"

    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $logs = docker compose logs --since $since $service
        if ($logs | Select-String -Pattern 'Registered tunnel connection' -Quiet) {
            Write-Ok "Edge connection registered"
            return
        }
        Start-Sleep -Seconds 2
    }

    throw "The '$service' tunnel never registered a connection with Cloudflare, so its hostname will not route. Check: docker compose logs $service"
}

# A best-effort look from this machine. Deliberately NOT fatal: what matters is whether *Telegram* can
# reach the URL, and this host's resolver often cannot answer for a hostname minted seconds ago (or hands
# back an AAAA record on a machine with no IPv6 route). Failing the run on that would stop a setup that
# actually works - the connection check above is the real gate.
function Test-TunnelReachable([string]$url, [string]$path, [int]$timeoutSec = 30) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri "$url$path" -UseBasicParsing -TimeoutSec 10 -Method Get
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Ok "Reachable from this machine (HTTP $($response.StatusCode))"
                return
            }
            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 3
    }

    Write-Warn "Could not reach $url$path from this machine ($lastError). This is often just local DNS lagging behind a brand-new tunnel hostname; Telegram resolves it independently. Continuing - webhook-info at the end reports what Telegram actually sees."
}

# Telegram rejects set-webhook outright when the hostname does not resolve for *it*:
#     Bad Request: bad webhook: Failed to resolve host: Name or service not known
# The edge-connection check above cannot catch that - cloudflared can be happily connected while the
# public DNS record for the new hostname is still seconds behind. Test-TunnelReachable cannot catch it
# either on a network whose own resolver refuses these names (a router filtering trycloudflare.com
# answers NXDOMAIN forever, so that probe warns on every run and its warning carries no information).
# Asking a public resolver directly is the closest proxy available for "can Telegram resolve this yet",
# and unlike the local probe it is worth blocking on: registering before it comes back is the failure.
function Wait-PublicDns([string]$url, [int]$timeoutSec = 90) {
    $hostName = ([uri]$url).Host
    Write-Step "Waiting for '$hostName' to resolve on public DNS"

    # Two independent resolvers, so one being slow to pick up the record does not stall the run.
    $resolvers = @("1.1.1.1", "8.8.8.8")
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        foreach ($resolver in $resolvers) {
            try {
                $answers = Resolve-DnsName -Name $hostName -Server $resolver -Type A -ErrorAction Stop |
                    Where-Object { $_.IPAddress }
                if ($answers) {
                    Write-Ok "Resolves via $resolver ($(($answers | Select-Object -First 2 -ExpandProperty IPAddress) -join ', '))"
                    return
                }
            }
            catch {
                # NXDOMAIN while the record is still propagating - keep waiting.
            }
        }
        Start-Sleep -Seconds 3
    }

    Write-Warn "'$hostName' still does not resolve on public DNS after $timeoutSec s. Telegram will likely reject the webhook with 'Failed to resolve host'. Continuing anyway - the registration step retries."
}

$apiUrl = Wait-TunnelUrl "tunnel" "pcmarket-tunnel"
Write-Ok "API tunnel:        $apiUrl"
Assert-TunnelConnected "tunnel" "pcmarket-tunnel" (Get-ContainerSince "pcmarket-tunnel")
Wait-PublicDns $apiUrl
Test-TunnelReachable $apiUrl "/health"

$storefrontUrl = $null
if (-not $SkipStorefrontTunnel) {
    $storefrontUrl = Wait-TunnelUrl "tunnel-web" "pcmarket-tunnel-web"
    Write-Ok "Storefront tunnel: $storefrontUrl"
    Assert-TunnelConnected "tunnel-web" "pcmarket-tunnel-web" (Get-ContainerSince "pcmarket-tunnel-web")
}

Write-Step "Updating .env with the fresh tunnel URL(s)"
Set-EnvValue "PUBLIC_API_URL" $apiUrl
if ($storefrontUrl) { Set-EnvValue "PUBLIC_STOREFRONT_URL" $storefrontUrl }

Write-Step "Restarting the API so it picks up the new URL(s)"
docker compose up -d --no-deps api
if ($LASTEXITCODE -ne 0) { throw "docker compose up --no-deps api failed" }

Write-Step "Waiting for the API to become healthy"
$deadline = (Get-Date).AddSeconds(60)
$status = $null
do {
    $status = docker inspect -f '{{.State.Health.Status}}' pcmarket-api
    if ($status -eq "healthy") { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)
if ($status -ne "healthy") { throw "API did not become healthy in time. Check: docker compose logs api" }
Write-Ok "API is healthy"

Write-Step "Logging in and registering the Telegram webhook"
$phone = Get-EnvValue "SEED_ADMIN_PHONE"
if (-not $phone) { $phone = "+998900000000" }
$password = Get-EnvValue "SEED_ADMIN_PASSWORD"
if (-not $password) { $password = "Admin!23456" }

$loginBody = @{ phone = $phone; password = $password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/auth/login" -Method Post `
    -ContentType "application/json" -Headers @{ Host = "api.localhost" } -Body $loginBody
$token = $login.accessToken

# Even with the public-DNS gate passed, Telegram's own resolvers can still be a beat behind and answer
# "Failed to resolve host" for a hostname minted a minute ago. That is transient and clears on its own,
# so a bare first-attempt failure should not sink a run that is otherwise correct - retry a few times
# before giving up, and surface the API's actual message if it never takes.
$setResult = $null
$attempts = 5
for ($i = 1; $i -le $attempts; $i++) {
    try {
        $setResult = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/bot/telegram/set-webhook" -Method Post `
            -Headers @{ Host = "api.localhost"; Authorization = "Bearer $token" }
        break
    }
    catch {
        # Invoke-RestMethod throws on a 4xx/5xx and discards the body, which is where the API puts
        # Telegram's own wording - read it off the response stream before it is lost.
        $detail = $_.Exception.Message
        $response = $_.Exception.Response
        if ($response) {
            try {
                $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
                $body = $reader.ReadToEnd()
                $reader.Close()
                if ($body) { $detail = $body }
            }
            catch { }
        }

        if ($i -eq $attempts) {
            throw "set-webhook failed after $attempts attempts. Last response: $detail"
        }
        Write-Warn "set-webhook attempt $i/$attempts failed, retrying in 10s. Response: $detail"
        Start-Sleep -Seconds 10
    }
}
Write-Ok "Webhook registered: $($setResult.webhookUrl)"

Start-Sleep -Seconds 2
$info = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/bot/telegram/webhook-info" -Method Get `
    -Headers @{ Host = "api.localhost"; Authorization = "Bearer $token" }
$info | ConvertTo-Json

if ($info.lastErrorMessage) {
    Write-Warn "Telegram reports a webhook error: $($info.lastErrorMessage)"
    exit 1
}

Write-Ok "`nAll set. Send /start to the bot on Telegram."
