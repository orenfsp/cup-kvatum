[CmdletBinding()]
param(
    [switch]$ConfirmDataLoss,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ProjectName = 'otklik',
    [ValidateRange(1, 65535)]
    [int]$FrontendPort = 3000,
    [ValidateRange(1, 65535)]
    [int]$ApiPort = 8080
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $ConfirmDataLoss) {
    throw 'Reset deletes only this Compose project volumes, including appeals and attachments. Re-run with -ConfirmDataLoss.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$composePath = Join-Path $repositoryRoot 'docker-compose.yml'
$startScript = Join-Path $PSScriptRoot 'start-demo.ps1'
if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Refusing reset: docker-compose.yml not found at $composePath"
}
if ($repositoryRoot -eq [System.IO.Path]::GetPathRoot($repositoryRoot)) {
    throw "Refusing reset from filesystem root: $repositoryRoot"
}

Write-Host "Resetting the explicitly selected Compose project '$ProjectName' in $repositoryRoot"
Push-Location $repositoryRoot
try {
    & docker compose -p $ProjectName down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose reset failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

& $startScript -ProjectName $ProjectName -FrontendPort $FrontendPort -ApiPort $ApiPort
