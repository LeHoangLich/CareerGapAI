# Dot-source from repository root: . ./scripts/dev-env.ps1
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath "$taskRoot/.env")) { throw 'Copy .env.example to .env and choose a local password first.' }
foreach ($taskLine in Get-Content -LiteralPath "$taskRoot/.env") {
    if ($taskLine -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = "$taskRoot/.local/dotnet"
# Never print environment contents or connection strings.
