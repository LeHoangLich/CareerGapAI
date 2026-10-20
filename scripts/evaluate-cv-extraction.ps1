param([string]$Dataset = '.local/evaluation/cv-extraction-fixtures/dataset.json', [string]$Output = '.local/evaluation/cv-extraction-smoke.json')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $taskOutput = [IO.Path]::GetFullPath($Output)
    $taskPrivateRoot = [IO.Path]::GetFullPath((Join-Path $taskRoot '.local')) + [IO.Path]::DirectorySeparatorChar
    if (-not $taskOutput.StartsWith($taskPrivateRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evaluation output must stay under .local' }
    dotnet run --no-build --project backend/CareerGap.Api --no-launch-profile -- --evaluate-cv-extraction ([IO.Path]::GetFullPath($Dataset)) $taskOutput
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic extraction smoke failed' }
} finally { Pop-Location }
