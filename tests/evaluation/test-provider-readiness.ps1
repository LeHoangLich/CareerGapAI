# Purely fabricated declarations to test an OFFLINE calculator; no real permission/key/call.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskScript = Join-Path $taskRoot 'scripts/check-deepseek-readiness.ps1'
$taskTemporary = Join-Path $taskRoot ('.local/evaluation/provider-selftest-' + [Guid]::NewGuid().ToString('N') + '.json')
New-Item -ItemType Directory -Force (Split-Path $taskTemporary -Parent) | Out-Null
$taskPassed = 0
function Assert-Readiness($Plan, [bool]$Expected, [string]$Blocker) {
    $Plan | ConvertTo-Json | Set-Content -LiteralPath $taskTemporary
    $taskResultText = & $taskScript -Plan $taskTemporary
    $taskExitCode = $LASTEXITCODE
    $taskResult = $taskResultText | ConvertFrom-Json
    if ($taskResult.readyForManualProbe -ne $Expected -or $taskResult.providerCalled -ne $false -or ($Expected -and $taskExitCode -ne 0) -or (-not $Expected -and $taskExitCode -ne 1)) { throw 'Readiness gate result mismatch' }
    if ($Blocker -and $Blocker -notin $taskResult.blockers) { throw 'Expected blocker missing' }
    if ($Expected -and [decimal]$taskResult.requiredIsolatedProbeBackendLimitUsd -gt [decimal]$Plan.authorizedProbeCapUsd) { throw 'Probe backend limit exceeds authorized total cap' }
    $taskExpectedPerAttempt = [decimal]::Ceiling((([decimal]$Plan.maxInputTokens * [decimal]$Plan.inputUsdPerMillion + [decimal]$Plan.maxOutputTokens * [decimal]$Plan.outputUsdPerMillion) / [decimal]1000000) * [decimal]100000000) / [decimal]100000000
    if ($Plan.inputUsdPerMillion -gt 0 -and $Plan.outputUsdPerMillion -gt 0 -and $taskResult.estimateUsdIncludingRetry -ne $taskExpectedPerAttempt * $Plan.maximumAttempts) { throw 'Decimal reservation mismatch' }
    $script:taskPassed++
}
try {
    $taskBase = Get-Content -Raw (Join-Path $taskRoot 'project-docs/evaluation/deepseek-probe-plan.json') | ConvertFrom-Json -AsHashtable
    Assert-Readiness $taskBase $false 'paid_probe_not_authorized'
    $taskReady = $taskBase.Clone()
    $taskReady.backendKeyConfigured = $true
    $taskReady.paidProbeAuthorized = $true
    $taskReady.inputRightsVerified = $true
    $taskReady.projectRemainingUsd = 1
    $taskReady.providerAvailableUsd = 1
    $taskReady.backendMonthlyLimitUsd = 1
    $taskReady.authorizedProbeCapUsd = [decimal]0.02
    $taskReady.proposedModel = 'synthetic-test-model-not-a-recommendation'
    $taskReady.inputUsdPerMillion = [decimal]0.30
    $taskReady.outputUsdPerMillion = [decimal]1.20
    $taskReady.pricingVerifiedAt = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
    $taskReady.periodStartUtc = [DateTimeOffset]::UtcNow.AddDays(-1).ToString('o')
    $taskReady.periodEndUtc = [DateTimeOffset]::UtcNow.AddDays(1).ToString('o')
    Assert-Readiness $taskReady $true ''
    $taskCase = $taskReady.Clone(); $taskCase.paidProbeAuthorized = $false
    Assert-Readiness $taskCase $false 'paid_probe_not_authorized'
    $taskCase = $taskReady.Clone(); $taskCase.projectRemainingUsd = [decimal]0.001
    Assert-Readiness $taskCase $false 'reservation_exceeds_available_budget'
    $taskCase = $taskReady.Clone(); $taskCase.pricingVerifiedAt = [DateTimeOffset]::UtcNow.AddDays(-32).ToString('o')
    Assert-Readiness $taskCase $false 'pricing_not_current'
    $taskCase = $taskReady.Clone(); $taskCase.periodEndUtc = $null
    Assert-Readiness $taskCase $false 'provider_period_unknown_or_invalid'
    $taskCase = $taskReady.Clone(); $taskCase.maxInputTokens = 32001
    Assert-Readiness $taskCase $false 'token_or_attempt_limit_invalid'
    $taskCase = $taskReady.Clone(); $taskCase.maximumAttempts = 3
    Assert-Readiness $taskCase $false 'token_or_attempt_limit_invalid'
    $taskCase = $taskReady.Clone(); $taskCase.backendMonthlyLimitUsd = [decimal]0.001
    Assert-Readiness $taskCase $false 'reservation_exceeds_backend_limit'
    $taskCase = $taskReady.Clone(); $taskCase.maximumAttempts = 1
    Assert-Readiness $taskCase $false 'token_or_attempt_limit_invalid'
    $taskCase = $taskReady.Clone(); $taskCase.contract = 'skills-v1'
    Assert-Readiness $taskCase $false 'full_cv_contract_plan_required'
    $taskCase = $taskReady.Clone(); $taskCase.authorizedProbeCapUsd = [decimal]0.001
    Assert-Readiness $taskCase $false 'reservation_exceeds_authorized_cap'
    Write-Output "$taskPassed offline provider readiness cases passed; no provider calls."
} finally {
    if (Test-Path -LiteralPath $taskTemporary -PathType Leaf) { Remove-Item -LiteralPath $taskTemporary }
}
