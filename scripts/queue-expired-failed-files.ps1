param(
    [Parameter(Mandatory = $true)][string]$ContainerName,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$DatabaseUser,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
# Local maintenance only. Dry run is the default; the existing worker performs object deletion.
if ($ContainerName -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,120}$' -or
    $DatabaseUser -notmatch '^[a-zA-Z0-9_]{1,63}$' -or
    ($Database -ne 'careergap' -and $Database -notmatch '^careergap_restore_[a-zA-Z0-9_]{1,40}$')) {
    throw 'Only a named local CareerGap database or isolated restore database is allowed.'
}
$metadataJson = & docker inspect $ContainerName
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the database container.' }
$metadata = @($metadataJson | ConvertFrom-Json)[0]
$bindings = @($metadata.NetworkSettings.Ports.'5432/tcp')
if (-not $metadata.State.Running -or $bindings.Count -eq 0 -or
    @($bindings | Where-Object { $_.HostIp -notin @('127.0.0.1', '::1') }).Count -gt 0) {
    throw 'Database must be running with PostgreSQL published exclusively on loopback.'
}
$predicate = @"
status = 'failed' AND created_at <= now() - interval '7 days'
AND NOT EXISTS (SELECT 1 FROM resume_versions rv WHERE rv.file_id = stored_files.id)
AND NOT EXISTS (SELECT 1 FROM candidate_public_resumes cpr WHERE cpr.redacted_file_id = stored_files.id)
"@
if (-not $Apply) {
    $query = "SELECT json_build_object('mode','dry_run','eligible',count(*),'retention_days_from_upload',7) FROM stored_files WHERE $predicate;"
} else {
    $query = @"
BEGIN;
WITH candidates AS (
 SELECT id FROM stored_files WHERE $predicate
 ORDER BY created_at,id LIMIT 100 FOR UPDATE SKIP LOCKED
), queued AS (
 UPDATE stored_files sf SET status='delete_pending', cleanup_next_attempt_at=NULL,
 cleanup_lease_until=NULL, cleanup_last_error_code=NULL
 FROM candidates c WHERE sf.id=c.id RETURNING sf.id
)
SELECT json_build_object('mode','applied','queued',count(*),'retention_days_from_upload',7) FROM queued;
COMMIT;
"@
}
$result = & docker exec $ContainerName psql -X -qAt -v ON_ERROR_STOP=1 -U $DatabaseUser -d $Database -c $query
if ($LASTEXITCODE -ne 0) { throw 'Failed-file maintenance transaction did not complete.' }
Write-Output $result
