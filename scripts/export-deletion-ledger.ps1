param([string]$OutputPath)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot '.local/backups/deletion-ledger-current.json' }
$output = [IO.Path]::GetFullPath($OutputPath)
$managedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/backups')).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($managedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Deletion ledger exports must stay under the protected .local/backups directory.' }

Push-Location $projectRoot
try {
    . ./scripts/dev-env.ps1
    if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Deletion ledger export is local-development only.' }
    $backupRoot = [IO.Directory]::CreateDirectory((Split-Path $output -Parent)).FullName
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
    & dotnet run --no-restore --project backend/CareerGap.Api -- --export-deletion-ledger $output
    if ($LASTEXITCODE -ne 0) { throw 'Could not export the local account-deletion ledger.' }
} finally { Pop-Location }
