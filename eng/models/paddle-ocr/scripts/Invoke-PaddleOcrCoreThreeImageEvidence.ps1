[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")),
    [string]$ModelRoot = "E:\Model\paddleocr",
    [string]$OutputPath = "",
    [string]$MarkdownPath = "",
    [string]$Configuration = "Debug",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"
$testProject = Join-Path $RepositoryRoot "tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj"
if (-not (Test-Path -LiteralPath $testProject)) { throw "Test project not found: $testProject" }

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepositoryRoot "eng\models\paddle-ocr\verification\paddleocr-core-three-image-openvino-ort-20260930.json"
}
if ([string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $MarkdownPath = [IO.Path]::ChangeExtension($OutputPath, ".md")
}

$images = @(
    "E:\Data\ocr\demo_1.jpg",
    "E:\Data\ocr\demo_2.jpg",
    "E:\Data\ocr\demo_3.jpg"
)
$cases = @(
    [ordered]@{ name = "v4-mobile"; folder = "PP-OCRv4"; detector = "PP-OCRv4_mobile_det.onnx"; classifier = "PP-OCRv4_mobile_cls.onnx"; recognizer = "PP-OCRv4_mobile_rec.onnx"; dictionary = "ppocrv4_keys.txt" },
    [ordered]@{ name = "v4-server"; folder = "PP-OCRv4"; detector = "PP-OCRv4_server_det.onnx"; classifier = "PP-OCRv4_mobile_cls.onnx"; recognizer = "PP-OCRv4_server_rec.onnx"; dictionary = "ppocrv4_keys.txt" },
    [ordered]@{ name = "v5-mobile"; folder = "PP-OCRv5"; detector = "PP-OCRv5_mobile_det.onnx"; classifier = "PP-OCRv5_mobile_cls.onnx"; recognizer = "PP-OCRv5_mobile_rec.onnx"; dictionary = "ppocrv5_dict.txt" },
    [ordered]@{ name = "v5-server"; folder = "PP-OCRv5"; detector = "PP-OCRv5_server_det.onnx"; classifier = "PP-OCRv5_server_cls.onnx"; recognizer = "PP-OCRv5_server_rec.onnx"; dictionary = "ppocrv5_dict.txt" },
    [ordered]@{ name = "v6-tiny"; folder = "PP-OCRv6\tiny"; detector = "PP-OCRv6_tiny_det_inference.onnx"; classifier = "..\..\PP-OCRv5\PP-OCRv5_mobile_cls.onnx"; recognizer = "PP-OCRv6_tiny_rec_inference.onnx"; dictionary = "PP-OCRv6_tiny_rec_dict.txt" },
    [ordered]@{ name = "v6-small"; folder = "PP-OCRv6\small"; detector = "PP-OCRv6_small_det_inference.onnx"; classifier = "..\..\PP-OCRv5\PP-OCRv5_mobile_cls.onnx"; recognizer = "PP-OCRv6_small_rec_inference.onnx"; dictionary = "PP-OCRv6_small_rec_dict.txt" },
    [ordered]@{ name = "v6-medium"; folder = "PP-OCRv6\medium"; detector = "PP-OCRv6_medium_det_inference.onnx"; classifier = "..\..\PP-OCRv5\PP-OCRv5_mobile_cls.onnx"; recognizer = "PP-OCRv6_medium_rec_inference.onnx"; dictionary = "PP-OCRv6_medium_rec_dict.txt" }
)

foreach ($image in $images) {
    if (-not (Test-Path -LiteralPath $image)) { throw "OCR input image not found: $image" }
}

function Invoke-ExternalTest {
    param(
        [Parameter(Mandatory = $true)][string]$Backend,
        [string]$ImagePath = ""
    )

    $names = @(
        "DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL",
        "DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE",
        "DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL",
        "DEPLOYSHARP_PADDLEOCR_ORT_IMAGE",
        "DEPLOYSHARP_PADDLEOCR_ROOT"
    )
    $saved = @{}
    foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT", $ModelRoot)
        if ($Backend -eq "openvino") {
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL", "1")
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL", $null)
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE", $null)
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ORT_IMAGE", $null)
            $filter = "FullyQualifiedName~PaddleOcrAllCorePipelineOpenVinoIntegrationTests.EveryCorePpOcrVariantRunsOpenVinoAcrossThreeLocalImages"
        } else {
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL", $null)
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL", "1")
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ORT_IMAGE", $ImagePath)
            [Environment]::SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE", $null)
            $filter = "FullyQualifiedName~PaddleOcrAllCorePipelineOrtIntegrationTests.EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnRealOrtCpu"
        }

        $arguments = @(
            "test", $testProject,
            "-c", $Configuration,
            "-f", $Framework,
            "--no-build", "--no-restore",
            "--filter", $filter,
            "--logger", "console;verbosity=detailed"
        )
        $output = (& dotnet @arguments 2>&1 | Out-String -Width 4096)
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "$Backend PaddleOCR external test failed with exit code $exitCode.`n$output"
        }
        return [regex]::Replace($output, "\x1b\[[0-?]*[ -/]*[@-~]", "")
    } finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
}

