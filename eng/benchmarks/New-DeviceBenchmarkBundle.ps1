[CmdletBinding()]
param(
    [string]$OutputRoot = 'F:\deploysharp-bin',
    [string]$ModelRoot = 'E:\Model',
    [Parameter(Mandatory = $true)]
    [string]$VisualImagePath,
    [string]$PaddleOcrRoot = 'E:\Model\paddleocr',
    [string]$PaddleOcrImagePath = 'E:\Data\ocr\demo_1.jpg',
    [string]$SpecialVisualModelRoot = 'E:\DeploySharp-Models',
    [string]$TensorRtBridgePath,
    [string]$CudaRuntimeBinPath,
    [string]$Cuda13RuntimeBinPath,
    [string]$CudnnRuntimeBinPath,
    [string]$TensorRtRuntimeBinPath,
    [switch]$UpdateOnly,
    [switch]$SkipArchive
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$bundleName = if ($UpdateOnly) { "DeploySharp-DeviceBenchmark-Update-$stamp" } else { "DeploySharp-DeviceBenchmark-$stamp" }
$bundleRoot = Join-Path $OutputRoot $bundleName
$modelOutput = Join-Path $bundleRoot 'models\visual'
$ocrModelOutput = Join-Path $bundleRoot 'models\paddleocr'
$ocrToolOutput = Join-Path $bundleRoot 'tools\paddleocr'
$specialModelOutput = Join-Path $bundleRoot 'models\special'
$specialToolOutput = Join-Path $bundleRoot 'tools\special-visual'
$dataOutput = Join-Path $bundleRoot 'data'

if (-not (Test-Path -LiteralPath $VisualImagePath)) { throw "Visual test image not found: $VisualImagePath" }
if (-not (Test-Path -LiteralPath $PaddleOcrRoot)) { throw "PaddleOCR model root not found: $PaddleOcrRoot" }
if (-not (Test-Path -LiteralPath $PaddleOcrImagePath)) { throw "PaddleOCR test image not found: $PaddleOcrImagePath" }
if (-not (Test-Path -LiteralPath $SpecialVisualModelRoot)) { throw "Special visual model root not found: $SpecialVisualModelRoot" }
New-Item -ItemType Directory -Force -Path $OutputRoot, $bundleRoot, $modelOutput, $ocrModelOutput, $ocrToolOutput, $specialModelOutput, $specialToolOutput, $dataOutput | Out-Null

$project = Join-Path $repoRoot 'tools\DeploySharp.VisualBenchmark\DeploySharp.VisualBenchmark.csproj'
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Visual benchmark Release build failed.' }
$releaseOutput = Join-Path $repoRoot 'tools\DeploySharp.VisualBenchmark\bin\Release\net10.0'
if (-not (Test-Path -LiteralPath (Join-Path $releaseOutput 'DeploySharp.VisualBenchmark.dll'))) { throw "Visual benchmark output not found: $releaseOutput" }
if ($UpdateOnly) { Get-ChildItem -LiteralPath $releaseOutput -File | Copy-Item -Destination $bundleRoot }
else { Copy-Item -Path (Join-Path $releaseOutput '*') -Destination $bundleRoot -Recurse }

$ocrProject = Join-Path $repoRoot 'tools\DeploySharp.PaddleOcrBenchmark\DeploySharp.PaddleOcrBenchmark.csproj'
dotnet build $ocrProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'PaddleOCR benchmark Release build failed.' }
$ocrReleaseOutput = Join-Path $repoRoot 'tools\DeploySharp.PaddleOcrBenchmark\bin\Release\net10.0'
if (-not (Test-Path -LiteralPath (Join-Path $ocrReleaseOutput 'DeploySharp.PaddleOcrBenchmark.dll'))) { throw "PaddleOCR benchmark output not found: $ocrReleaseOutput" }
Get-ChildItem -LiteralPath $ocrReleaseOutput -File | Copy-Item -Destination $ocrToolOutput
Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\DeploySharp.PaddleOcrBenchmark\Build-TensorRtEngines.ps1') -Destination $ocrToolOutput

