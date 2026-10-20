param([string]$Plan = 'project-docs/evaluation/deepseek-probe-plan.json')

# Offline only: reads a NONSECRET plan, calculates reservation; no network/dispatch.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskPlanPath = if ([IO.Path]::IsPathRooted($Plan)) { $Plan } else { Join-Path $taskRoot $Plan }
$taskPlan = Get-Content -Raw -LiteralPath $taskPlanPath | ConvertFrom-Json
$taskBlocked = [Collections.Generic.List[string]]::new()
$taskNow = [DateTimeOffset]::UtcNow
if ($taskPlan.schemaVersion -ne 2 -or $taskPlan.contract -ne 'cv-fields-v3') { $taskBlocked.Add('full_cv_contract_plan_required') }
function Convert-TaskDateText($Value) {
    if ($Value -is [DateTime] -or $Value -is [DateTimeOffset]) { return $Value.ToString('o') }
    return [string]$Value
}
if ($taskPlan.backendKeyConfigured -ne $true) { $taskBlocked.Add('backend_key_not_configured') }
if ($taskPlan.paidProbeAuthorized -ne $true) { $taskBlocked.Add('paid_probe_not_authorized') }
if ($taskPlan.inputRightsVerified -ne $true) { $taskBlocked.Add('provider_input_rights_not_verified') }
if ($null -eq $taskPlan.projectRemainingUsd) { $taskBlocked.Add('project_remaining_budget_unknown') }
if ($null -eq $taskPlan.providerAvailableUsd) { $taskBlocked.Add('provider_balance_unknown') }
$taskPriceDate = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse((Convert-TaskDateText $taskPlan.pricingVerifiedAt), [ref]$taskPriceDate) -or $taskPriceDate -gt $taskNow -or $taskPriceDate -lt $taskNow.AddDays(-31)) { $taskBlocked.Add('pricing_not_current') }
$taskStart = [DateTimeOffset]::MinValue
$taskEnd = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse((Convert-TaskDateText $taskPlan.periodStartUtc), [ref]$taskStart) -or -not [DateTimeOffset]::TryParse((Convert-TaskDateText $taskPlan.periodEndUtc), [ref]$taskEnd) -or $taskStart -gt $taskNow -or $taskEnd -le $taskNow -or ($taskEnd - $taskStart).TotalDays -gt 32) { $taskBlocked.Add('provider_period_unknown_or_invalid') }
if ($taskPlan.maxInputTokens -lt 1024 -or $taskPlan.maxInputTokens -gt 32000 -or $taskPlan.maxOutputTokens -lt 128 -or $taskPlan.maxOutputTokens -gt 2048 -or $taskPlan.maximumAttempts -ne 2) { $taskBlocked.Add('token_or_attempt_limit_invalid') }
if ($taskPlan.inputUsdPerMillion -le 0 -or $taskPlan.outputUsdPerMillion -le 0 -or [string]::IsNullOrWhiteSpace($taskPlan.proposedModel)) { $taskBlocked.Add('model_or_price_missing') }
if ($null -eq $taskPlan.authorizedProbeCapUsd -or $taskPlan.authorizedProbeCapUsd -le 0) { $taskBlocked.Add('authorized_probe_cap_missing') }
$taskPerAttempt = [decimal]::Ceiling((([decimal]$taskPlan.maxInputTokens * [decimal]$taskPlan.inputUsdPerMillion + [decimal]$taskPlan.maxOutputTokens * [decimal]$taskPlan.outputUsdPerMillion) / [decimal]1000000) * [decimal]100000000) / [decimal]100000000
$taskEstimate = $taskPerAttempt * [int]$taskPlan.maximumAttempts
if ($null -ne $taskPlan.authorizedProbeCapUsd -and $taskEstimate -gt [decimal]$taskPlan.authorizedProbeCapUsd) { $taskBlocked.Add('reservation_exceeds_authorized_cap') }
foreach ($taskBudget in @($taskPlan.projectRemainingUsd, $taskPlan.providerAvailableUsd)) {
    if ($null -ne $taskBudget -and $taskEstimate -gt [decimal]$taskBudget) { $taskBlocked.Add('reservation_exceeds_available_budget') }
}
if ($null -eq $taskPlan.backendMonthlyLimitUsd -or $taskPlan.backendMonthlyLimitUsd -le 0 -or $taskPlan.backendMonthlyLimitUsd -gt 3 -or ($null -ne $taskPlan.projectRemainingUsd -and $taskPlan.backendMonthlyLimitUsd -gt $taskPlan.projectRemainingUsd)) { $taskBlocked.Add('backend_budget_limit_not_ready') }
if ($null -ne $taskPlan.backendMonthlyLimitUsd -and $taskEstimate -gt [decimal]$taskPlan.backendMonthlyLimitUsd) { $taskBlocked.Add('reservation_exceeds_backend_limit') }
$taskProbeBackendLimit = $null
if ($taskBlocked.Count -eq 0) {
    $taskProbeBackendLimit = [decimal]$taskPlan.backendMonthlyLimitUsd
    foreach ($taskAmount in @($taskPlan.authorizedProbeCapUsd, $taskPlan.projectRemainingUsd, $taskPlan.providerAvailableUsd)) {
        if ([decimal]$taskAmount -lt $taskProbeBackendLimit) { $taskProbeBackendLimit = [decimal]$taskAmount }
    }
}
[ordered]@{
    readyForManualProbe = ($taskBlocked.Count -eq 0)
    contract = $taskPlan.contract
    estimateUsdIncludingRetry = if ($taskPlan.inputUsdPerMillion -gt 0 -and $taskPlan.outputUsdPerMillion -gt 0) { $taskEstimate } else { $null }
    maximumAttempts = $taskPlan.maximumAttempts
    fullCvMaxOutputTokens = $taskPlan.maxOutputTokens
    requiredIsolatedProbeBackendLimitUsd = $taskProbeBackendLimit
    blockers = @($taskBlocked)
    providerCalled = $false
    note = 'Offline declarations only; key/rights/balance must be verified at execution. No permission is granted by this check.'
} | ConvertTo-Json -Depth 3
if ($taskBlocked.Count -gt 0) { exit 1 }
exit 0
