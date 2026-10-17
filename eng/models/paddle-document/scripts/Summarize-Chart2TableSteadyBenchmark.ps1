param(
    [string]$OrtEvidence = 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017-ort.json',
    [string]$OpenVinoEvidence = 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017-openvino.json',
    [string]$TensorRtEvidence = 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017-tensorrt.json',
    [string]$HarnessRevision = 'working-tree',
    [string]$JsonOutput = 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017.json',
    [string]$MarkdownOutput = 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path

function Read-Evidence([string]$relativePath, [string]$expectedBackend) {
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $relativePath))
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Evidence file not found: $relativePath" }
    $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($json.schemaVersion -ne 'deploysharp-chart2table-fixed-benchmark-v1') { throw "Unexpected schema in $relativePath" }
    if ($json.backend -ne $expectedBackend) { throw "Expected backend $expectedBackend in $relativePath; got $($json.backend)" }
    if ($json.protocol.warmupCount -ne 5 -or $json.protocol.measurementCount -ne 50) { throw "Evidence does not use 5+50 in $relativePath" }
    if ($json.resultContract.invariantAcrossAllRuns -ne $true) { throw "Result contract is not invariant in $relativePath" }
    if ($json.summary.totalMs.samplesMs.Count -ne 50) { throw "Total timing samples are incomplete in $relativePath" }
    return [pscustomobject]@{
        RelativePath = $relativePath
        Json = $json
        Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$records = @(
    (Read-Evidence $OrtEvidence 'onnxruntime-cpu'),
    (Read-Evidence $OpenVinoEvidence 'openvino-cpu'),
    (Read-Evidence $TensorRtEvidence 'tensorrt-cuda')
)
$first = $records[0].Json
foreach ($record in $records | Select-Object -Skip 1) {
    $current = $record.Json
    if ($current.sourceRevision -ne $first.sourceRevision) { throw 'ChartQA source revisions differ.' }
    if ($current.input.sample -ne $first.input.sample -or $current.input.sha256 -ne $first.input.sha256) { throw 'Benchmark inputs differ.' }
    if ($current.resultContract.outputSha256 -ne $first.resultContract.outputSha256 -or $current.resultContract.tokenCountIncludingEos -ne $first.resultContract.tokenCountIncludingEos -or $current.resultContract.finishReason -ne $first.resultContract.finishReason) { throw 'Backend result contracts differ.' }
}

$summaryBackends = foreach ($record in $records) {
    $json = $record.Json
    [ordered]@{
        backend = $json.backend
        evidencePath = $record.RelativePath
        evidenceSha256 = $record.Sha256
        runtime = if ($json.runtime) { $json.runtime } else { $null }
        device = $json.device
        models = if ($json.models) { $json.models } else { $json.engines }
        totalMs = $json.summary.totalMs
        visionMs = $json.summary.visionMs
        embeddingMs = $json.summary.embeddingMs
        prefillMs = $json.summary.prefillMs
        decodeP50Ms = $json.summary.decodeP50Ms
        decodeP95Ms = $json.summary.decodeP95Ms
    }
}

$output = [ordered]@{
    schemaVersion = 'deploysharp-chart2table-fixed-benchmark-summary-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    harnessRevision = $HarnessRevision
    sourceRevision = $first.sourceRevision
    protocol = $first.protocol
    input = $first.input
    resultContract = $first.resultContract
    clockControl = 'not locked; nvidia-smi was observed separately and is not used as a controlled performance gate'
    backends = $summaryBackends
    boundary = 'One fixed ChartQA validation image, one session and one channel per backend on one Windows host. This is a repeated latency and execution-contract record, not ChartQA split accuracy, a cross-device ranking, a throughput result, or a TensorRT plan portability guarantee.'
}
$jsonPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $JsonOutput))
$markdownPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $MarkdownOutput))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath) | Out-Null
$output | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $jsonPath -Encoding utf8

function Fmt([double]$value) { $value.ToString('F2', [Globalization.CultureInfo]::InvariantCulture) }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Chart2Table fixed-sample steady-state benchmark (2026-10-17)')
$lines.Add('')
$lines.Add('This report uses one pinned ChartQA validation image and the same four-graph Bundle/decoder on three backends. Each backend uses one session and one channel, with five warm-ups followed by fifty measured generations. It is a latency and execution-contract record, not a ChartQA accuracy score.')
$lines.Add('')
$lines.Add('## Protocol and result contract')
$lines.Add('')
$lines.Add("- ChartQA revision: ``$($first.sourceRevision)``; input ``$($first.input.sample)``; input SHA-256 ``$($first.input.sha256)``.")
$lines.Add("- Result on all 55 invocations per backend: ``$($first.resultContract.finishReason)``, ``$($first.resultContract.tokenCountIncludingEos)`` tokens, generated-text SHA-256 ``$($first.resultContract.outputSha256)``.")
$lines.Add('- CPU/GPU clocks were not locked. The values are therefore host/runtime observations, not a universal ranking.')
$lines.Add('')
$lines.Add('| Backend | Total P50/P95 ms | Vision P50/P95 ms | Prefill P50/P95 ms | Decode-token P50/P95 ms | Evidence |')
$lines.Add('|---|---:|---:|---:|---:|---|')
foreach ($record in $summaryBackends) {
    $decodeP50 = $record.decodeP50Ms.p50Ms
    $decodeP95 = $record.decodeP95Ms.p95Ms
    $lines.Add("| $($record.backend) | $(Fmt $record.totalMs.p50Ms) / $(Fmt $record.totalMs.p95Ms) | $(Fmt $record.visionMs.p50Ms) / $(Fmt $record.visionMs.p95Ms) | $(Fmt $record.prefillMs.p50Ms) / $(Fmt $record.prefillMs.p95Ms) | $(Fmt $decodeP50) / $(Fmt $decodeP95) | [$([IO.Path]::GetFileName($record.evidencePath))]($([IO.Path]::GetFileName($record.evidencePath))) |")
}
$lines.Add('')
$lines.Add('All three backends produced the same fixed-sample output contract in every warm-up and measured invocation. TensorRT uses the previously built library plans on the recorded RTX 3060/TensorRT 10.11 runtime; this does not establish plan portability to another GPU or runtime.')
$lines.Add('')
$lines.Add('## Reproduction')
$lines.Add('')
$lines.Add('Run the corresponding external test with the backend gate and paths documented in the PP-Structure README. Set ``DEPLOYSHARP_CHART2TABLE_BENCHMARK_REPORT_PATH`` to an output path, then summarize the three JSON files with ``pwsh -File eng/models/paddle-document/scripts/Summarize-Chart2TableSteadyBenchmark.ps1 -HarnessRevision <commit>``.')
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8
Write-Output "Wrote $jsonPath and $markdownPath"
