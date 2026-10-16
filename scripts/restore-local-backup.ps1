param([Parameter(Mandatory = $true)][string]$BackupDirectory, [string]$DeletionLedgerPath)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backupRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/backups')).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$backupPath = [IO.Path]::GetFullPath($BackupDirectory)
if (-not $backupPath.StartsWith($backupRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Restore accepts only a managed local backup under .local/backups.' }
$backupItem = Get-Item -LiteralPath $backupPath -ErrorAction Stop
if (-not $backupItem.PSIsContainer -or ($backupItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Backup path must be a real managed directory, not a reparse point.' }
if (Get-ChildItem -LiteralPath $backupPath -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Backup contains a reparse point; refusing to follow it.' }
$manifestPath = Join-Path $backupPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Backup manifest is missing.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.FormatVersion -ne 1 -or $manifest.Database -ne 'careergap' -or $manifest.DumpFile -ne 'database.dump') { throw 'Unsupported or invalid local backup manifest.' }
$dumpPath = Join-Path $backupPath $manifest.DumpFile
if ((Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash -ne $manifest.DumpSha256) { throw 'Database dump checksum does not match the manifest.' }
$selectedLedger = if ($DeletionLedgerPath) { [IO.Path]::GetFullPath($DeletionLedgerPath) } else { Join-Path $backupPath $manifest.DeletionLedgerFile }
if (-not (Test-Path -LiteralPath $selectedLedger)) { throw 'A deletion ledger is required before a restored database can be opened.' }
$ledgerEntries = @(Get-Content -LiteralPath $selectedLedger -Raw | ConvertFrom-Json)

Push-Location $projectRoot
try {
    . ./scripts/dev-env.ps1
    if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Restore is local-development only.' }
    $connection = @{}
    foreach ($part in $env:ConnectionStrings__Database.Split(';')) {
        $pair = $part.Split('=', 2)
        if ($pair.Count -eq 2) { $connection[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim() }
    }
    if ($connection['host'] -notin @('localhost', '127.0.0.1', '::1') -or
        $connection['port'] -ne '55432' -or $connection['database'] -ne 'careergap' -or $connection['username'] -ne 'careergap') {
        throw 'Restore requires the local careergap PostgreSQL container on port 55432.'
    }
    $container = ((& docker compose ps --status running -q db) -join '').Trim()
    if ($LASTEXITCODE -ne 0 -or -not $container) { throw 'Start the local PostgreSQL container before restoring.' }

    $restoreId = [Guid]::NewGuid().ToString('N')
    $restoreDatabase = "careergap_restore_$restoreId"
    $restoreRoot = Join-Path $projectRoot ".local/restore/$restoreId"
    $restoreFiles = Join-Path $restoreRoot 'files'
    [IO.Directory]::CreateDirectory($restoreFiles) | Out-Null
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = Get-Acl -LiteralPath $restoreRoot
    $requiredSids = @($identity.Value, 'S-1-5-18', 'S-1-5-32-544')
    $currentRules = @($acl.Access | Where-Object { $_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow })
    $alreadyProtected = $acl.AreAccessRulesProtected -and @($acl.Access).Count -eq $requiredSids.Count -and $currentRules.Count -eq $requiredSids.Count -and
        @($currentRules | Where-Object { ($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -eq [Security.AccessControl.FileSystemRights]::FullControl }).Count -eq $requiredSids.Count -and
        @($currentRules | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } | Where-Object { $_ -in $requiredSids }).Count -eq $requiredSids.Count
    if (-not $alreadyProtected) {
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($rule in @($acl.Access)) { $acl.RemoveAccessRuleSpecific($rule) }
        foreach ($sidValue in $requiredSids) {
            $sid = [Security.Principal.SecurityIdentifier]::new($sidValue)
            $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, [Security.AccessControl.FileSystemRights]::FullControl,
                [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit', [Security.AccessControl.PropagationFlags]::None,
                [Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($rule)
        }
        Set-Acl -LiteralPath $restoreRoot -AclObject $acl
    }

    $objectsSource = Join-Path $backupPath $manifest.ObjectDirectory
    foreach ($object in $manifest.ObjectFiles) {
        if ($object.Key -notmatch '^[0-9a-fA-F]{32}$') { throw 'Object manifest contains an invalid local storage key.' }
        $source = Join-Path $objectsSource $object.Key
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'A backed-up object is missing.' }
        if ((Get-Item -LiteralPath $source).Length -ne $object.Length -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $object.Sha256) {
            throw 'A backed-up object checksum does not match the manifest.'
        }
        Copy-Item -LiteralPath $source -Destination $restoreFiles
    }

    $containerDump = "/tmp/careergap-restore-$restoreId.dump"
    try {
        & docker compose exec -T db createdb -U careergap $restoreDatabase
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the isolated restore database.' }
        & docker cp $dumpPath "$($container):$containerDump"
        if ($LASTEXITCODE -ne 0) { throw 'Could not copy the dump to the local PostgreSQL container.' }
        & docker compose exec -T db pg_restore -U careergap -d $restoreDatabase --exit-on-error --no-owner --no-privileges $containerDump
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL restore failed; the database remains isolated.' }
    } finally {
        & docker compose exec -T db rm -f $containerDump *> $null
    }

    $originalConnection = $env:ConnectionStrings__Database
    $originalStorageProvider = $env:Storage__Provider
    $originalStorageRoot = $env:Storage__Root
    try {
        $databasePattern = [regex]::new('(?i)(Database|Initial Catalog)\s*=\s*[^;]*')
        $env:ConnectionStrings__Database = $databasePattern.Replace($originalConnection, "Database=$restoreDatabase", 1)
        $env:Storage__Provider = 'Local'
        $env:Storage__Root = $restoreFiles
        & dotnet ef database update --project backend/CareerGap.Api
        if ($LASTEXITCODE -ne 0) { throw 'Could not bring the isolated restore database to the current schema.' }
        & dotnet run --no-restore --project backend/CareerGap.Api -- --replay-deletion-ledger $selectedLedger
        if ($LASTEXITCODE -ne 0) { throw 'Deletion ledger replay failed; keep this database isolated and do not open the API.' }
    } finally {
        $env:ConnectionStrings__Database = $originalConnection
        if ($null -eq $originalStorageProvider) { Remove-Item Env:Storage__Provider -ErrorAction SilentlyContinue } else { $env:Storage__Provider = $originalStorageProvider }
        if ($null -eq $originalStorageRoot) { Remove-Item Env:Storage__Root -ErrorAction SilentlyContinue } else { $env:Storage__Root = $originalStorageRoot }
    }

    $dpCount = [int]((& docker compose exec -T db psql -X -qAt -U careergap -d $restoreDatabase -c 'SELECT count(*) FROM "DataProtectionKeys"') -join '').Trim()
    if ($LASTEXITCODE -ne 0 -or $dpCount -ne [int]$manifest.DataProtectionKeyCount) { throw 'Restored Data Protection keys do not match the backup manifest.' }
    $objectCount = @(Get-ChildItem -LiteralPath $restoreFiles -File -Force).Count
    if ($objectCount -gt @($manifest.ObjectFiles).Count) { throw 'Restore produced an unexpected object count.' }
    [IO.File]::WriteAllText((Join-Path $restoreRoot 'RESTORE-ISOLATED.txt'), "Database: $restoreDatabase`r`nAPI was not started. Keep this restore isolated until manually reviewed.`r`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Isolated restore completed: $restoreDatabase"
    Write-Output "Data Protection keys verified: $dpCount; remaining local objects after deletion replay: $objectCount. API was not started."
} finally { Pop-Location }
