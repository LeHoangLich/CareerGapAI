param(
    [string]$Dataset = 'project-docs/evaluation/matching-baseline-smoke.json',
    [string]$Output,
    [int]$TopK = 20
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $datasetPath = [System.IO.Path]::GetFullPath($Dataset)
    if (-not (Test-Path -LiteralPath $datasetPath -PathType Leaf)) { throw 'Evaluation dataset file was not found.' }
    if (-not $Output) {
        $outputDirectory = Join-Path $taskRoot '.local/evaluation'
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
        $Output = Join-Path $outputDirectory ("matching-{0}.json" -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
    }
    $outputPath = [System.IO.Path]::GetFullPath($Output)
    $outputParent = Split-Path -Parent $outputPath
    if ([string]::IsNullOrWhiteSpace($outputParent)) { throw 'Evaluation output must be an explicit file path.' }
    $privateOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $taskRoot '.local/evaluation')).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $outputPath.StartsWith($privateOutputRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Evaluation manifests must be written under .local/evaluation.'
    }
    New-Item -ItemType Directory -Force -Path $outputParent | Out-Null
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & dotnet run --project backend/CareerGap.Api --no-launch-profile -- --evaluate-keyword-baseline $datasetPath $outputPath $TopK
    if ($LASTEXITCODE -ne 0) { throw "Matching evaluation failed with exit code $LASTEXITCODE." }
    Write-Host "Run manifest written to $outputPath"
}
finally { Pop-Location }
