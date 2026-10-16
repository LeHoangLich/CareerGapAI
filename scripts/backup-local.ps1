param([switch]$OfflineConfirmed)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OfflineConfirmed) { throw 'Stop the local API/worker, then rerun with -OfflineConfirmed so the database and objects are a consistent maintenance snapshot.' }
Push-Location $projectRoot
try {
    . ./scripts/dev-env.ps1
    if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Local backup only supports the Development environment.' }
    $connection = @{}
    foreach ($part in $env:ConnectionStrings__Database.Split(';')) {
        $pair = $part.Split('=', 2)
        if ($pair.Count -eq 2) { $connection[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim() }
    }
    if ($connection['host'] -notin @('localhost', '127.0.0.1', '::1') -or
        $connection['port'] -ne '55432' -or $connection['database'] -ne 'careergap' -or $connection['username'] -ne 'careergap') {
        throw 'Backup is restricted to the local careergap PostgreSQL database on port 55432.'
    }

    $settingsPath = Join-Path $projectRoot 'backend/CareerGap.Api/appsettings.Local.json'
    $settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json } else { $null }
    $provider = $env:Storage__Provider
    if (-not $provider -and $settings) { $provider = $settings.Storage.Provider }
    if ($provider -eq 'S3') { throw 'Local backup will not access external object storage.' }
    if ($env:DataProtection__CertificatePath -or ($settings -and $settings.DataProtection.CertificatePath)) {
        throw 'A configured Data Protection certificate needs a separate protected backup procedure; this local script does not copy it.'
    }
    $storageSetting = $env:Storage__Root
    if (-not $storageSetting -and $settings) { $storageSetting = $settings.Storage.Root }
    if (-not $storageSetting) { $storageSetting = '../../.local/files' }
    $storageRoot = [IO.Path]::GetFullPath((Join-Path (Join-Path $projectRoot 'backend/CareerGap.Api') $storageSetting))

    $backupRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/backups'))
    $storageRootPrefix = $storageRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $backupRootPrefix = $backupRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($storageRootPrefix.StartsWith($backupRootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $backupRootPrefix.StartsWith($storageRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Storage and backup directories must not contain one another.'
    }
    [IO.Directory]::CreateDirectory($backupRoot) | Out-Null
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = Get-Acl -LiteralPath $backupRoot
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
        Set-Acl -LiteralPath $backupRoot -AclObject $acl
    }

    $running = (& docker compose ps --status running -q db).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $running) { throw 'Start the local PostgreSQL container before taking a backup.' }
    $backupId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
    $destination = Join-Path $backupRoot $backupId
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    $objectDestination = Join-Path $destination 'objects'
    [IO.Directory]::CreateDirectory($objectDestination) | Out-Null
    if (Test-Path -LiteralPath $storageRoot) {
        $sourceFiles = @(Get-ChildItem -LiteralPath $storageRoot -File -Force -Recurse)
        if ($sourceFiles | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Object storage contains a reparse point; refusing to follow it.' }
        if (Get-ChildItem -LiteralPath $storageRoot -Directory -Force) { throw 'Local object keys are expected at the storage root; refusing an unexpected directory tree.' }
        foreach ($file in $sourceFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination $objectDestination -Force
        }
    } else { $sourceFiles = @() }

    $dumpName = "careergap-$backupId.dump"
    $containerDump = "/tmp/$dumpName"
    try {
        & docker compose exec -T db pg_dump -U careergap -d careergap -Fc -f $containerDump
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL dump failed.' }
        & docker cp "$($running):$containerDump" (Join-Path $destination 'database.dump')
        if ($LASTEXITCODE -ne 0) { throw 'Could not copy the database dump out of the local PostgreSQL container.' }
    } finally {
        & docker compose exec -T db rm -f $containerDump *> $null
    }

    $hasLedger = (& docker compose exec -T db psql -X -qAt -U careergap -d careergap -c "SELECT to_regclass('public.account_deletion_requests') IS NOT NULL").Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the local deletion ledger.' }
    $ledgerPath = Join-Path $destination 'deletion-ledger.json'
    if ($hasLedger -eq 't') {
        & dotnet run --no-restore --project backend/CareerGap.Api -- --export-deletion-ledger $ledgerPath
        if ($LASTEXITCODE -ne 0) { throw 'Could not export the minimal deletion ledger.' }
    } else {
        [IO.File]::WriteAllText($ledgerPath, '[]', [Text.UTF8Encoding]::new($false))
    }

    $dpCount = [int]((& docker compose exec -T db psql -X -qAt -U careergap -d careergap -c 'SELECT count(*) FROM "DataProtectionKeys"').Trim())
    if ($LASTEXITCODE -ne 0) { throw 'Could not verify the Data Protection key count.' }
    $ledger = Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
    $objectManifest = @($sourceFiles | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Key = $_.Name; Length = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $manifest = [ordered]@{
        FormatVersion = 1
        BackupId = $backupId
        CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Database = 'careergap'
        DumpFile = 'database.dump'
        DumpSha256 = (Get-FileHash -LiteralPath (Join-Path $destination 'database.dump') -Algorithm SHA256).Hash
        ObjectDirectory = 'objects'
        ObjectFiles = $objectManifest
        DataProtectionKeyCount = $dpCount
        DataProtectionKeyLocation = 'PostgreSQL DataProtectionKeys table in database.dump'
        DeletionLedgerFile = 'deletion-ledger.json'
        DeletionLedgerEntryCount = @($ledger).Count
        Scope = 'local development only; no external services'
    }
    [IO.File]::WriteAllText((Join-Path $destination 'manifest.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Output "Local backup created: $destination"
    Write-Output "Database dump SHA-256 verified; object files: $($objectManifest.Count); Data Protection keys: $dpCount; deletion ledger rows: $(@($ledger).Count)."
} finally { Pop-Location }
