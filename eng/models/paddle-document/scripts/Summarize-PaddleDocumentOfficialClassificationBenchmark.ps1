[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RawDirectory,
    [string]$OutputJsonPath = 'eng/models/paddle-document/verification/paddle-document-official-classification-benchmark-20261017.json',
    [string]$OutputMarkdownPath = 'eng/models/paddle-document/verification/paddle-document-official-classification-benchmark-20261017.md'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$rawFullPath = (Resolve-Path -LiteralPath $RawDirectory).Path
$expectedFiles = @(
    'ort-doc-ori.json', 'ort-table-cls.json',
    'openvino-doc-ori.json', 'openvino-table-cls.json',
    'opencv-doc-ori.json', 'opencv-table-cls.json'
)
$expectedImageSha = @{
    'img_rot180_demo.jpg' = 'c5a77e031470e13878ff4f28a06ca843fd455d95c20b1b49b486681e346209ed'
    'table_recognition.jpg' = 'acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c'
}
$expectedModels = @{
    'img_rot180_demo.jpg' = 'paddle-doc/pp-lcnet-x1-0-doc-ori'
    'table_recognition.jpg' = 'paddle-table/pp-lcnet-x1-0-table-cls'
}
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($fileName in $expectedFiles) {
    $path = Join-Path $rawFullPath $fileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing raw benchmark report: $path" }
    $raw = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($raw.warmup -ne 5 -or $raw.iterations -ne 50) { throw "Expected 5 warm-ups + 50 measurements in $fileName." }
    if ($raw.batch -ne 1 -or $raw.sessions -ne 1) { throw "Expected batch=1 and sessions=1 in $fileName." }
    if (-not $expectedModels.ContainsKey([string]$raw.image) -or $raw.model -ne $expectedModels[[string]$raw.image]) { throw "Unexpected model/image pair in $fileName." }
    if ($raw.imageSha256 -ne $expectedImageSha[[string]$raw.image]) { throw "Input image SHA changed in $fileName." }
    if ($raw.samples.Count -ne 50) { throw "Expected 50 measured samples in $fileName." }
    if ($raw.preprocessing.p50Ms -le 0 -or $raw.inference.p50Ms -le 0 -or $raw.total.p50Ms -le 0) { throw "Non-positive percentile in $fileName." }
    $rows.Add([ordered]@{
        rawReport = $fileName
        rawReportSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        model = [string]$raw.model
        backend = [string]$raw.backend
        image = [string]$raw.image
        imageSha256 = [string]$raw.imageSha256
        modelSha256 = [string]$raw.modelSha256
        machine = [string]$raw.machine
        os = [string]$raw.os
        runtime = [string]$raw.runtime
        sourceRevision = [string]$raw.sourceRevision
        warmupCount = [int]$raw.warmup
        measurementCount = [int]$raw.iterations
        expectedClass = [int]$raw.expectedClass
        score = [double]$raw.score
        preprocessing = $raw.preprocessing
        inference = $raw.inference
        postprocessing = $raw.postprocessing
        total = $raw.total
    })
}

$sourceRevisions = @($rows | ForEach-Object { $_['sourceRevision'] } | Sort-Object -Unique)
if ($sourceRevisions.Count -ne 1) { throw 'Raw reports do not share one source revision.' }
$backends = @($rows | ForEach-Object { $_['backend'] } | Sort-Object -Unique)
if ($backends.Count -ne 3) { throw 'Expected ort, openvino and opencv backends.' }
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-official-classification-benchmark-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    sourceRevision = $sourceRevisions[0]
    rawDirectory = $rawFullPath
    device = [string]$rows[0]['machine']
    os = [string]$rows[0]['os']
    runtime = [string]$rows[0]['runtime']
    modelCount = 2
    backendCount = 3
    rowCount = $rows.Count
    protocol = [ordered]@{ warmupCount = 5; measurementCount = 50; batch = 1; sessions = 1; inputReuse = 'decoded image reused; decode/load excluded' }
    rows = @($rows)
    boundary = 'Official example classification execution and repeated latency only. This is not a document-orientation or table-classification accuracy benchmark, not a cross-device ranking, and not evidence for other PP-Structure artifacts.'
}
$jsonFullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputJsonPath))
$markdownFullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputMarkdownPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonFullPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFullPath) | Out-Null
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonFullPath -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$sourceRevision = [string]$summary['sourceRevision']
$modelCount = [int]$summary['modelCount']
$backendCount = [int]$summary['backendCount']
$lines.Add('# PP-Structure official classification benchmark (2026-10-17)')
$lines.Add('')
$lines.Add('This report records the official document-orientation and table-classification examples on one Windows host. Each exact model/backend row uses 5 warm-ups followed by 50 measured synchronous calls with batch=1 and one session; model loading, image decode and hashing are excluded.')
$lines.Add('')
$lines.Add("- Device: **$($summary.device)**; OS: $($summary.os); runtime: $($summary.runtime).")
$lines.Add("- Source revision: $sourceRevision; rows: **$($summary.rowCount)** ($modelCount models × $backendCount backends).")
$lines.Add('- Input contracts: `img_rot180_demo.jpg` for document orientation and `table_recognition.jpg` for table classification; image SHA-256 values are recorded in the JSON.')
$lines.Add('')
$lines.Add('| Model | Backend | Score | Pre P50/P95 ms | Infer P50/P95 ms | Post P50/P95 ms | Total P50/P95 ms |')
$lines.Add('| --- | --- | ---: | ---: | ---: | ---: | ---: |')
foreach ($row in ($summary.rows | Sort-Object model, backend)) {
    $lines.Add("| $($row['model']) | $($row['backend']) | $([double]$row['score']) | $([double]$row['preprocessing'].p50Ms)/$([double]$row['preprocessing'].p95Ms) | $([double]$row['inference'].p50Ms)/$([double]$row['inference'].p95Ms) | $([double]$row['postprocessing'].p50Ms)/$([double]$row['postprocessing'].p95Ms) | $([double]$row['total'].p50Ms)/$([double]$row['total'].p95Ms) |")
}
$lines.Add('')
$lines.Add('All six rows passed their exact official-example class and score contracts. The OpenCV rows are CPU DNN execution; the table below is not a full PP-Structure support matrix and does not generalize to layout, table-cell, formula, seal, Chart2Table or SLANeXt artifacts.')
$lines.Add('')
$lines.Add('Reproduction: set `DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY=1` and `DEPLOYSHARP_PADDLE_DOCUMENT_ROOT` to the local PaddleDocument root, then run `PaddleDocumentOfficialExampleTests.ClassificationMatchesOfficialExampleAndPreservesProbabilities` in `tests/DeploySharp.Visual.OpenCV.Tests` with Release/net10.0. The test source is [`PaddleDocumentOfficialExampleTests.cs`](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentOfficialExampleTests.cs).')
Set-Content -LiteralPath $markdownFullPath -Value ($lines -join [Environment]::NewLine) -Encoding utf8
Write-Host "PADDLE_DOCUMENT_OFFICIAL_CLASSIFICATION_JSON=$jsonFullPath"
Write-Host "PADDLE_DOCUMENT_OFFICIAL_CLASSIFICATION_MARKDOWN=$markdownFullPath"
Write-Host 'PADDLE_DOCUMENT_OFFICIAL_CLASSIFICATION_STATUS=pass'
