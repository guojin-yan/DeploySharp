[CmdletBinding()]
param(
    [string]$ModelRoot = $(if ($env:DEPLOYSHARP_PADDLEOCR_ROOT) { $env:DEPLOYSHARP_PADDLEOCR_ROOT } else { 'E:\Model\paddleocr' }),
    [string]$OutputRoot = 'artifacts\local-model-benchmarks\paddleocr-trt10-rebuilt',
    [string]$TensorRtRoot = $(if ($env:JYPPX_TENSORRT_ROOT) { $env:JYPPX_TENSORRT_ROOT } else { '' }),
    [int]$BuilderOptimizationLevel = 3,
    [int]$StageOptBatch = 4,
    [int]$StageMaxBatch = 8,
    [int]$RecognitionMinWidth = 48,
    [int]$RecognitionOptWidth = 160,
    [int]$RecognitionMaxWidth = 320,
    [switch]$Fp16
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $ModelRoot).Path
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
$inPlace = [string]::Equals($sourceRoot.TrimEnd('\\'), $outputRootFull.TrimEnd('\\'), [StringComparison]::OrdinalIgnoreCase)
if ([string]::IsNullOrWhiteSpace($TensorRtRoot)) {
    throw "TensorRtRoot is required. Set JYPPX_TENSORRT_ROOT or pass -TensorRtRoot to the TensorRT 10.10 installation."
}
$trtexec = Join-Path $TensorRtRoot 'bin\trtexec.exe'
if (-not (Test-Path -LiteralPath $trtexec)) {
    throw "trtexec.exe was not found under '$TensorRtRoot'."
}

$cudaRoot = if ($env:JYPPX_CUDA_ROOT) { $env:JYPPX_CUDA_ROOT } else { '' }
$cudnnRoot = if ($env:JYPPX_CUDNN_ROOT) { $env:JYPPX_CUDNN_ROOT } else { '' }
if ([string]::IsNullOrWhiteSpace($cudaRoot) -or [string]::IsNullOrWhiteSpace($cudnnRoot)) {
    throw "JYPPX_CUDA_ROOT and JYPPX_CUDNN_ROOT must point to the target CUDA/cuDNN installation."
}
$env:PATH = "$TensorRtRoot\bin;$TensorRtRoot\lib;$cudaRoot\bin;$cudnnRoot\bin;$env:PATH"

if ($Fp16) {
    $trtexecHelp = (& $trtexec --help 2>&1 | Out-String)
    if ($trtexecHelp -notmatch '(?m)^\s*--fp16(?:\s|$)') {
        throw "This trtexec does not expose --fp16. Precision must be encoded in the ONNX graph before engine building. No FP16 engine was built."
    }
}

$trtexecHelp = (& $trtexec --help 2>&1 | Out-String)
$supportsBuilderOptimizationLevel = $trtexecHelp -match '(?m)^\s*--builderOptimizationLevel(?:[=\s]|$)'

$models = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.onnx' -File
if ($models.Count -eq 0) {
    throw "No ONNX models were found under '$sourceRoot'."
}
if ($StageOptBatch -lt 1 -or $StageMaxBatch -lt $StageOptBatch) {
    throw "StageOptBatch must be >= 1 and StageMaxBatch must be >= StageOptBatch."
}
if ($RecognitionMinWidth -lt 1 -or $RecognitionOptWidth -lt $RecognitionMinWidth -or $RecognitionMaxWidth -lt $RecognitionOptWidth) {
    throw "RecognitionMinWidth, RecognitionOptWidth and RecognitionMaxWidth must be positive and ordered."
}
if ($BuilderOptimizationLevel -lt 0 -or $BuilderOptimizationLevel -gt 5) {
    throw "BuilderOptimizationLevel must be between 0 and 5."
}

foreach ($asset in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.txt' -File) {
    $assetRelative = $asset.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $assetDestination = Join-Path $outputRootFull $assetRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $assetDestination) | Out-Null
    if (-not $inPlace) { Copy-Item -LiteralPath $asset.FullName -Destination $assetDestination -Force }
}

foreach ($model in $models) {
    $relative = $model.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $destination = Join-Path $outputRootFull $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    if (-not $inPlace) { Copy-Item -LiteralPath $model.FullName -Destination $destination -Force }

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
        $minWidth = $width
        $optWidth = $width
        $maxWidth = $width
    } elseif ($role -eq 'rec') {
        $height = 48
        $width = $RecognitionMaxWidth
        $minWidth = $RecognitionMinWidth
        $optWidth = $RecognitionOptWidth
        $maxWidth = $RecognitionMaxWidth
    } else {
        $height = 736
        $width = 736
        $minWidth = $width
        $optWidth = $width
        $maxWidth = $width
    }

    $minShape = "x:1x3x${height}x${minWidth}"
    $optBatch = if ($role -eq 'det') { 1 } else { $StageOptBatch }
    $maxBatch = if ($role -eq 'det') { 1 } else { $StageMaxBatch }
    $optShape = "x:${optBatch}x3x${height}x${optWidth}"
    $maxShape = "x:${maxBatch}x3x${height}x${maxWidth}"
    $engine = "$destination.engine"
    $precision = if ($Fp16) { 'fp16-command-line' } else { 'graph-defined' }
    Write-Host "TRT_ENGINE_BUILD model=$relative min=$minShape opt=$optShape max=$maxShape precision=$precision output=$engine"
    $arguments = @(
        "--onnx=$destination",
        "--saveEngine=$engine",
        "--minShapes=$minShape",
        "--optShapes=$optShape",
        "--maxShapes=$maxShape",
        '--skipInference'
    )
    if ($supportsBuilderOptimizationLevel) { $arguments += "--builderOptimizationLevel=$BuilderOptimizationLevel" }
    if ($Fp16) { $arguments += '--fp16' }
    & $trtexec @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "TensorRT engine build failed for '$relative' with exit code $LASTEXITCODE."
    }
}

Write-Host "TRT_ENGINE_BUILD_COMPLETE output=$OutputRoot"
