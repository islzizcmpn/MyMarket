<#
.SYNOPSIS
    Points a physical Android device at the local backend, then proves the path actually works.

.DESCRIPTION
    A debug build resolves its backend in Services/AppConfig.cs. On an *emulator* that is
    http://10.0.2.2:5055, which needs no help. On a *physical device* it is http://localhost:5055 -
    and on the phone "localhost" is the phone. Without an `adb reverse` tunnel every request dies at
    the transport layer and the app shows:

        Can't reach the store. Check your connection and try again.

    That string comes from the HttpRequestException arm of BaseViewModel.RunAsync, so it means the
    connection was never established. It reads like a Wi-Fi problem and is not one - the phone is
    talking to itself.

    Two things have to be true, and one without the other still fails:

      1. Something is listening on the host at :5055.
      2. A reverse tunnel carries the device's localhost:5055 to it.

    This script arranges both and then verifies them from both ends.

    Note on the Docker stack: `docker compose up` publishes only nginx (:8080), and nginx routes by
    Host header - "localhost" goes to the storefront, "api.localhost" to the API. A tunnel pointed
    at :8080 would therefore land the app's /api/v1 calls on the Blazor storefront, which cannot
    serve them. The device cannot rewrite its Host header, so the API is run on the host instead,
    exactly as the README's dev loop describes. The compose stack can keep running alongside; the
    ports do not collide.

    Reverse tunnels live on the adb transport and are silently lost whenever it resets - unplugging
    the cable, the device sleeping into a reconnect, or `adb kill-server`. When the app starts
    failing again after it had been working, re-run this script first; that is almost always it.

    Safe to re-run at any time.

.PARAMETER Device
    adb serial to target. Only needed when more than one device is attached.

.PARAMETER WithStorefront
    Also run the storefront on :5193 and tunnel it. Needed once the app fetches its decorative
    artwork over HTTP (mobile_redesign task 8); not needed for catalog, cart or checkout.

.PARAMETER SkipServices
    Do not run start-dev-services.ps1 first. Use when postgres/redis/minio are already up and you
    want to skip the Docker checks.

.PARAMETER Launch
    Deploy and start the app on the device when the tunnels are up.

.EXAMPLE
    .\scripts\start-mobile-dev.ps1

.EXAMPLE
    .\scripts\start-mobile-dev.ps1 -WithStorefront -Launch
#>
param(
    [string]$Device,
    [switch]$WithStorefront,
    [switch]$SkipServices,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$ApiPort = 5055
$WebPort = 5193
$PackageName = "uz.pcmarket.mobile"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "WARNING: $msg" -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host $msg -ForegroundColor Green }

function Resolve-Adb {
    $onPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $roots = @($env:LOCALAPPDATA, $env:ANDROID_HOME, $env:ANDROID_SDK_ROOT) | Where-Object { $_ }
    foreach ($root in $roots) {
        foreach ($leaf in @("Android\Sdk\platform-tools\adb.exe", "platform-tools\adb.exe")) {
            $candidate = Join-Path $root $leaf
            if (Test-Path $candidate) { return $candidate }
        }
    }

    throw "adb was not found on PATH or under LOCALAPPDATA/ANDROID_HOME/ANDROID_SDK_ROOT. Install the Android SDK platform-tools, or add adb to PATH."
}

# A TCP connect rather than Test-NetConnection: this runs in a wait loop and Test-NetConnection
# takes seconds per call even against a closed local port.
function Test-HostPort([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(1000)) { return $false }
        $client.EndConnect($async)
        return $true
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Test-ApiHealthy {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -UseBasicParsing -TimeoutSec 5
        return ($response.StatusCode -eq 200) -and ($response.Content -match '"status"\s*:\s*"Healthy"')
    } catch {
        return $false
    }
}

# Starts a project in its own window so its log stays readable and Ctrl+C stops just that process.
function Start-DotnetProject([string]$Project, [int]$Port, [string]$Label) {
    if (Test-HostPort $Port) {
        Write-Ok "$Label is already listening on :$Port."
        return
    }

    Write-Host "Starting $Label (dotnet run --project $Project) in a new window..."
    Start-Process -FilePath "dotnet" `
                  -ArgumentList @("run", "--project", $Project, "--launch-profile", "http") `
                  -WorkingDirectory $repoRoot | Out-Null

    Write-Host "Waiting for :$Port (up to 3 minutes; the first run applies EF migrations)..."
    $deadline = (Get-Date).AddMinutes(3)
    while (-not (Test-HostPort $Port)) {
        if ((Get-Date) -gt $deadline) {
            throw "$Label did not start listening on :$Port. Check the window it opened for the error."
        }
        Start-Sleep -Seconds 2
    }
    Write-Ok "$Label is up on :$Port."
}

$adb = Resolve-Adb

