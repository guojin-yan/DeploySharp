param(
    [string]$OnnxRuntimeEvidence = 'eng/models/paddle-document/verification/paddle-document-table-pipeline-20261014-onnxruntime.json',
    [string]$OpenVinoEvidence = 'eng/models/paddle-document/verification/paddle-document-table-pipeline-20261014-openvino.json',
    [string]$JsonOutput = 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.json',
    [string]$MarkdownOutput = 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path

function Read-Evidence([string]$RelativePath) {
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Evidence file not found: $RelativePath" }
    $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return [pscustomobject]@{ Path = $path; RelativePath = $RelativePath; Json = $json; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
}

$ort = Read-Evidence $OnnxRuntimeEvidence
$openvino = Read-Evidence $OpenVinoEvidence
$left = $ort.Json
$right = $openvino.Json

if ($left.backendId -ne 'onnxruntime' -or $right.backendId -ne 'openvino') { throw 'Expected one ORT and one OpenVINO evidence file.' }
if ($left.input.sha256 -ne $right.input.sha256) { throw 'Backend evidence does not use the same input SHA-256.' }
if ($left.execution.benchmark.warmupCount -ne $right.execution.benchmark.warmupCount -or $left.execution.benchmark.measurementCount -ne $right.execution.benchmark.measurementCount) { throw 'Backend evidence uses different warm-up/measurement counts.' }
foreach ($item in @($left, $right)) {
    if ($item.execution.benchmark.invariantResultContract -ne $true) { throw "Benchmark result contract was not stable for $($item.backend)." }
    if ($item.execution.benchmark.totalMs.samplesMs.Count -ne $item.execution.benchmark.measurementCount) { throw "Total timing sample count is incomplete for $($item.backend)." }
}
$leftRun = $left.execution
$rightRun = $right.execution
$sameTaskOutput = $leftRun.classification.label -eq $rightRun.classification.label -and
    $leftRun.cellDetectionCandidates -eq $rightRun.cellDetectionCandidates -and
    $leftRun.structureRegions -eq $rightRun.structureRegions -and
    $leftRun.tokenCount -eq $rightRun.tokenCount -and
    $leftRun.htmlSha256 -eq $rightRun.htmlSha256
if (!$sameTaskOutput) { throw 'The ORT/OpenVINO task-level result contract differs.' }

$backends = @(
    [ordered]@{
        backend = $left.backend
        evidencePath = $ort.RelativePath
        evidenceSha256 = $ort.Sha256
        environment = $left.environment
        models = $left.models
        execution = $left.execution
    },
    [ordered]@{
        backend = $right.backend
        evidencePath = $openvino.RelativePath
        evidenceSha256 = $openvino.Sha256
        environment = $right.environment
        models = $right.models
        execution = $right.execution
    }
)
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-table-pipeline-benchmark-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    sourceRevision = $left.sourceRevision
    protocol = [ordered]@{
        warmupCount = $left.execution.benchmark.warmupCount
        measurementCount = $left.execution.benchmark.measurementCount
        inputSha256 = $left.input.sha256
        sameTaskOutputAcrossBackends = $sameTaskOutput
        resultInvariantAcrossMeasurements = $true
    }
    backends = $backends
    boundary = 'Single host, single official table image, one execution channel per backend. The report records task-chain execution and steady repeated latency only; the image has no cell/table ground truth, so this is not table accuracy, recall, a dataset score, cross-device ranking, or general backend support.'
}

$jsonPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $JsonOutput))
$markdownPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $MarkdownOutput))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownPath) | Out-Null
$summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $jsonPath -Encoding utf8

