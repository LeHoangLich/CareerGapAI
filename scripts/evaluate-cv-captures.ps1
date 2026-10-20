param([Parameter(Mandatory)][string]$Dataset, [Parameter(Mandatory)][string]$Captures,
    [Parameter(Mandatory)][string]$Output)
# Offline replay: deliberately does not source .env or start the API/worker.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    dotnet run --no-build --project backend/CareerGap.Api --no-launch-profile -- --evaluate-cv-captures ([IO.Path]::GetFullPath($Dataset)) ([IO.Path]::GetFullPath($Captures)) ([IO.Path]::GetFullPath($Output))
    if ($LASTEXITCODE -ne 0) { throw 'Offline capture evaluation rejected input or failed; no provider dispatch.' }
} finally { Pop-Location }