Write-Step "Selecting a device"
$attached = @()
$problems = @()
& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' } | ForEach-Object {
    $parts = $_ -split '\s+'
    if ($parts.Count -ge 2) {
        if ($parts[1] -eq "device") { $attached += $parts[0] }
        else { $problems += ("{0} ({1})" -f $parts[0], $parts[1]) }
    }
}

if ($problems.Count -gt 0) {
    Write-Warn ("Ignoring devices that are not ready: " + ($problems -join ", "))
    Write-Warn "'unauthorized' means the USB-debugging prompt on the phone has not been accepted; 'offline' usually clears with: adb reconnect offline"
}

if ($Device) {
    if ($attached -notcontains $Device) {
        throw "Device '$Device' is not attached and ready. Ready now: $($attached -join ', ')"
    }
    $serial = $Device
} elseif ($attached.Count -eq 1) {
    $serial = $attached[0]
} elseif ($attached.Count -eq 0) {
    throw "No ready Android device is attached. Plug the phone in, accept the USB-debugging prompt, and re-run."
} else {
    throw "More than one device is attached ($($attached -join ', ')). Re-run with -Device <serial>."
}

$model = (& $adb -s $serial shell getprop ro.product.model) -replace '\s',''
Write-Ok "Using $serial ($model)."

if ($serial -like "emulator-*") {
    Write-Warn "This is an emulator. AppConfig points emulators at http://10.0.2.2:$ApiPort, so the tunnels below are not what it uses - they are set anyway and do no harm."
}

if (-not $SkipServices) {
    Write-Step "Backing services"
    & (Join-Path $PSScriptRoot "start-dev-services.ps1")
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "start-dev-services.ps1 failed. Fix that first - the API terminates at startup without PostgreSQL."
    }
}

Write-Step "Backend on the host"
Start-DotnetProject "src/PcMarket.Api" $ApiPort "API"
if ($WithStorefront) {
    Start-DotnetProject "src/PcMarket.Web" $WebPort "Storefront"
}

Write-Step "Reverse tunnels"
$ports = @($ApiPort)
if ($WithStorefront) { $ports += $WebPort }

foreach ($port in $ports) {
    & $adb -s $serial reverse "tcp:$port" "tcp:$port" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "adb reverse failed for tcp:$port." }
    Write-Ok "  device localhost:$port -> host localhost:$port"
}

Write-Step "Verifying"

if (Test-ApiHealthy) {
    Write-Ok "  host    /health reports Healthy (postgres, redis, minio)"
} else {
    throw "The API is listening on :$ApiPort but /health is not Healthy. Check the API window; a dependency is probably down."
}

$listed = & $adb -s $serial reverse --list
foreach ($port in $ports) {
    if (($listed -join "`n") -notmatch "tcp:$port") {
        throw "The tunnel for tcp:$port is not registered. The adb transport may have reset; re-run this script."
    }
}
Write-Ok "  tunnel  registered on the adb transport"

# The device-side check is a TCP connect, not an HTTP request. This ROM ships no curl or wget, and
# toybox nc connects but will not relay a response body back over the adb reverse socket (measured
# on the Redmi Note 11 - it returns 0 bytes and exits 0). The exit code still discriminates
# perfectly, which is all that is being asked here: no tunnel gives "connect: Connection refused"
# and RC=1, a working tunnel gives RC=0. Paired with the /health check above, that covers both
# halves - the device can reach the host port, and what answers there is a healthy API.
foreach ($port in $ports) {
    $probe = & $adb -s $serial exec-out ('nc -w 3 127.0.0.1 {0} < /dev/null; echo RC=$?' -f $port)
    if (($probe -join "`n") -match "RC=0") {
        Write-Ok "  device  can open tcp:$port through the tunnel"
    } else {
        throw "The device could not connect to tcp:$port through the tunnel. Re-run this script; if it persists, unplug and replug the cable and check that USB debugging is still authorized."
    }
}

if ($Launch) {
    Write-Step "Deploying the app"
    dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android -t:Install -p:AdbTarget="-s $serial"
    if ($LASTEXITCODE -ne 0) { throw "Deploy failed with exit code $LASTEXITCODE." }

    & $adb -s $serial shell am force-stop $PackageName | Out-Null
    & $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Ok "Launched $PackageName."
}

Write-Ok "`nThe device can reach the store."
Write-Host "  API         http://localhost:$ApiPort   (health: /health, docs: /scalar/v1)"
if ($WithStorefront) {
    Write-Host "  Storefront  http://localhost:$WebPort"
}
Write-Host "`nIf 'Can't reach the store' comes back, the adb transport reset and took the tunnels"
Write-Host "with it. Re-run this script - it is idempotent."
if (-not $Launch) {
    Write-Host "`nDeploy and start the app with:"
    Write-Host "  dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android -t:Run -p:AdbTarget=`"-s $serial`""
}