$resolvedOcrRoot = (Resolve-Path -LiteralPath $PaddleOcrRoot).Path
foreach ($ocrAsset in Get-ChildItem -LiteralPath $resolvedOcrRoot -Recurse -File | Where-Object { $_.Extension -in @('.onnx', '.txt') }) {
    $relative = $ocrAsset.FullName.Substring($resolvedOcrRoot.Length).TrimStart('\')
    $destination = Join-Path $ocrModelOutput $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $ocrAsset.FullName -Destination $destination
}

$specialProject = Join-Path $repoRoot 'tools\DeploySharp.SpecialVisualBenchmark\DeploySharp.SpecialVisualBenchmark.csproj'
dotnet build $specialProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Special visual benchmark Release build failed.' }
$specialReleaseOutput = Join-Path $repoRoot 'tools\DeploySharp.SpecialVisualBenchmark\bin\Release\net10.0'
if (-not (Test-Path -LiteralPath (Join-Path $specialReleaseOutput 'DeploySharp.SpecialVisualBenchmark.dll'))) { throw "Special visual benchmark output not found: $specialReleaseOutput" }
Get-ChildItem -LiteralPath $specialReleaseOutput -File | Copy-Item -Destination $specialToolOutput

$specialModels = [ordered]@{
    'clip\image-encoder.onnx' = Join-Path $SpecialVisualModelRoot 'clip-vit-base-patch32\clip-image-encoder-opset17.onnx'
    'clip\text-encoder.onnx' = Join-Path $SpecialVisualModelRoot 'clip-vit-base-patch32\clip-text-encoder-opset17.onnx'
    'sam-v1-vit-b\image-encoder.onnx' = Join-Path $SpecialVisualModelRoot 'sam-v1-vit-b\sam_vit_b_image_encoder_opset17.onnx'
    'sam-v1-vit-b\prompt-mask-decoder.onnx' = Join-Path $SpecialVisualModelRoot 'sam-v1-vit-b\sam_vit_b_prompt_mask_decoder_opset17_legacy.onnx'
    'blip-caption-base\vision-encoder.onnx' = Join-Path $SpecialVisualModelRoot 'blip-caption-base\converted-opset17\vision_encoder.onnx'
    'blip-caption-base\language-decoder.onnx' = Join-Path $SpecialVisualModelRoot 'blip-caption-base\converted-opset17\text_decoder_full_prefix.onnx'
    'blip-caption-base\vocab.txt' = Join-Path $SpecialVisualModelRoot 'blip-caption-base\bert-base-uncased-vocab.txt'
}
foreach ($entry in $specialModels.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value)) { throw "Required special visual model asset not found: $($entry.Value)" }
    $destination = Join-Path $specialModelOutput $entry.Key
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $entry.Value -Destination $destination
}

$runtimeDescription = 'existing package private .NET 10 runtime'
if (-not $UpdateOnly) {
    $dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
    $runtimeDirectory = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App') -Directory |
        Where-Object { $_.Name -like '10.*' } |
        Sort-Object { [Version]$_.Name } |
        Select-Object -Last 1
    $hostFxrDirectory = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'host\fxr') -Directory |
        Where-Object { $_.Name -like '10.*' } |
        Sort-Object { [Version]$_.Name } |
        Select-Object -Last 1
    if ($null -eq $runtimeDirectory -or $null -eq $hostFxrDirectory) { throw 'A local .NET 10 runtime and hostfxr are required to build the portable bundle.' }
    $runtimeDescription = "private runtime $($runtimeDirectory.Name) / net10.0"
    $privateDotnet = Join-Path $bundleRoot '.dotnet'
    New-Item -ItemType Directory -Force -Path (Join-Path $privateDotnet 'shared\Microsoft.NETCore.App'), (Join-Path $privateDotnet 'host\fxr') | Out-Null
    Copy-Item -LiteralPath (Join-Path $dotnetRoot 'dotnet.exe') -Destination $privateDotnet
    Copy-Item -LiteralPath $runtimeDirectory.FullName -Destination (Join-Path $privateDotnet 'shared\Microsoft.NETCore.App') -Recurse
    Copy-Item -LiteralPath $hostFxrDirectory.FullName -Destination (Join-Path $privateDotnet 'host\fxr') -Recurse

    foreach ($runtimeName in @('concrt140.dll', 'msvcp140.dll', 'msvcp140_1.dll', 'msvcp140_2.dll', 'vcomp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')) {
        $runtimePath = Join-Path $env:WINDIR "System32\$runtimeName"
        if (Test-Path -LiteralPath $runtimePath) { Copy-Item -LiteralPath $runtimePath -Destination $bundleRoot }
    }
}