function Fmt([double]$Value) { return $Value.ToString('F2', [Globalization.CultureInfo]::InvariantCulture) }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# PP-Structure table task pipeline: ORT/OpenVINO benchmark (2026-10-14)')
$lines.Add('')
$lines.Add('This record executes table classification → wired table-cell detection → SLANeXt structure decoding → HTML generation on one official table image. It is a fixed-host execution and repeated-latency report, not a quality score.')
$lines.Add('')
$lines.Add('## Protocol and input')
$lines.Add('')
$lines.Add("- Host: $($left.environment.machine), $($left.environment.operatingSystem), $($left.environment.runtime), $($left.environment.processorCount) logical processors.")
$lines.Add("- Input: ``$($left.input.path)``; SHA-256 ``$($left.input.sha256)``; size $($left.input.sourceSize.width)×$($left.input.sourceSize.height).")
$lines.Add("- Protocol: $($summary.protocol.warmupCount) warm-ups + $($summary.protocol.measurementCount) measured page runs, one page per invocation; exact output-contract assertions on every measured run.")
$lines.Add('- Each backend uses its own CPU pipeline/session. OpenVINO SLANeXt uses the separately derived, SHA-recorded compatibility graph; ORT uses the official graph.')
$lines.Add('')
$lines.Add('## Results')
$lines.Add('')
$lines.Add('| Backend | Total P50/P95 (ms) | Classification P50/P95 (ms) | Cell detector P50/P95 (ms) | SLANeXt P50/P95 (ms) | Table output contract |')
$lines.Add('|---|---:|---:|---:|---:|---|')
foreach ($record in $backends) {
    $run = $record.execution
    $stages = $run.benchmark.stageMs
    $lines.Add("| $($record.backend) | $(Fmt $run.benchmark.totalMs.p50Ms) / $(Fmt $run.benchmark.totalMs.p95Ms) | $(Fmt $stages.TableClassification.p50Ms) / $(Fmt $stages.TableClassification.p95Ms) | $(Fmt $stages.TableCellDetection.p50Ms) / $(Fmt $stages.TableCellDetection.p95Ms) | $(Fmt $stages.TableStructureRecognition.p50Ms) / $(Fmt $stages.TableStructureRecognition.p95Ms) | $($run.classification.label), $($run.cellDetectionCandidates) candidates, $($run.structureRegions) structure regions, $($run.tokenCount) tokens, HTML SHA-256 ``$($run.htmlSha256)`` |")
}
$lines.Add('')
$lines.Add("Both backends returned the same classification label, candidate count, structure-region count, token count and exact HTML SHA-256. All 50 measured invocations per backend preserved its baseline output contract.")
$lines.Add('')
$lines.Add('## Provenance and limits')
$lines.Add('')
foreach ($record in $backends) {
    $lines.Add("- $($record.backend): [$([IO.Path]::GetFileName($record.evidencePath))]($([IO.Path]::GetFileName($record.evidencePath))) (SHA-256 ``$($record.evidenceSha256)``). Models: table classification ``$($record.models.tableClassification.sha256)``, cell detector ``$($record.models.cellDetection.sha256)``, SLANeXt ``$($record.models.structureRecognition.sha256)``.")
}
$lines.Add('- The source image has no cell/table annotations. Candidate count uses the current zero score threshold, so it is not a precision/recall measurement.')
$lines.Add('- Timings are for this host and exact graph/runtime configuration; CPU frequency was not locked. They are not a cross-device ranking or a guarantee for other inputs.')
$lines.Add('- OpenVINO uses the derived SLANeXt compatibility graph; this does not make the original Loop graph directly importable. TensorRT and OpenCV are outside this paired report.')
$lines.Add('')
$lines.Add("Reproduce from the repository root by setting ``DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1``, ``DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_TABLE_PIPELINE=1``, ``DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_PIPELINE_BENCHMARK=1`` and ``DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_PIPELINE_EVIDENCE_PATH`` to a path containing ``{backend}``, then run ``dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~PaddleDocumentTablePipelineIntegrationTests``. Summarize the paired outputs with ``pwsh -File eng/models/paddle-document/scripts/Summarize-PaddleDocumentTablePipelineEvidence.ps1``.")
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8
Write-Output "Wrote paired summary; sameOutput=$sameTaskOutput; JSON=$jsonPath; Markdown=$markdownPath"
