[CmdletBinding()]
param(
    [string]$ModelRoot = $(if ($env:DEPLOYSHARP_PADDLEOCR_ROOT) { $env:DEPLOYSHARP_PADDLEOCR_ROOT } else { 'E:\Model\paddleocr' }),
    [string]$OutputRoot = 'artifacts\local-model-benchmarks\paddleocr-trt11-rebuilt',
    [string]$TensorRtRoot = $(if ($env:JYPPX_TENSORRT_ROOT) { $env:JYPPX_TENSORRT_ROOT } else { 'D:\Program Files\TensorRT-11.0.0.114-cu12' }),
    [int]$BuilderOptimizationLevel = 3,
    [int]$StageOptBatch = 4,
    [int]$StageMaxBatch = 8,
    [switch]$Fp16
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $ModelRoot).Path
$trtexec = Join-Path $TensorRtRoot 'bin\trtexec.exe'
if (-not (Test-Path -LiteralPath $trtexec)) {
    throw "trtexec.exe was not found under '$TensorRtRoot'."
}

$cudaRoot = if ($env:JYPPX_CUDA_ROOT) { $env:JYPPX_CUDA_ROOT } else { 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' }
$cudnnRoot = if ($env:JYPPX_CUDNN_ROOT) { $env:JYPPX_CUDNN_ROOT } else { 'D:\Program Files\cuDNN-9.22.0-cuda12.9' }
$env:PATH = "$TensorRtRoot\bin;$TensorRtRoot\lib;$cudaRoot\bin;$cudnnRoot\bin;$env:PATH"

if ($Fp16) {
    $trtexecHelp = (& $trtexec --help 2>&1 | Out-String)
    if ($trtexecHelp -notmatch '(?m)^\s*--fp16(?:\s|$)') {
        throw "This trtexec does not expose --fp16. TensorRT 11 uses strongly typed networks, so precision must be encoded in the ONNX graph before engine building. No FP16 engine was built."
    }
}

$models = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.onnx' -File
if ($models.Count -eq 0) {
    throw "No ONNX models were found under '$sourceRoot'."
}
if ($StageOptBatch -lt 1 -or $StageMaxBatch -lt $StageOptBatch) {
    throw "StageOptBatch must be >= 1 and StageMaxBatch must be >= StageOptBatch."
}
if ($BuilderOptimizationLevel -lt 0 -or $BuilderOptimizationLevel -gt 5) {
    throw "BuilderOptimizationLevel must be between 0 and 5."
}

foreach ($asset in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.txt' -File) {
    $assetRelative = $asset.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $assetDestination = Join-Path $OutputRoot $assetRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $assetDestination) | Out-Null
    Copy-Item -LiteralPath $asset.FullName -Destination $assetDestination -Force
}

foreach ($model in $models) {
    $relative = $model.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $destination = Join-Path $OutputRoot $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $model.FullName -Destination $destination -Force

    $file = [IO.Path]::GetFileNameWithoutExtension($model.Name).ToLowerInvariant()
    $version = if ($model.FullName -match 'PP-OCRv4') { 'v4' } elseif ($model.FullName -match 'PP-OCRv5') { 'v5' } elseif ($model.FullName -match 'PP-OCRv6') { 'v6' } else { 'unknown' }
    $role = if ($file.Contains('cls')) { 'cls' } elseif ($file.Contains('rec')) { 'rec' } elseif ($file.Contains('det')) { 'det' } else { 'unknown' }
    if ($version -eq 'unknown' -or $role -eq 'unknown') {
        Write-Warning "Skipping unrecognised model '$relative'."
        continue
    }

    if ($role -eq 'cls') {
        $height = if ($version -eq 'v4') { 48 } else { 80 }
        $width = if ($version -eq 'v4') { 192 } else { 160 }
    } elseif ($role -eq 'rec') {
        $height = 48
        $width = 320
    } else {
        $height = 736
        $width = 736
    }

    $minShape = "x:1x3x${height}x${width}"
    $optBatch = if ($role -eq 'det') { 1 } else { $StageOptBatch }
    $maxBatch = if ($role -eq 'det') { 1 } else { $StageMaxBatch }
    $optShape = "x:${optBatch}x3x${height}x${width}"
    $maxShape = "x:${maxBatch}x3x${height}x${width}"
    $engine = "$destination.engine"
    $precision = if ($Fp16) { 'fp16-command-line' } else { 'graph-defined' }
    Write-Host "TRT_ENGINE_BUILD model=$relative min=$minShape opt=$optShape max=$maxShape precision=$precision output=$engine"
    $arguments = @(
        "--onnx=$destination",
        "--saveEngine=$engine",
        "--minShapes=$minShape",
        "--optShapes=$optShape",
        "--maxShapes=$maxShape",
        "--builderOptimizationLevel=$BuilderOptimizationLevel",
        '--skipInference'
    )
    if ($Fp16) { $arguments += '--fp16' }
    & $trtexec @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "TensorRT engine build failed for '$relative' with exit code $LASTEXITCODE."
    }
}

Write-Host "TRT_ENGINE_BUILD_COMPLETE output=$OutputRoot"
