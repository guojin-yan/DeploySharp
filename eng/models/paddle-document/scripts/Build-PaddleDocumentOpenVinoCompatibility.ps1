[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\PaddleDocument\onnx',
    [string]$OutputRoot = 'E:\Model\PaddleDocument\onnx-normalized',
    [string]$Python = 'python',
    [string]$ManifestPath = '',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'normalize_loop_parameters.py'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "The normalizer script was not found: $scriptPath"
}

$pythonCommand = Get-Command $Python -ErrorAction SilentlyContinue
if (-not $pythonCommand) {
    throw "Python was not found at '$Python'. Install a Python environment with the onnx package."
}

$sourceRoot = (Resolve-Path -LiteralPath $ModelRoot -ErrorAction Stop).Path
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $outputRootFull 'slanext-openvino-compatibility.json'
}

$entries = @(
    [ordered]@{
        modelId = 'paddle-table/slanext-wired'
        compatibilityId = 'paddle-table/slanext-wired-openvino-compat'
        sourceFile = 'slanext-wired.onnx'
        derivedFile = 'slanext-wired-local-parameters.onnx'
        releaseAssetName = 'slanext-wired-openvino-compat.onnx'
    },
    [ordered]@{
        modelId = 'paddle-table/slanext-wireless'
        compatibilityId = 'paddle-table/slanext-wireless-openvino-compat'
        sourceFile = 'slanext-wireless.onnx'
        derivedFile = 'slanext-wireless-local-parameters.onnx'
        releaseAssetName = 'slanext-wireless-openvino-compat.onnx'
    }
)

$artifacts = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $entries) {
    $sourcePath = Join-Path $sourceRoot $entry.sourceFile
    $derivedPath = Join-Path $outputRootFull $entry.derivedFile
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Missing source ONNX: $sourcePath"
    }
    if ((Test-Path -LiteralPath $derivedPath -PathType Leaf) -and -not $Force) {
        throw "Derived output already exists. Choose another OutputRoot or pass -Force: $derivedPath"
    }
    if (Test-Path -LiteralPath $derivedPath -PathType Leaf) {
        Remove-Item -LiteralPath $derivedPath -Force
    }

    Write-Host "SLANEXT_COMPAT_BUILD model=$($entry.modelId) source=$sourcePath output=$derivedPath"
    $lines = @(& $pythonCommand.Source $scriptPath $sourcePath $derivedPath 2>&1)
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $derivedPath -PathType Leaf)) {
        throw "SLANeXt compatibility conversion failed for '$($entry.modelId)' with exit code $LASTEXITCODE."
    }
    $jsonLine = $lines | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace([string]$jsonLine)) {
        throw "The normalizer did not emit its JSON result for '$($entry.modelId)'. Output: $($lines -join ' ')"
    }
    $normalized = [string]$jsonLine | ConvertFrom-Json
    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $derivedHash = (Get-FileHash -LiteralPath $derivedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sourceHash -ne [string]$normalized.source_sha256 -or $derivedHash -ne [string]$normalized.output_sha256) {
        throw "The normalizer hash output does not match the files for '$($entry.modelId)'."
    }
    $artifacts.Add([ordered]@{
        modelId = $entry.modelId
        compatibilityId = $entry.compatibilityId
        sourceFile = $entry.sourceFile
        sourcePath = $sourcePath
        sourceSize = (Get-Item -LiteralPath $sourcePath).Length
        sourceSha256 = $sourceHash
        derivedFile = $entry.derivedFile
        derivedPath = $derivedPath
        derivedSize = (Get-Item -LiteralPath $derivedPath).Length
        derivedSha256 = $derivedHash
        releaseAssetName = $entry.releaseAssetName
        transformation = 'alpha-rename Loop-body formal parameters only; source graph and weights are unchanged'
        openvinoStatus = 'verified-by-ORT-and-decoder-parity'
        releaseStatus = 'planned-separate-asset'
    })
}

$manifest = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-openvino-compatibility-v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    generator = 'normalize_loop_parameters.py'
    generatorPath = $scriptPath
    modelRoot = $sourceRoot
    outputRoot = $outputRootFull
    sourcePolicy = 'preserve-official-release-onnx-and-publish-derived-compatibility-asset-separately'
    importerBoundary = 'OpenVINO current Loop importer rejects the original graphs; derived graphs are the compatibility input.'
    artifacts = $artifacts
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
Write-Host "SLANEXT_COMPAT_MANIFEST path=$ManifestPath"
$manifest | ConvertTo-Json -Depth 12
