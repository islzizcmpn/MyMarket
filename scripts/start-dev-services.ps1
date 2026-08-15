<#
.SYNOPSIS
    Brings up only the backing services the apps need when you run them from Visual Studio or
    `dotnet run`: PostgreSQL, Redis, MinIO (plus the one-shot bucket init).

.DESCRIPTION
    The API opens PostgreSQL eagerly during startup - Hangfire builds its storage while the DI
    container is being constructed - so with nothing listening on 5432 the host does not start
    degraded, it terminates. That surfaces in the browser as the admin panel's generic
    "unexpected error" banner, which says nothing about the real cause. Running this first
    removes that whole class of confusion.

    Safe to re-run at any time; already-running containers are left alone.

.PARAMETER SkipDockerStart
    Do not try to launch Docker Desktop when its engine is unreachable; fail instead. Useful on a
    machine where the engine is managed some other way.

.EXAMPLE
    .\scripts\start-dev-services.ps1
#>
param(
    [switch]$SkipDockerStart
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "WARNING: $msg" -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host $msg -ForegroundColor Green }

# docker compose interpolates the whole file up front, and the api service declares
# JWT_SIGNING_KEY with `:?`, so a missing .env fails the command even though we only start
# postgres/redis/minio here.
if (-not (Test-Path (Join-Path $repoRoot ".env"))) {
    throw ".env not found. Copy .env.example to .env first, then re-run."
}

function Test-DockerEngine {
    docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

Write-Step "Checking the Docker engine"
if (Test-DockerEngine) {
    Write-Ok "Engine is up."
} elseif ($SkipDockerStart) {
    throw "Docker engine is not reachable and -SkipDockerStart was passed."
} else {
    $desktop = Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
    if (-not (Test-Path $desktop)) {
        throw "Docker engine is not reachable and Docker Desktop was not found at $desktop."
    }

    # The tray process can already be running while the engine is still booting, so start it only
    # when it is absent and then wait on the engine either way.
    if (-not (Get-Process "Docker Desktop" -ErrorAction SilentlyContinue)) {
        Write-Host "Starting Docker Desktop..."
        Start-Process $desktop | Out-Null
    } else {
        Write-Host "Docker Desktop is running but the engine is not answering yet."
    }

    Write-Host "Waiting for the engine (up to 3 minutes)..."
    $deadline = (Get-Date).AddMinutes(3)
    while (-not (Test-DockerEngine)) {
        if ((Get-Date) -gt $deadline) {
            throw "Docker engine did not become reachable. If Docker Desktop is crash-looping on startup, a Windows restart clears it."
        }
        Start-Sleep -Seconds 5
    }
    Write-Ok "Engine is up."
}

Write-Step "Starting postgres, redis, minio"
# minio-init makes the media bucket anonymously readable; without it uploaded images 404/403.
docker compose up -d postgres redis minio minio-init
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE." }

Write-Step "Waiting for health checks"
$deadline = (Get-Date).AddMinutes(2)
$pending = @("pcmarket-postgres", "pcmarket-redis", "pcmarket-minio")
while ($pending.Count -gt 0) {
    $stillPending = @()
    foreach ($name in $pending) {
        $state = docker inspect -f '{{.State.Health.Status}}' $name 2>$null
        if ($state -ne "healthy") { $stillPending += $name }
    }
    $pending = $stillPending
    if ($pending.Count -eq 0) { break }
    if ((Get-Date) -gt $deadline) {
        Write-Warn ("Still not healthy after 2 minutes: " + ($pending -join ", "))
        Write-Warn "Check with: docker compose logs postgres redis minio"
        exit 1
    }
    Start-Sleep -Seconds 3
}

Write-Ok "`nBacking services are healthy:"
Write-Host "  PostgreSQL  localhost:5432   (pcmarket / pcmarket)"
Write-Host "  Redis       localhost:6379"
Write-Host "  MinIO       localhost:9000   console http://localhost:9001 (minioadmin / minioadmin)"
Write-Host "`nVisual Studio is now free to F5 the 'API + Web + Admin' launch profile."
