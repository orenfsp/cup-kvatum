[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ProjectName = 'otklik',
    [ValidateRange(1, 65535)]
    [int]$FrontendPort = 3000,
    [ValidateRange(1, 65535)]
    [int]$ApiPort = 8080,
    [ValidateRange(30, 900)]
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$composePath = Join-Path $repositoryRoot 'docker-compose.yml'
$environmentPath = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "docker-compose.yml not found at $composePath"
}

function New-RandomBase64([int]$byteCount) {
    $bytes = [byte[]]::new($byteCount)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes)
}

function New-RandomHex([int]$byteCount) {
    $bytes = [byte[]]::new($byteCount)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToHexString($bytes).ToLowerInvariant()
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    $lines = @(
        'POSTGRES_DB=otklik'
        'POSTGRES_USER=otklik'
        "POSTGRES_PASSWORD=local-$(New-RandomHex 18)"
        'ASPNETCORE_ENVIRONMENT=Development'
        'OTKLIK_OPERATOR_PASSWORD=Operator!2026'
        'OTKLIK_EXPERT_PASSWORD=ExpertHelp!2026'
        'OTKLIK_ADMINISTRATOR_PASSWORD=AdminPanel!2026'
        "OTKLIK_TRACK_HASH_KEY=$(New-RandomBase64 32)"
        "OTKLIK_RATE_LIMIT_HASH_KEY=$(New-RandomBase64 32)"
        'OTKLIK_PUSH_PUBLIC_KEY='
        'OTKLIK_PUSH_PRIVATE_KEY='
        'OTKLIK_PUSH_SUBJECT=mailto:dev@otklik.local'
    )
    [System.IO.File]::WriteAllLines($environmentPath, $lines, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Created a local ignored .env with random database and HMAC secrets.'
}

$placeholder = Select-String -LiteralPath $environmentPath -Pattern '<[^>]+>' -Quiet
if ($placeholder) {
    throw '.env still contains placeholder values. Replace them or remove .env and run this script again.'
}

$env:OTKLIK_FRONTEND_PORT = $FrontendPort.ToString()
$env:OTKLIK_API_PORT = $ApiPort.ToString()
$requiredServices = @('postgres', 'redis', 'kafka', 'api', 'worker', 'frontend')

function Test-ComposeHealth([string]$composeProject, [string[]]$services) {
    foreach ($service in $services) {
        $containerId = (& docker compose -p $composeProject ps -q $service 2>$null).Trim()
        if (-not $containerId) { return $false }
        $status = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $containerId 2>$null).Trim()
        if ($status -ne 'healthy') { return $false }
    }
    return $true
}

Push-Location $repositoryRoot
try {
    & docker compose -p $ProjectName up --build -d
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed with exit code $LASTEXITCODE." }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $apiReady = $false
    $frontendReady = $false
    $servicesReady = $false
    do {
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/health/ready" -TimeoutSec 3
            $apiReady = $health.status -eq 'Healthy'
        }
        catch { $apiReady = $false }

        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$FrontendPort/" -TimeoutSec 3 -UseBasicParsing
            $frontendReady = $response.StatusCode -eq 200
        }
        catch { $frontendReady = $false }

        $servicesReady = Test-ComposeHealth $ProjectName $requiredServices

        if (-not ($apiReady -and $frontendReady -and $servicesReady)) {
            Start-Sleep -Seconds 2
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline -and -not ($apiReady -and $frontendReady -and $servicesReady))

    if (-not ($apiReady -and $frontendReady -and $servicesReady)) {
        & docker compose -p $ProjectName ps
        & docker compose -p $ProjectName logs --tail 80 api worker frontend
        throw "The demo did not become ready within $TimeoutSeconds seconds."
    }

    & docker compose -p $ProjectName ps
    Write-Host "Demo is ready: http://localhost:$FrontendPort"
    Write-Host "Readiness:    http://localhost:$ApiPort/api/health/ready"
    Write-Host "All six Compose services are healthy."
}
finally {
    Pop-Location
}
