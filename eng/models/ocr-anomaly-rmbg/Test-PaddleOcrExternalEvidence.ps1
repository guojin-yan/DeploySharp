[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\ocr\ppocrv5',
    [string]$ImagePath = 'E:\Data\image\bus.jpg',
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'

$files = [ordered]@{
    'DEPLOYSHARP_STAGE20_OCR_DET_MODEL' = @{ Name = 'PP-OCRv5_mobile_det_onnx.onnx'; Sha256 = '1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039' }
    'DEPLOYSHARP_STAGE20_OCR_REC_MODEL' = @{ Name = 'PP-OCRv5_mobile_rec_onnx.onnx'; Sha256 = 'f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9' }
    'DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL' = @{ Name = 'PP-OCRv5_mobile_cls_onnx.onnx'; Sha256 = 'dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2' }
    'DEPLOYSHARP_STAGE20_OCR_DICT' = @{ Name = 'ppocrv5_dict.txt'; Sha256 = 'd1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b' }
}

if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "The validation image does not exist: $ImagePath"
}

$imageFullPath = [System.IO.Path]::GetFullPath($ImagePath)
$imageSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $imageFullPath).Hash.ToLowerInvariant()
Write-Output "image=$imageFullPath;sha256=$imageSha"

foreach ($variable in $files.Keys) {
    $spec = $files[$variable]
    $path = Join-Path -Path $ModelRoot -ChildPath $spec.Name
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

Write-Output "DEPLOYSHARP_PADDLE_OCR_EXTERNAL_EVIDENCE_OK imageSha256=$imageSha;redistribution=blocked-external-only"