function Resolve-ModelAsset {
    param([string]$Source, [string]$DownloadUrl, [string]$Sha256, [string]$CacheName)
    $candidate = $Source
    if (-not (Test-Path -LiteralPath $candidate)) {
        $cacheRoot = Join-Path $OutputRoot '.model-cache'
        New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
        $candidate = Join-Path $cacheRoot $CacheName
        if (-not (Test-Path -LiteralPath $candidate)) {
            $partial = "$candidate.partial"
            & curl.exe --fail --location --retry 5 --retry-all-errors --connect-timeout 15 --speed-time 60 --speed-limit 1024 --output $partial $DownloadUrl
            if ($LASTEXITCODE -ne 0) { throw "Model download failed for '$DownloadUrl' with curl exit code $LASTEXITCODE." }
            Move-Item -LiteralPath $partial -Destination $candidate -Force
        }
    }
    $actual = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Sha256) { throw "Model SHA-256 mismatch for '$candidate': expected $Sha256, actual $actual" }
    return (Resolve-Path -LiteralPath $candidate).Path
}

$models = [ordered]@{
    'yolov5n.onnx' = [pscustomobject]@{ Kind='yolov5n'; Source=(Join-Path $ModelRoot 'yolo\yolov5\yolov5n.onnx'); Url=''; Sha='1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d' }
    'yolov6s.onnx' = [pscustomobject]@{ Kind='yolov6s'; Source=(Join-Path $ModelRoot 'yolo\yolov6s.onnx'); Url=''; Sha='f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6' }
    'yolov7.onnx' = [pscustomobject]@{ Kind='yolov7'; Source=(Join-Path $ModelRoot 'yolo\yolov7.onnx'); Url=''; Sha='8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641' }
    'yolov8n.onnx' = [pscustomobject]@{ Kind='yolov8n'; Source=(Join-Path $ModelRoot 'yolo\yolov8\yolov8n.onnx'); Url=''; Sha='50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4' }
    'yolov9s.onnx' = [pscustomobject]@{ Kind='yolov9s'; Source=(Join-Path $ModelRoot 'yolo\yolov9s.onnx'); Url=''; Sha='e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7' }
    'yolov10n.onnx' = [pscustomobject]@{ Kind='yolov10n'; Source=(Join-Path $ModelRoot 'yolo\yolov10\yolov10n.onnx'); Url=''; Sha='908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81' }
    'yolo11n.onnx' = [pscustomobject]@{ Kind='yolo11n'; Source=(Join-Path $ModelRoot 'yolo\yolov11\yolo11n.onnx'); Url=''; Sha='7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3' }
    'yolo12n.onnx' = [pscustomobject]@{ Kind='yolo12n'; Source=(Join-Path $ModelRoot 'yolo\yolov12\yolo12n.onnx'); Url=''; Sha='9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80' }
    'yolo13n.onnx' = [pscustomobject]@{ Kind='yolo13n'; Source=(Join-Path $ModelRoot 'yolo\yolov13n.onnx'); Url=''; Sha='a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80' }
    'yolo26n.onnx' = [pscustomobject]@{ Kind='yolo26n'; Source=(Join-Path $ModelRoot 'yolo\yolov26\yolo26n.onnx'); Url=''; Sha='bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4' }
    'yolov8s-cls.onnx' = [pscustomobject]@{ Kind='yolov8s-cls'; Source=(Join-Path $ModelRoot 'yolo\yolov8\yolov8s-cls.onnx'); Url=''; Sha='6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee' }
    'yolov5s-seg.onnx' = [pscustomobject]@{ Kind='yolov5s-seg'; Source=(Join-Path $ModelRoot 'yolo\yolov5\yolov5s-seg.onnx'); Url=''; Sha='ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576' }
    'yolov8n-seg.onnx' = [pscustomobject]@{ Kind='yolov8n-seg'; Source=(Join-Path $ModelRoot 'yolo\yolov8\yolov8n-seg.onnx'); Url=''; Sha='986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2' }
    'yolov9c-seg.onnx' = [pscustomobject]@{ Kind='yolov9c-seg'; Source=(Join-Path $ModelRoot 'yolo\yolov9-c-seg.onnx'); Url=''; Sha='2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0' }
    'yolo11s-seg.onnx' = [pscustomobject]@{ Kind='yolo11s-seg'; Source=(Join-Path $ModelRoot 'yolo\yolov11\yolo11s-seg.onnx'); Url=''; Sha='0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48' }
    'yolo26s-seg.onnx' = [pscustomobject]@{ Kind='yolo26s-seg'; Source=(Join-Path $ModelRoot 'yolo\yolov26\yolo26s-seg.onnx'); Url=''; Sha='79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f' }
    'yolov8s-pose.onnx' = [pscustomobject]@{ Kind='yolov8s-pose'; Source=(Join-Path $ModelRoot 'yolo\yolov8\yolov8s-pose.onnx'); Url=''; Sha='253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b' }
    'yolo11s-pose.onnx' = [pscustomobject]@{ Kind='yolo11s-pose'; Source=(Join-Path $ModelRoot 'yolo\yolov11\yolo11s-pose.onnx'); Url=''; Sha='5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5' }
    'yolo26s-pose.onnx' = [pscustomobject]@{ Kind='yolo26s-pose'; Source=(Join-Path $ModelRoot 'yolo\yolov26\yolo26s-pose.onnx'); Url=''; Sha='55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc' }
    'yolov8s-obb.onnx' = [pscustomobject]@{ Kind='yolov8s-obb'; Source=(Join-Path $ModelRoot 'yolo\yolov8\yolov8s-obb.onnx'); Url=''; Sha='2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981' }
    'yolo11s-obb.onnx' = [pscustomobject]@{ Kind='yolo11s-obb'; Source=(Join-Path $ModelRoot 'yolo\yolov11\yolo11s-obb.onnx'); Url=''; Sha='50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0' }
    'yolo26s-obb.onnx' = [pscustomobject]@{ Kind='yolo26s-obb'; Source=(Join-Path $ModelRoot 'yolo\yolov26\yolo26s-obb.onnx'); Url=''; Sha='bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df' }
    'deimv2.onnx' = [pscustomobject]@{ Kind='deimv2'; Source=(Join-Path $ModelRoot 'DEIMv2\DEIMv2.onnx'); Url='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/deim-v2-detect.model.onnx'; Sha='08a6a9052c83ccd356e91f8839dfe7b2e686639b577feb7f0b7b204f7f2969cc' }
    'ppyoloe.onnx' = [pscustomobject]@{ Kind='ppyoloe'; Source=(Join-Path $ModelRoot 'ppyoloe\ppyoloe_plus_crn_l_80e_coco.onnx'); Url=''; Sha='68866d9841e41f6637d4a1c13db6c70a42c9f0367c79870b0a8a9e9df32b8504' }
    'rfdetr.onnx' = [pscustomobject]@{ Kind='rfdetr'; Source=(Join-Path $ModelRoot 'rf-detr\rf-detr.onnx'); Url=''; Sha='b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769' }
    'rfdetr-seg.onnx' = [pscustomobject]@{ Kind='rfdetr-seg'; Source=(Join-Path $ModelRoot 'rf-detr\rf-detr-seg.onnx'); Url=''; Sha='6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35' }
    'rtdetr-decoded-ir.xml' = [pscustomobject]@{ Kind='rtdetr-decoded-ir'; Source=(Join-Path $ModelRoot 'RT-DETR\catalog\rtdetr-decoded.xml'); Url='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/rt-detr-r50vd-decoded-vector-ir.model.xml'; Sha='9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d' }
    'rtdetr-decoded-ir.bin' = [pscustomobject]@{ Kind=$null; Source=(Join-Path $ModelRoot 'RT-DETR\catalog\rtdetr-decoded.bin'); Url='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/rt-detr-r50vd-decoded-vector-ir.model.bin'; Sha='c4f2ea6021314c23d691e5d6911da0804191202d049f3927cfa242f181600455' }
    'rtdetr-decoded-onnx.onnx' = [pscustomobject]@{ Kind='rtdetr-decoded-onnx'; Source=(Join-Path $ModelRoot 'RT-DETR\catalog\rtdetr-decoded.onnx'); Url='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/rt-detr-r50vd-decoded-vector-onnx.model.onnx'; Sha='a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db' }
    'rtdetr-raw.onnx' = [pscustomobject]@{ Kind='rtdetr-raw'; Source=(Join-Path $ModelRoot 'RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx'); Url=''; Sha='544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad' }
    'padim.onnx' = [pscustomobject]@{ Kind='padim'; Source=(Join-Path $ModelRoot 'anomalib\Padim\model\padim.onnx'); Url=''; Sha='bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff' }
    'bria-rmbg-1.4.onnx' = [pscustomobject]@{ Kind='rmbg14'; Source=(Join-Path $ModelRoot 'RMBG\bria-rmbg-1.4.onnx'); Url=''; Sha='8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f' }
    'rmbg-2.0.onnx' = [pscustomobject]@{ Kind='rmbg20'; Source=(Join-Path $ModelRoot 'RMBG\RMBG-2.0.onnx'); Url=''; Sha='5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958' }
    'rmbg-2.0-int8.onnx' = [pscustomobject]@{ Kind='rmbg20-int8'; Source=(Join-Path $ModelRoot 'RMBG\RMBG-2.0_quantized.onnx'); Url=''; Sha='fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420' }
}
foreach ($entry in $models.GetEnumerator()) {
    $source = Resolve-ModelAsset -Source $entry.Value.Source -DownloadUrl $entry.Value.Url -Sha256 $entry.Value.Sha -CacheName $entry.Key
    Copy-Item -LiteralPath $source -Destination (Join-Path $modelOutput $entry.Key)
}