function Parse-Runs {
    param([Parameter(Mandatory = $true)][string]$Output, [Parameter(Mandatory = $true)][string]$Backend)
    $pattern = "PADDLEOCR_(?:ORT|OPENVINO)_FULL_PIPELINE variant=(?<variant>[^;]+);regions=(?<regions>\d+);recognized=(?<recognized>\d+);elapsedMs=(?<elapsed>[^;]+);resultSha=(?<resultSha>[^;]+);textSha=(?<textSha>[0-9a-f]+)"
    $runs = @()
    foreach ($match in [regex]::Matches($Output, $pattern)) {
        $runs += [ordered]@{
            backend = $Backend
            variant = $match.Groups["variant"].Value
            regions = [int]$match.Groups["regions"].Value
            recognized = [int]$match.Groups["recognized"].Value
            elapsedMs = [double]::Parse($match.Groups["elapsed"].Value, [Globalization.CultureInfo]::InvariantCulture)
            resultSha256 = $match.Groups["resultSha"].Value
            textSha256 = $match.Groups["textSha"].Value
        }
    }
    return $runs
}

$openVinoOutput = Invoke-ExternalTest -Backend "openvino"
$openVinoRuns = @(Parse-Runs -Output $openVinoOutput -Backend "openvino-cpu")
if ($openVinoRuns.Count -ne ($images.Count * $cases.Count)) {
    throw "Expected $($images.Count * $cases.Count) OpenVINO records, found $($openVinoRuns.Count)."
}

$ortRuns = @()
foreach ($image in $images) {
    $ortOutput = Invoke-ExternalTest -Backend "ort" -ImagePath $image
    $imageRuns = @(Parse-Runs -Output $ortOutput -Backend "onnxruntime-cpu")
    if ($imageRuns.Count -ne $cases.Count) { throw "Expected $($cases.Count) ORT records for $image, found $($imageRuns.Count)." }
    $ortRuns += $imageRuns
}

function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
$imageRecords = @($images | ForEach-Object { [ordered]@{ path = $_; sha256 = Get-Sha256 $_; lengthBytes = (Get-Item -LiteralPath $_).Length } })
$artifactRecords = @()
foreach ($testCase in $cases) {
    $folder = Join-Path $ModelRoot $testCase.folder
    $classifierPath = if ($testCase.name.StartsWith("v6-")) { Join-Path $ModelRoot "PP-OCRv5\PP-OCRv5_mobile_cls.onnx" } else { Join-Path $folder $testCase.classifier }
    $paths = [ordered]@{
        detector = Join-Path $folder $testCase.detector
        classifier = $classifierPath
        recognizer = Join-Path $folder $testCase.recognizer
        dictionary = Join-Path $folder $testCase.dictionary
    }
    $hashes = [ordered]@{}
    foreach ($key in $paths.Keys) {
        if (-not (Test-Path -LiteralPath $paths[$key])) { throw "Model artifact not found for $($testCase.name): $($paths[$key])" }
        $hashes[$key] = Get-Sha256 $paths[$key]
    }
    $artifactRecords += [ordered]@{ variant = $testCase.name; files = $paths; sha256 = $hashes }
}

$comparisons = @()
for ($imageIndex = 0; $imageIndex -lt $images.Count; $imageIndex++) {
    for ($caseIndex = 0; $caseIndex -lt $cases.Count; $caseIndex++) {
        $openVino = $openVinoRuns[$imageIndex * $cases.Count + $caseIndex]
        $ort = $ortRuns[$imageIndex * $cases.Count + $caseIndex]
        if ($openVino.variant -ne $cases[$caseIndex].name -or $ort.variant -ne $cases[$caseIndex].name) { throw "Unexpected variant ordering in test output." }
        $comparisons += [ordered]@{
            image = $images[$imageIndex]
            variant = $cases[$caseIndex].name
            regionCountMatch = ($openVino.regions -eq $ort.regions)
            recognizedCountMatch = ($openVino.recognized -eq $ort.recognized)
            textShaMatch = ($openVino.textSha256 -eq $ort.textSha256)
            openVino = $openVino
            ort = $ort
        }
    }
}

$summary = [ordered]@{
    imageCount = $images.Count
    variantCount = $cases.Count
    combinations = $comparisons.Count
    openVinoExecutionPass = ($openVinoRuns | Where-Object { $_.regions -gt 0 -and $_.recognized -gt 0 }).Count
    ortExecutionPass = ($ortRuns | Where-Object { $_.regions -gt 0 -and $_.recognized -gt 0 }).Count
    regionCountMatches = ($comparisons | Where-Object { $_.regionCountMatch }).Count
    recognizedCountMatches = ($comparisons | Where-Object { $_.recognizedCountMatch }).Count
    textShaMatches = ($comparisons | Where-Object { $_.textShaMatch }).Count
    textShaMatchRate = [math]::Round((($comparisons | Where-Object { $_.textShaMatch }).Count / [double]$comparisons.Count), 4)
}

