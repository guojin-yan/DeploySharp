[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputJson,
    [Parameter(Mandatory = $true)]
    [string]$OutputMarkdown,
    [int]$MinimumRecords = 200,
    [double]$MaximumFalseCorrectionRate = 0,
    [double]$MaximumP95Milliseconds = 250
)

$ErrorActionPreference = 'Stop'
if ($MinimumRecords -lt 1) { throw 'MinimumRecords must be positive.' }
if ($MaximumFalseCorrectionRate -lt 0 -or $MaximumFalseCorrectionRate -gt 1) { throw 'MaximumFalseCorrectionRate must be in [0,1].' }
if ($MaximumP95Milliseconds -le 0) { throw 'MaximumP95Milliseconds must be positive.' }

function Resolve-Input([string]$path) {
    $resolved = (Resolve-Path -LiteralPath $path -ErrorAction Stop).Path
    $value = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    if ($null -eq $value.backends) { throw "Expected a backends array in $resolved" }
    return [pscustomobject]@{ Path = $resolved; Value = $value }
}

$evidence = foreach ($path in $InputPath) { Resolve-Input $path }
$backends = @()
foreach ($item in $evidence) {
    foreach ($backend in @($item.Value.backends)) {
        if ($null -eq $backend.summary) { throw "Missing summary for backend in $($item.Path)" }
        $summary = $backend.summary
        $records = [int]$summary.records
        $cerRate = if ($null -eq $summary.selectedFalseCorrectionRateByCer) { $null } else { [double]$summary.selectedFalseCorrectionRateByCer }
        $werRate = if ($null -eq $summary.selectedFalseCorrectionRateByWer) { $null } else { [double]$summary.selectedFalseCorrectionRateByWer }
        $p95 = if ($null -eq $summary.latencyMs.all.p95) { $null } else { [double]$summary.latencyMs.all.p95 }
        $checks = [ordered]@{
            minimumRecords = $records -ge $MinimumRecords
            cerFalseCorrection = $null -ne $cerRate -and $cerRate -le $MaximumFalseCorrectionRate
            werFalseCorrection = $null -ne $werRate -and $werRate -le $MaximumFalseCorrectionRate
            p95Budget = $null -ne $p95 -and $p95 -le $MaximumP95Milliseconds
        }
        $backends += [ordered]@{
            backend = [string]$backend.backend
            evidenceId = [string]$backend.evidenceId
            source = [string]$backend.source
            records = $records
            candidateSelected = [int]$summary.candidateSelected
            falseCorrectionRateByCer = $cerRate
            falseCorrectionRateByWer = $werRate
            allP95Milliseconds = $p95
            checks = $checks
            status = if (($checks.Values | Where-Object { -not $_ }).Count -eq 0) { 'pass' } else { 'blocked' }
        }
    }
}
$status = if ($backends.Count -gt 0 -and (@($backends | Where-Object status -ne 'pass').Count -eq 0)) { 'pass' } else { 'blocked' }
$report = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-enhancement-safety-gate-v1'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    status = $status
    thresholds = [ordered]@{
        minimumRecords = $MinimumRecords
        maximumFalseCorrectionRate = $MaximumFalseCorrectionRate
        maximumP95Milliseconds = $MaximumP95Milliseconds
    }
    backends = $backends
    boundary = 'This is a fail-closed safety and cost gate over controlled parent-linked degraded-crop evidence. It is not page/line accuracy, robustness, or approval of a production enhancement default. A pass still requires an independent natural-quality split and business-field error budget.'
}
$jsonDirectory = Split-Path -Parent $OutputJson
$markdownDirectory = Split-Path -Parent $OutputMarkdown
if ($jsonDirectory) { New-Item -ItemType Directory -Force -Path $jsonDirectory | Out-Null }
if ($markdownDirectory) { New-Item -ItemType Directory -Force -Path $markdownDirectory | Out-Null }
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputJson -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# PaddleOCR enhancement safety gate')
$lines.Add('')
$lines.Add("Overall status: **$status**.")
$lines.Add('')
$lines.Add("Thresholds: records >= $MinimumRecords; CER/WER false-correction rate <= $MaximumFalseCorrectionRate; all-run P95 <= $MaximumP95Milliseconds ms.")
$lines.Add('')
$lines.Add('| Backend | Records | Selected | CER false-correction | WER false-correction | All P95 ms | Status |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | --- |')
foreach ($backend in $backends) {
    $cer = if ($null -eq $backend.falseCorrectionRateByCer) { '-' } else { '{0:P2}' -f $backend.falseCorrectionRateByCer }
    $wer = if ($null -eq $backend.falseCorrectionRateByWer) { '-' } else { '{0:P2}' -f $backend.falseCorrectionRateByWer }
    $p95 = if ($null -eq $backend.allP95Milliseconds) { '-' } else { '{0:N2}' -f $backend.allP95Milliseconds }
    $lines.Add("| $($backend.backend) | $($backend.records) | $($backend.candidateSelected) | $cer | $wer | $p95 | $($backend.status) |")
}
$lines.Add('')
$lines.Add('The current production default remains disabled. The controlled FUNSD evidence contains selected CER-worse candidates, so this gate is expected to remain blocked until an independent natural-quality split, business-field error budget, and a separately calibrated latency budget are available.')
$lines.Add('')
$lines.Add($report.boundary)
Set-Content -LiteralPath $OutputMarkdown -Value ($lines -join "`n") -Encoding UTF8
Write-Output (([ordered]@{ status = $status; json = $OutputJson; markdown = $OutputMarkdown; backends = $backends.Count } | ConvertTo-Json -Compress))
