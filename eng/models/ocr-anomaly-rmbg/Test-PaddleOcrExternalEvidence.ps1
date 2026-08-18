[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\ocr\ppocrv5',
    [string]$ImagePath = '',
    [string]$RecognitionGoldenImagePath = '',
    [string]$ClassificationGoldenImagePath = '',
    [string]$ServerRecognitionModelPath = '',
    [string]$ServerClassificationModelPath = '',
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ServerRecognitionModelPath)) {
    $ServerRecognitionModelPath = Join-Path $ModelRoot 'PP-OCRv5_server_rec_onnx.onnx'
}
if ([string]::IsNullOrWhiteSpace($ServerClassificationModelPath)) {
    $ServerClassificationModelPath = Join-Path $ModelRoot 'PP-OCRv5_server_cls_onnx.onnx'
    if (-not (Test-Path -LiteralPath $ServerClassificationModelPath -PathType Leaf)) {
        $siblingRoot = Join-Path (Split-Path -Parent $ModelRoot) 'ppocrv5-1'
        $siblingCandidate = Join-Path $siblingRoot 'PP-OCRv5_server_cls_onnx.onnx'
        if (Test-Path -LiteralPath $siblingCandidate -PathType Leaf) { $ServerClassificationModelPath = $siblingCandidate }
    }
}

$files = [ordered]@{
    'DEPLOYSHARP_STAGE20_OCR_DET_MODEL' = @{ Name = 'PP-OCRv5_mobile_det_onnx.onnx'; Sha256 = '1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039' }
    'DEPLOYSHARP_STAGE20_SERVER_DET_MODEL' = @{ Name = 'PP-OCRv5_server_det_onnx.onnx'; Sha256 = '9a910baffbefb807ff2f7bfaa72910e3e470bd17014d798386d87bb46f442839' }
    'DEPLOYSHARP_STAGE20_OCR_REC_MODEL' = @{ Name = 'PP-OCRv5_mobile_rec_onnx.onnx'; Sha256 = 'f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9' }
    'DEPLOYSHARP_STAGE20_SERVER_REC_MODEL' = @{ Name = 'PP-OCRv5_server_rec_onnx.onnx'; Path = $ServerRecognitionModelPath; Sha256 = '5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7' }
    'DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL' = @{ Name = 'PP-OCRv5_mobile_cls_onnx.onnx'; Sha256 = 'dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2' }
    'DEPLOYSHARP_STAGE20_SERVER_CLS_MODEL' = @{ Name = 'PP-OCRv5_server_cls_onnx.onnx'; Path = $ServerClassificationModelPath; Sha256 = 'd874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f' }
    'DEPLOYSHARP_STAGE20_OCR_DICT' = @{ Name = 'ppocrv5_dict.txt'; Sha256 = 'd1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b' }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    $ImagePath = Join-Path $repoRoot 'artifacts\paddleocr-reference\images\det.png'
}
if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "The validation image does not exist: $ImagePath"
}

$imageFullPath = [System.IO.Path]::GetFullPath($ImagePath)
$imageSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $imageFullPath).Hash.ToLowerInvariant()
Write-Output "image=$imageFullPath;sha256=$imageSha"

if ([string]::IsNullOrWhiteSpace($RecognitionGoldenImagePath)) {
    $RecognitionGoldenImagePath = Join-Path $repoRoot 'artifacts\paddleocr-reference\images\rec.png'
}
if ([string]::IsNullOrWhiteSpace($ClassificationGoldenImagePath)) {
    $ClassificationGoldenImagePath = Join-Path $repoRoot 'artifacts\paddleocr-reference\images\cls.jpg'
}
$goldenImages = [ordered]@{
    'DEPLOYSHARP_STAGE20_REC_GOLDEN_IMAGE' = @{ Path = $RecognitionGoldenImagePath; Sha256 = '5362ba97741413494c507237b5096ef09ed575a501c4d9e68bfeffe17528a6ad' }
    'DEPLOYSHARP_STAGE20_CLS_GOLDEN_IMAGE' = @{ Path = $ClassificationGoldenImagePath; Sha256 = '872200f57a1408e7aab2856d5f2c687b3a937805e0c4ff74bd7de21df1f742b9' }
}
foreach ($variable in $goldenImages.Keys) {
    $spec = $goldenImages[$variable]
    if (-not (Test-Path -LiteralPath $spec.Path -PathType Leaf)) {
        throw "The official PaddleOCR golden image does not exist: $($spec.Path)"
    }

    $fullPath = [System.IO.Path]::GetFullPath($spec.Path)
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
    if ($actual -ne $spec.Sha256) {
        throw "SHA256 mismatch for $fullPath. Expected $($spec.Sha256), got $actual."
    }

    Set-Item -Path ("Env:" + $variable) -Value $fullPath
    Write-Output "$variable=$fullPath;sha256=$actual"
}

foreach ($variable in $files.Keys) {
    $spec = $files[$variable]
    $path = if ($spec.ContainsKey('Path')) { $spec.Path } else { Join-Path -Path $ModelRoot -ChildPath $spec.Name }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The external PaddleOCR file does not exist: $path"
    }

    $fullPath = [System.IO.Path]::GetFullPath($path)
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
    if ($actual -ne $spec.Sha256) {
        throw "SHA256 mismatch for $fullPath. Expected $($spec.Sha256), got $actual."
    }

    Set-Item -Path ("Env:" + $variable) -Value $fullPath
    Write-Output "$variable=$fullPath;sha256=$actual"
}

Set-Item -Path 'Env:DEPLOYSHARP_STAGE20_IMAGE' -Value $imageFullPath
Set-Item -Path 'Env:DEPLOYSHARP_STAGE20_RUN_EXTERNAL' -Value '1'

$testArguments = @(
    'test',
    'tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj',
    '-c',
    'Release',
    '--filter',
    'FullyQualifiedName~Stage20PaddleOcrThreeModelParityTests'
)
if (-not $Restore) { $testArguments += '--no-restore' }

& dotnet @testArguments
if ($LASTEXITCODE -ne 0) {
    throw "The PaddleOCR external parity test failed with exit code $LASTEXITCODE."
}

Write-Output "DEPLOYSHARP_PADDLE_OCR_EXTERNAL_EVIDENCE_OK imageSha256=$imageSha;assetSource=public-release"
