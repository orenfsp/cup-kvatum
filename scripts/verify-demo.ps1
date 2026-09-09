[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$FrontendPort = 3000,
    [ValidateRange(1, 65535)]
    [int]$ApiPort = 8080
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Net.Http

$health = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/health/ready" -TimeoutSec 10
if ($health.status -ne 'Healthy') { throw 'API readiness is not Healthy.' }

$landing = Invoke-WebRequest -Uri "http://127.0.0.1:$FrontendPort/" -TimeoutSec 10 -UseBasicParsing
if ($landing.StatusCode -ne 200 -or $landing.Content -notmatch '<div id="root">') {
    throw 'Frontend smoke failed.'
}

$statusBody = '{"trackNumber":"\u041e\u0422\u041a-RUSH-DEFG"}'
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(10)
try {
    $content = [System.Net.Http.StringContent]::new($statusBody, [System.Text.Encoding]::UTF8, 'application/json')
    $statusResponse = $http.PostAsync(
        "http://127.0.0.1:$ApiPort/api/public/appeals/status",
        $content
    ).GetAwaiter().GetResult()
    $statusResponse.EnsureSuccessStatusCode() | Out-Null
    $statusJson = $statusResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $status = $statusJson | ConvertFrom-Json
}
finally {
    $http.Dispose()
}
if (-not $status.needsImmediateHelp -or $status.crisisSupport.Count -lt 2) {
    throw 'Seeded crisis scenario smoke failed.'
}

Write-Host "Smoke passed: frontend 200, API Healthy, seeded anonymous crisis route available."