Copy-Item -LiteralPath $VisualImagePath -Destination (Join-Path $dataOutput 'bus.jpg')
Copy-Item -LiteralPath $PaddleOcrImagePath -Destination (Join-Path $dataOutput 'ocr-demo.jpg')
Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\DeploySharp.VisualBenchmark\Run-DeviceBenchmark.ps1') -Destination $bundleRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\DeploySharp.VisualBenchmark\DEVICE-BENCHMARK-README.md') -Destination (Join-Path $bundleRoot 'README.md')

if (-not [string]::IsNullOrWhiteSpace($TensorRtBridgePath)) {
    if (-not (Test-Path -LiteralPath $TensorRtBridgePath)) { throw "TensorRT bridge not found: $TensorRtBridgePath" }
    $bridgeOutput = Join-Path $bundleRoot 'vendor\tensorrt-11.0\bin'
    New-Item -ItemType Directory -Force -Path $bridgeOutput | Out-Null
    Copy-Item -LiteralPath $TensorRtBridgePath -Destination (Join-Path $bridgeOutput 'jyppxtrtbridge.dll')
}

foreach ($runtime in @(
    [pscustomobject]@{ Source = $CudaRuntimeBinPath; Destination = 'vendor\cuda-12.9\bin'; Name = 'CUDA runtime' },
    [pscustomobject]@{ Source = $Cuda13RuntimeBinPath; Destination = 'vendor\cuda-13.2\bin'; Name = 'ONNX Runtime CUDA libraries' },
    [pscustomobject]@{ Source = $CudnnRuntimeBinPath; Destination = 'vendor\cudnn-9.22\bin'; Name = 'cuDNN runtime' },
    [pscustomobject]@{ Source = $TensorRtRuntimeBinPath; Destination = 'vendor\tensorrt-11.0\bin'; Name = 'TensorRT runtime' }
)) {
    if ([string]::IsNullOrWhiteSpace($runtime.Source)) { continue }
    if (-not (Test-Path -LiteralPath $runtime.Source)) { throw "$($runtime.Name) directory not found: $($runtime.Source)" }
    $destination = Join-Path $bundleRoot $runtime.Destination
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $runtime.Source '*') -Destination $destination -Recurse -Force
}

