[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReportPath,
    [string]$EnvironmentPath,
    [switch]$RequireSourceRevision,
    [switch]$RequireTensorRtRuntime,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$requiredColumns = @(
    'version', 'variant', 'backend', 'device', 'status',
    'selected_batch_size', 'selected_inference_channels',
    'preprocess_ms', 'detection_inference_ms', 'detection_postprocess_ms',
    'crop_ms', 'orientation_ms', 'recognition_prepare_work_ms',
    'recognition_inference_work_ms', 'recognition_postprocess_work_ms',
    'total_ms', 'total_p50_ms', 'total_p95_ms', 'regions',
    'result_text_sha256', 'result_contract_sha256', 'image_path'
)

function Fail-Report([string]$message) {
    throw "PaddleOCR formal benchmark report is invalid: $message"
}

$report = (Resolve-Path -LiteralPath $ReportPath -ErrorAction Stop).Path
$environment = if ([string]::IsNullOrWhiteSpace($EnvironmentPath)) { "$report.environment.json" } else { (Resolve-Path -LiteralPath $EnvironmentPath -ErrorAction Stop).Path }
if (-not (Test-Path -LiteralPath $environment -PathType Leaf)) { Fail-Report "environment metadata is missing: $environment" }

$rows = @(Import-Csv -LiteralPath $report)
if ($rows.Count -eq 0) { Fail-Report 'the CSV contains no rows' }
$columns = @($rows[0].PSObject.Properties.Name)
foreach ($column in $requiredColumns) {
    if ($column -notin $columns) { Fail-Report "required column '$column' is missing" }
}

$metadata = Get-Content -LiteralPath $environment -Raw | ConvertFrom-Json
$protocol = $metadata.protocol
if ($null -eq $protocol) { Fail-Report 'protocol metadata is missing' }
if ([int]$protocol.warmup -ne 5 -or [int]$protocol.iterations -ne 50) {
    Fail-Report "expected warmup=5 and iterations=50, found warmup=$($protocol.warmup), iterations=$($protocol.iterations)"
}
if ($metadata.protocolName -and $metadata.protocolName -ne 'deploysharp-paddleocr-full-5-50-v1') {
    Fail-Report "unknown protocolName '$($metadata.protocolName)'"
}
if ([string]::IsNullOrWhiteSpace([string]$metadata.imageSha256) -or [string]$metadata.imageSha256 -notmatch '^[0-9a-fA-F]{64}$') {
    Fail-Report 'imageSha256 is missing or not a SHA-256 digest'
}
if ($RequireSourceRevision -and ([string]::IsNullOrWhiteSpace([string]$metadata.sourceRevision) -or $metadata.sourceRevision -eq 'unrecorded')) {
    Fail-Report 'sourceRevision is missing or unrecorded'
}

$checked = 0
$passed = 0
$nonPass = 0
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($row in $rows) {
    $checked++
    $status = ([string]$row.status).ToLowerInvariant()
    if ($status -ne 'pass') { $nonPass++; continue }
    $passed++
    foreach ($digestColumn in @('result_text_sha256', 'result_contract_sha256')) {
        if ([string]$row.$digestColumn -notmatch '^[0-9a-fA-F]{64}$') { $failures.Add("$($row.version)/$($row.variant)/$($row.backend): $digestColumn is not a SHA-256 digest") }
    }
    # Newer benchmark rows expose a discrete semantic fingerprint and the
    # number of strict (floating-point-inclusive) contract variants. Keep old
    # reports readable, but validate both fields whenever they are present.
    if ('result_semantic_contract_sha256' -in $columns -and [string]$row.result_semantic_contract_sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        $failures.Add("$($row.version)/$($row.variant)/$($row.backend): result_semantic_contract_sha256 is not a SHA-256 digest")
    }
    if ('result_contract_variants' -in $columns) {
        $variants = 0
        if (-not [int]::TryParse([string]$row.result_contract_variants, [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$variants) -or $variants -lt 1) {
            $failures.Add("$($row.version)/$($row.variant)/$($row.backend): result_contract_variants must be a positive integer")
        }
    }
    if ('result_numeric_max_abs_drift' -in $columns) {
        $drift = 0.0
        if (-not [double]::TryParse([string]$row.result_numeric_max_abs_drift, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$drift) -or [double]::IsNaN($drift) -or [double]::IsInfinity($drift) -or $drift -lt 0) {
            $failures.Add("$($row.version)/$($row.variant)/$($row.backend): result_numeric_max_abs_drift must be a non-negative finite number")
        }
    }
    foreach ($numberColumn in @('preprocess_ms', 'detection_inference_ms', 'detection_postprocess_ms', 'crop_ms', 'orientation_ms', 'recognition_prepare_work_ms', 'recognition_inference_work_ms', 'recognition_postprocess_work_ms', 'total_ms', 'total_p50_ms', 'total_p95_ms')) {
        $number = 0.0
        if (-not [double]::TryParse([string]$row.$numberColumn, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number) -or [double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt 0) {
            $failures.Add("$($row.version)/$($row.variant)/$($row.backend): $numberColumn is not a non-negative finite number")
        }
    }
    $p50 = [double]::Parse([string]$row.total_p50_ms, [Globalization.CultureInfo]::InvariantCulture)
    $p95 = [double]::Parse([string]$row.total_p95_ms, [Globalization.CultureInfo]::InvariantCulture)
    if ($p95 -lt $p50) { $failures.Add("$($row.version)/$($row.variant)/$($row.backend): total_p95_ms is lower than total_p50_ms") }
    if ([string]::IsNullOrWhiteSpace([string]$row.image_path)) { $failures.Add("$($row.version)/$($row.variant)/$($row.backend): image_path is empty") }
}
if ($passed -eq 0) { Fail-Report 'the report has no pass rows' }

$tensorRows = @($rows | Where-Object { $_.backend -eq 'tensorrt' -and $_.status -eq 'pass' })
if ($RequireTensorRtRuntime -and $tensorRows.Count -gt 0) {
    $runtime = $metadata.runtime
    foreach ($property in @('tensorRtRoot', 'bridge')) {
        if ($null -eq $runtime -or [string]::IsNullOrWhiteSpace([string]$runtime.$property)) { $failures.Add("TensorRT pass rows require runtime.$property") }
    }
    if ([string]::IsNullOrWhiteSpace([string]$metadata.tensorRtApiVersion) -and [string]::IsNullOrWhiteSpace([string]$protocol.tensorRtApiVersion)) { $failures.Add('TensorRT pass rows require a TensorRT API version') }
}

$modelRecords = @($metadata.models)
if ($modelRecords.Count -eq 0) { $failures.Add('model SHA records are missing from metadata') }
foreach ($model in $modelRecords) {
    if ([string]$model.onnxSha256 -notmatch '^[0-9a-fA-F]{64}$') { $failures.Add("model $($model.version)/$($model.variant)/$($model.role) is missing onnxSha256") }
    if ($model.enginePath -and [string]$model.engineSha256 -notmatch '^[0-9a-fA-F]{64}$') { $failures.Add("model $($model.version)/$($model.variant)/$($model.role) has an invalid engineSha256") }
}
if ($failures.Count -gt 0) { Fail-Report ($failures -join '; ') }

$result = [ordered]@{
    schemaVersion = 1
    protocolName = 'deploysharp-paddleocr-full-5-50-v1'
    report = $report
    environment = $environment
    rows = $checked
    passRows = $passed
    nonPassRows = $nonPass
    tensorRtPassRows = $tensorRows.Count
    validatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    status = 'formal-ready'
}
$json = $result | ConvertTo-Json -Depth 6
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $output = [IO.Path]::GetFullPath($OutputPath)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    Set-Content -LiteralPath $output -Value $json -Encoding utf8
}
Write-Output $json