$gitRevision = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
$dirty = -not [string]::IsNullOrWhiteSpace((& git -C $RepositoryRoot status --porcelain --untracked-files=no | Out-String))
$report = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    sourceRevision = $gitRevision + $(if ($dirty) { "+working-tree" } else { "" })
    protocol = [ordered]@{
        testProject = $testProject
        configuration = $Configuration
        framework = $Framework
        openVinoCommand = "PaddleOcrAllCorePipelineOpenVinoIntegrationTests.EveryCorePpOcrVariantRunsOpenVinoAcrossThreeLocalImages"
        ortCommand = "PaddleOcrAllCorePipelineOrtIntegrationTests.EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnRealOrtCpu (one image per invocation)"
        preprocessing = "OpenCV input factory + PP-OCR official detection preprocessing"
        timing = "single end-to-end pipeline invocation per model/image; elapsedMs includes preparation, DET, CLS, crop, REC and merge"
    }
    device = [ordered]@{ os = [Environment]::OSVersion.VersionString; processor = [Environment]::GetEnvironmentVariable("PROCESSOR_IDENTIFIER"); runtime = "OpenVINO CPU / ONNX Runtime CPU" }
    inputs = $imageRecords
    models = $artifactRecords
    runs = [ordered]@{ openVino = $openVinoRuns; ort = $ortRuns }
    comparisons = $comparisons
    summary = $summary
    limitations = @(
        "输入图片没有逐字人工真值；本报告只证明完整流水线执行、数量/文本跨后端匹配和可复现摘要，不是 CER/WER、召回率或准确率报告。",
        "single invocation per combination is smoke timing, not the formal 5/50 performance protocol.",
        "v6 uses the official PP-OCRv5 mobile CLS model because no separately versioned v6 CLS artifact is present."
    )
}

foreach ($path in @($OutputPath, $MarkdownPath)) { $directory = Split-Path -Parent $path; if ($directory) { New-Item -ItemType Directory -Force $directory | Out-Null } }
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

$markdown = @(
    "# PP-OCR v4/v5/v6 三图跨后端流水线证据",
    "",
    "> 生成时间：$($report.generatedUtc)；源码：$($report.sourceRevision)。本报告只证明执行覆盖与跨后端输出摘要，不是准确率报告。",
    "",
    "## 范围",
    "",
    "- 输入：`demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg`。",
    "- 模型：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组。",
    "- 后端：ONNX Runtime CPU 与 OpenVINO CPU。",
    "- 每组记录区域数、已识别区域数、端到端耗时、整结果 SHA 和按区域纯文本 SHA。",
    "",
    "## 汇总",
    "",
    "| 项目 | 结果 |",
    "| --- | ---: |",
    "| 组合数 | $($summary.combinations) |",
    "| OpenVINO 完整执行 | $($summary.openVinoExecutionPass)/$($summary.combinations) |",
    "| ORT 完整执行 | $($summary.ortExecutionPass)/$($summary.combinations) |",
    "| 区域数一致 | $($summary.regionCountMatches)/$($summary.combinations) |",
    "| 已识别数一致 | $($summary.recognizedCountMatches)/$($summary.combinations) |",
    "| 纯文本 SHA 一致 | $($summary.textShaMatches)/$($summary.combinations) |",
    "",
    "## 逐组合结果",
    "",
    "| 图片 | 模型 | ORT 区域/识别 | OpenVINO 区域/识别 | 纯文本 SHA | ORT ms | OpenVINO ms |",
    "| --- | --- | ---: | ---: | :---: | ---: | ---: |"
)
foreach ($comparison in $comparisons) {
    $imageName = Split-Path -Leaf $comparison.image
    $textMatch = if ($comparison.textShaMatch) { "yes" } else { "no" }
    $markdown += "| $imageName | $($comparison.variant) | $($comparison.ort.regions)/$($comparison.ort.recognized) | $($comparison.openVino.regions)/$($comparison.openVino.recognized) | $textMatch | $([math]::Round($comparison.ort.elapsedMs, 3)) | $([math]::Round($comparison.openVino.elapsedMs, 3)) |"
}
$markdown += @(
    "",
    "## 边界",
    "",
    "输入没有人工逐字真值，因此不能从本报告计算 CER/WER、检测召回率或识别准确率；单次 elapsedMs 也不替代正式 5 次预热 + 50 次测量。v6 明确复用 PP-OCRv5 mobile CLS。机器可读完整记录见 [paddleocr-core-three-image-openvino-ort-20260930.json](paddleocr-core-three-image-openvino-ort-20260930.json)。",
    "",
    "复现脚本：eng/models/paddle-ocr/scripts/Invoke-PaddleOcrCoreThreeImageEvidence.ps1。"
)
$markdown -join [Environment]::NewLine | Set-Content -LiteralPath $MarkdownPath -Encoding UTF8
Write-Output ("Wrote " + $OutputPath)
Write-Output ("Wrote " + $MarkdownPath)
Write-Output ($summary | ConvertTo-Json -Compress)