$manifestEntries = foreach ($entry in $models.GetEnumerator()) {
    $file = Get-Item -LiteralPath (Join-Path $modelOutput $entry.Key)
    [ordered]@{
        name = if ([string]::IsNullOrWhiteSpace($entry.Value.Kind)) { [IO.Path]::GetFileNameWithoutExtension($entry.Key) } else { $entry.Value.Kind }
        relative_path = "models/visual/$($entry.Key)"
        size_bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$ocrManifestEntries = foreach ($file in Get-ChildItem -LiteralPath $ocrModelOutput -Recurse -File) {
    [ordered]@{
        relative_path = $file.FullName.Substring($bundleRoot.Length).TrimStart('\').Replace('\', '/')
        size_bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$specialManifestEntries = foreach ($file in Get-ChildItem -LiteralPath $specialModelOutput -Recurse -File) {
    [ordered]@{
        relative_path = $file.FullName.Substring($bundleRoot.Length).TrimStart('\').Replace('\', '/')
        size_bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest = [ordered]@{
    schema_version = 1
    package_name = $bundleName
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    target = 'win-x64'
    dotnet = $runtimeDescription
    tensor_rt_bridge_api = if ([string]::IsNullOrWhiteSpace($TensorRtBridgePath)) { $null } else { '11' }
    bundled_runtimes = [ordered]@{ cuda = '12.9'; onnxruntime_cuda = '13.2'; cudnn = '9.22.0.52'; tensorrt = '11.0.0.114' }
    models = @($manifestEntries)
    paddleocr_models = @($ocrManifestEntries)
    special_visual_models = @($specialManifestEntries)
    test_image = [ordered]@{
        relative_path = 'data/bus.jpg'
        sha256 = (Get-FileHash -LiteralPath (Join-Path $dataOutput 'bus.jpg') -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    paddleocr_test_image = [ordered]@{
        relative_path = 'data/ocr-demo.jpg'
        sha256 = (Get-FileHash -LiteralPath (Join-Path $dataOutput 'ocr-demo.jpg') -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $bundleRoot 'models\manifest.json') -Encoding utf8

$archivePath = $null
if (-not $SkipArchive) {
    $archivePath = Join-Path $OutputRoot "$bundleName.zip"
    Push-Location $OutputRoot
    try {
        & tar.exe -a -c -f $archivePath $bundleName
        if ($LASTEXITCODE -ne 0) { throw 'Device benchmark archive creation failed.' }
    }
    finally {
        Pop-Location
    }
}

$bundleBytes = (Get-ChildItem -LiteralPath $bundleRoot -Recurse -File | Measure-Object -Property Length -Sum).Sum
[ordered]@{
    bundle = $bundleRoot
    archive = $archivePath
    file_count = (Get-ChildItem -LiteralPath $bundleRoot -Recurse -File).Count
    size_bytes = $bundleBytes
} | ConvertTo-Json
