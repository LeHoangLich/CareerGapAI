param([Parameter(Mandatory = $true)][string]$DeletionLedgerPath, [DateTimeOffset]$Now = [DateTimeOffset]::UtcNow)

$ErrorActionPreference = 'Stop'
function Convert-ToUtc([object]$value) {
    if ($value -is [DateTimeOffset]) { return $value.ToUniversalTime() }
    if ($value -is [DateTime]) { return ([DateTimeOffset]$value).ToUniversalTime() }
    return [DateTimeOffset]::Parse([string]$value, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
}
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backupRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/backups')).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$ledger = @(Get-Content -LiteralPath $DeletionLedgerPath -Raw | ConvertFrom-Json)
foreach ($entry in $ledger) {
    if ($entry.Status -eq 'purged' -and -not $entry.DataPurgedAt) { throw 'Invalid deletion ledger; a purged row is missing its purge time.' }
}
$expiredDeadlines = @($ledger | Where-Object { $_.Status -eq 'purged' -and $_.DataPurgedAt } | ForEach-Object {
    (Convert-ToUtc $_.DataPurgedAt).AddDays(14)
} | Where-Object { $_ -le $Now.ToUniversalTime() })
if ($expiredDeadlines.Count -eq 0) { Write-Output 'No local backup has reached a deletion-ledger expiry deadline.'; return }
if (-not (Test-Path -LiteralPath $backupRoot)) { return }

foreach ($directory in Get-ChildItem -LiteralPath $backupRoot -Directory -Force) {
    if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Backup root contains a reparse point; refusing recursive cleanup.' }
    $manifestPath = Join-Path $directory.FullName 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { continue }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.FormatVersion -ne 1 -or -not $manifest.CreatedAtUtc) { throw 'Refusing to expire an unrecognized backup directory.' }
    $createdAt = Convert-ToUtc $manifest.CreatedAtUtc
    $deadline = $expiredDeadlines | Where-Object { $createdAt -lt $_ } | Select-Object -First 1
    if (-not $deadline) { continue }

    $resolved = [IO.Path]::GetFullPath($directory.FullName)
    if (-not $resolved.StartsWith($backupRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup expiry target escaped the managed backup directory.' }
    if (Get-ChildItem -LiteralPath $resolved -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw 'Backup contains a reparse point; refusing recursive cleanup.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Write-Output "Expired managed backup: $($directory.Name)"
}
