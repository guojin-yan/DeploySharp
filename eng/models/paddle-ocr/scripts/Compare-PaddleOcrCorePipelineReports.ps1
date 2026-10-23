[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OrtReportPath,
    [Parameter(Mandatory)]
    [string]$OpenVinoReportPath,
    [Parameter(Mandatory)]
    [string]$OutputJsonPath,
    [Parameter(Mandatory)]
    [string]$OutputMarkdownPath
)

$ErrorActionPreference = 'Stop'

function Read-Report([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Get-Key($Run) {
    return '{0}|{1}' -f $Run.Variant, $Run.ImageFileName
}

function Assert-UniqueCompleteRuns($Report, [string]$Backend) {
    if (@($Report.Runs).Count -ne 21) {
        throw "$Backend report must contain exactly 21 model/image runs; found $(@($Report.Runs).Count)."
    }

    $keys = @($Report.Runs | ForEach-Object { Get-Key $_ })
    if (@($keys | Sort-Object -Unique).Count -ne 21) {
        throw "$Backend report contains duplicate model/image keys."
    }

    $expectedModels = @('v4-mobile', 'v4-server', 'v5-mobile', 'v5-server', 'v6-tiny', 'v6-small', 'v6-medium')
    $expectedImages = @('demo_1.jpg', 'demo_2.jpg', 'demo_3.jpg')
    foreach ($model in $expectedModels) {
        foreach ($image in $expectedImages) {
            $key = "$model|$image"
            if ($keys -notcontains $key) { throw "$Backend report is missing $key." }
        }
    }
}

$ort = Read-Report $OrtReportPath
$openVino = Read-Report $OpenVinoReportPath
Assert-UniqueCompleteRuns $ort 'ORT'
Assert-UniqueCompleteRuns $openVino 'OpenVINO'

$ortByKey = @{}
foreach ($run in $ort.Runs) { $ortByKey[(Get-Key $run)] = $run }
$pairs = [System.Collections.Generic.List[object]]::new()
$identityMismatches = [System.Collections.Generic.List[string]]::new()
$regionMismatches = [System.Collections.Generic.List[string]]::new()
$textMismatches = [System.Collections.Generic.List[string]]::new()

foreach ($ovRun in $openVino.Runs) {
    $key = Get-Key $ovRun
    $ortRun = $ortByKey[$key]
    $identityMatches = ($ovRun.ImageSha256 -eq $ortRun.ImageSha256) -and
        ($ovRun.DetectorSha256 -eq $ortRun.DetectorSha256) -and
        ($ovRun.ClassifierSha256 -eq $ortRun.ClassifierSha256) -and
        ($ovRun.RecognizerSha256 -eq $ortRun.RecognizerSha256) -and
        ($ovRun.DictionarySha256 -eq $ortRun.DictionarySha256)
    $regionMatches = ($ovRun.RegionCount -eq $ortRun.RegionCount) -and
        ($ovRun.RecognizedRegionCount -eq $ortRun.RecognizedRegionCount)
    $textMatches = $ovRun.TextSha256 -eq $ortRun.TextSha256
    $resultMatches = $ovRun.ResultSha256 -eq $ortRun.ResultSha256

    if (-not $identityMatches) { $identityMismatches.Add($key) }
    if (-not $regionMatches) { $regionMismatches.Add($key) }
    if (-not $textMatches) { $textMismatches.Add($key) }

    $pairs.Add([pscustomobject]@{
        Variant = $ovRun.Variant
        ImageFileName = $ovRun.ImageFileName
        ImageSha256 = $ovRun.ImageSha256
        DetectorSha256 = $ovRun.DetectorSha256
        ClassifierSha256 = $ovRun.ClassifierSha256
        RecognizerSha256 = $ovRun.RecognizerSha256
        DictionarySha256 = $ovRun.DictionarySha256
        OpenVinoRegionCount = $ovRun.RegionCount
        OrtRegionCount = $ortRun.RegionCount
        OpenVinoRecognizedRegionCount = $ovRun.RecognizedRegionCount
        OrtRecognizedRegionCount = $ortRun.RecognizedRegionCount
        IdentityMatches = $identityMatches
        RegionCountsMatch = $regionMatches
        TextSha256Matches = $textMatches
        OpenVinoTextSha256 = $ovRun.TextSha256
        OrtTextSha256 = $ortRun.TextSha256
        CanonicalResultSha256Matches = $resultMatches
        OpenVinoResultSha256 = $ovRun.ResultSha256
        OrtResultSha256 = $ortRun.ResultSha256
    })
}

if ($identityMismatches.Count -gt 0) { throw "Image/model identity mismatch: $($identityMismatches -join ', ')." }
if ($regionMismatches.Count -gt 0) { throw "Region/recognized count mismatch: $($regionMismatches -join ', ')." }
if ($textMismatches.Count -gt 0) { throw "Ordered text fingerprint mismatch: $($textMismatches -join ', ')." }

$models = [System.Collections.Generic.List[object]]::new()
foreach ($group in $pairs | Group-Object Variant | Sort-Object Name) {
    $ordered = @($group.Group | Sort-Object ImageFileName)
    $models.Add([pscustomobject]@{
        Variant = $group.Name
        ImageCount = $ordered.Count
        RegionsByImage = @($ordered | ForEach-Object {
            [pscustomobject]@{ ImageFileName = $_.ImageFileName; RegionCount = $_.OpenVinoRegionCount }
        })
        TotalRegionsPerBackend = ($ordered | Measure-Object OpenVinoRegionCount -Sum).Sum
        TextParityCount = @($ordered | Where-Object TextSha256Matches).Count
        CanonicalResultParityCount = @($ordered | Where-Object CanonicalResultSha256Matches).Count
    })
}

$summary = [pscustomobject]@{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow
    Status = 'pass-with-canonical-result-differences'
    Scope = 'Seven PP-OCR v4/v5/v6 complete DET-to-merge pipelines on three fixed images; ONNX Runtime CPU versus OpenVINO CPU.'
    PairCount = $pairs.Count
    ImageModelIdentityMatches = $pairs.Count - $identityMismatches.Count
    RegionAndRecognizedCountMatches = $pairs.Count - $regionMismatches.Count
    OrderedTextFingerprintMatches = $pairs.Count - $textMismatches.Count
    CanonicalResultFingerprintMatches = @($pairs | Where-Object CanonicalResultSha256Matches).Count
    CanonicalResultFingerprintDifferences = @($pairs | Where-Object { -not $_.CanonicalResultSha256Matches }).Count
    Inputs = @($pairs | Group-Object ImageFileName | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{ ImageFileName = $_.Name; Sha256 = $_.Group[0].ImageSha256 }
    })
    Backends = @(
        [pscustomobject]@{
            Name = $ort.Backend
            OperatingSystem = $ort.OperatingSystem
            ProcessArchitecture = $ort.ProcessArchitecture
            DotNetRuntime = $ort.DotNetRuntime
            RuntimeVersion = $ort.OnnxRuntimeNativeVersion
            ProviderAssemblyVersion = $ort.OnnxRuntimeProviderAssemblyVersion
            SourceCommit = $ort.SourceCommit
            SourceTreeDirty = $ort.SourceTreeDirty
            TestSourceSha256 = $ort.TestSourceSha256
            WarmupRuns = $ort.WarmupRuns
            MeasuredRunsPerModelImage = $ort.MeasuredRunsPerModelImage
        },
        [pscustomobject]@{
            Name = $openVino.Backend
            OperatingSystem = $openVino.OperatingSystem
            ProcessArchitecture = $openVino.ProcessArchitecture
            DotNetRuntime = $openVino.DotNetRuntime
            RuntimeVersion = $openVino.OpenVinoRuntimeVersion
            ProviderAssemblyVersion = $openVino.OpenVinoProviderAssemblyVersion
            SourceCommit = $openVino.SourceCommit
            SourceTreeDirty = $openVino.SourceTreeDirty
            TestSourceSha256 = $openVino.TestSourceSha256
            WarmupRuns = $openVino.WarmupRuns
            MeasuredRunsPerModelImage = $openVino.MeasuredRunsPerModelImage
        }
    )
    Boundary = 'Single-run functional integration evidence only. Inputs and Session creation are outside the recorded OcrPipeline.Run timing. Demo images have no transcription ground truth. Text and region-count parity do not establish accuracy; canonical OcrResult fingerprints differ and require separate geometry/confidence/token-drift analysis. RTX 2060 is host hardware only: both tested backends ran on CPU. Not a steady-state benchmark, GPU test, or full support claim.'
    Models = $models
    Pairs = @($pairs | Sort-Object Variant, ImageFileName)
}

$jsonPath = [IO.Path]::GetFullPath($OutputJsonPath)
$markdownPath = [IO.Path]::GetFullPath($OutputMarkdownPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownPath) | Out-Null
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$builder = [Text.StringBuilder]::new()
[void]$builder.AppendLine('# PP-OCR core seven-model three-image CPU backend check (2026-10-23)')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Result')
[void]$builder.AppendLine()
[void]$builder.AppendLine('All 21 model/image pairs completed the full `det → crop → cls/orientation → rec → merge` pipeline on both ONNX Runtime CPU and OpenVINO CPU. The image and model artifact identities match for `21/21`; detected and recognized region counts match for `21/21`; ordered text SHA-256 fingerprints match for `21/21`. The canonical `OcrResult` SHA-256 matches for `0/21`, so this is text/count parity, **not** full result parity.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Model | demo_1 regions | demo_2 regions | demo_3 regions | Total per backend | Text SHA parity | Canonical result SHA parity |')
[void]$builder.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($model in $models) {
    $counts = @($model.RegionsByImage | ForEach-Object RegionCount)
    [void]$builder.AppendLine("| $($model.Variant) | $($counts[0]) | $($counts[1]) | $($counts[2]) | $($model.TotalRegionsPerBackend) | $($model.TextParityCount)/3 | $($model.CanonicalResultParityCount)/3 |")
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Device and runtime')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Remote host: Ubuntu 22.04.5 LTS, x64, .NET 10.0.9; host GPU is NVIDIA RTX 2060. Both runs used CPU execution only. ORT native runtime: 1.28.0. OpenVINO runtime: 2026.3.1. No GPU execution, frequency tuning, or GPU performance was tested.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Each model/image pair was run once with zero warmups. Timing starts at `OcrPipeline.Run` after image input preparation and backend Session creation; reported elapsed values are diagnostic only and must not be used as a latency ranking or steady-state benchmark.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Source commit recorded by both runs: `16facb3`; both source trees were dirty. The respective test-source SHA-256 values are recorded in the JSON evidence. Input, detector, classifier, recognizer and dictionary SHA-256 values are also retained per pair.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Parity boundary')
[void]$builder.AppendLine()
[void]$builder.AppendLine('The text fingerprint is SHA-256 over the ordered recognized strings joined with newline separators. Matching text and region counts do not prove polygon, confidence, orientation, token, or result metadata equality; all 21 canonical result fingerprints differ. The demo images have no manually verified transcription annotations, so this run does not calculate CER/WER, detection recall, or accuracy. Follow-up should inspect exact per-field drift on controlled model/image pairs before describing the backends as result-equivalent.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Reproduction')
[void]$builder.AppendLine()
[void]$builder.AppendLine('On a machine with the listed models and images, set `DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL=1` and/or `DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1`, configure `DEPLOYSHARP_PADDLEOCR_ROOT` and the backend-specific `*_IMAGES` semicolon-delimited list, then run the corresponding `PaddleOcrAllCorePipeline*IntegrationTests` opt-in test. Configure `DEPLOYSHARP_PADDLEOCR_ORT_REPORT` or `DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT` to emit raw evidence JSON. Compare a new report pair with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrCorePipelineReports.ps1`.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Machine-readable pair report: [`paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.json`](paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.json). Source reports: [ORT](paddleocr-core-seven-models-ort-three-image-linux-rtx2060-20261023.json) and [OpenVINO](paddleocr-core-seven-models-openvino-three-image-linux-rtx2060-20261023.json).')
Set-Content -LiteralPath $markdownPath -Value $builder.ToString().TrimEnd() -Encoding utf8

Write-Host "PAIR_COUNT=$($pairs.Count)"
Write-Host "TEXT_PARITY=$(@($pairs | Where-Object TextSha256Matches).Count)/$($pairs.Count)"
Write-Host "CANONICAL_RESULT_PARITY=$(@($pairs | Where-Object CanonicalResultSha256Matches).Count)/$($pairs.Count)"
Write-Host "OUTPUT_JSON=$jsonPath"
Write-Host "OUTPUT_MARKDOWN=$markdownPath"
