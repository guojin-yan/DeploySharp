[CmdletBinding()]
param(
    [string]$TensorRtRoot,
    [ValidateSet('8', '10', '11')]
    [string]$TensorRtApi,
    [string]$CudaRoot,
    [string]$CudnnRoot,
    [string]$BridgePath,
    [ValidateRange(1, 100)]
    [int]$LayoutWarmup = 5,
    [ValidateRange(1, 1000)]
    [int]$LayoutIterations = 50,
    [ValidateRange(1, 100)]
    [int]$TableWarmup = 5,
    [ValidateRange(1, 1000)]
    [int]$TableIterations = 50,
    [ValidateRange(1, 100)]
    [int]$SealWarmup = 5,
    [ValidateRange(1, 1000)]
    [int]$SealIterations = 50,
    [ValidateRange(1, 100)]
    [int]$ServerSealWarmup = 5,
    [ValidateRange(1, 1000)]
    [int]$ServerSealIterations = 50,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$project = Join-Path $repoRoot 'tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj'

function Find-TensorRtRoot {
    param([string]$RequestedRoot, [string]$RequestedApi)

    $roots = @()
    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) { $roots += $RequestedRoot }
    if (-not [string]::IsNullOrWhiteSpace($env:JYPPX_TENSORRT_ROOT)) { $roots += $env:JYPPX_TENSORRT_ROOT }
    $roots += @('D:\TensorRt', 'C:\TensorRT', 'C:\Program Files\NVIDIA GPU Computing Toolkit\TensorRT', 'D:\Program Files')

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($root in ($roots | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } | Select-Object -Unique)) {
        $trtexec = Get-ChildItem -LiteralPath $root -Recurse -File -Filter 'trtexec.exe' -ErrorAction SilentlyContinue
        foreach ($item in $trtexec) {
            $bin = Split-Path -Parent $item.FullName
            $candidate = Split-Path -Parent $bin
            if ($candidates -notcontains $candidate) { $candidates.Add($candidate) }
        }
    }
    if ($candidates.Count -eq 0) { throw 'No trtexec.exe was found. Pass -TensorRtRoot explicitly.' }

    if (-not [string]::IsNullOrWhiteSpace($RequestedApi)) {
        $matching = $candidates | Where-Object { $_ -match ('TensorRT-' + [regex]::Escape($RequestedApi) + '(?:\.|-|$)') }
        if ($matching.Count -gt 0) { return $matching | Select-Object -First 1 }
    }

    # Prefer the newest TensorRT 11 installation because the checked-in bridge
    # used by this repository targets API 11. An explicit root/API still wins.
    $preferred = $candidates | Where-Object { $_ -match 'TensorRT-11(?:\.|-|$)' } | Select-Object -First 1
    if ($null -ne $preferred) { return $preferred }
    return $candidates | Select-Object -First 1
}

function Find-CudnnRoot {
    param([string]$RequestedRoot)
    $roots = @($RequestedRoot, $env:JYPPX_CUDNN_ROOT, 'D:\Program Files\cuDNN-9.22.0-cuda12.9') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } |
        Select-Object -Unique
    foreach ($root in $roots) {
        if (Get-ChildItem -LiteralPath $root -Recurse -File -Filter 'cudnn*.dll' -ErrorAction SilentlyContinue | Select-Object -First 1) { return (Resolve-Path $root).Path }
    }
    throw 'No cuDNN root containing cudnn*.dll was found. Pass -CudnnRoot explicitly.'
}

$resolvedTensorRtRoot = Find-TensorRtRoot $TensorRtRoot $TensorRtApi
if ([string]::IsNullOrWhiteSpace($TensorRtApi)) {
    if ($resolvedTensorRtRoot -match 'TensorRT-(\d+)') { $TensorRtApi = $Matches[1] } else { $TensorRtApi = '11' }
}
if ([string]::IsNullOrWhiteSpace($CudaRoot)) { $CudaRoot = $env:JYPPX_CUDA_ROOT }
if ([string]::IsNullOrWhiteSpace($CudaRoot) -and (Test-Path 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9')) { $CudaRoot = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' }
if ([string]::IsNullOrWhiteSpace($CudaRoot) -or -not (Test-Path -LiteralPath $CudaRoot)) { throw 'No CUDA root was found. Pass -CudaRoot explicitly.' }
$resolvedCudnnRoot = Find-CudnnRoot $CudnnRoot

if ([string]::IsNullOrWhiteSpace($BridgePath)) { $BridgePath = $env:JYPPX_NATIVE_BRIDGE_PATH }
if ([string]::IsNullOrWhiteSpace($BridgePath) -and $TensorRtApi -eq '11') {
    $BridgePath = Join-Path $repoRoot 'artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll'
}
if ([string]::IsNullOrWhiteSpace($BridgePath) -or -not (Test-Path -LiteralPath $BridgePath)) {
    throw "No bridge matching TensorRT API $TensorRtApi was found. Pass -BridgePath explicitly; vendor nvinfer.dll alone is not the DeploySharp bridge."
}

$env:JYPPX_TENSORRT_ROOT = (Resolve-Path $resolvedTensorRtRoot).Path
$env:JYPPX_CUDA_ROOT = (Resolve-Path $CudaRoot).Path
$env:JYPPX_CUDNN_ROOT = (Resolve-Path $resolvedCudnnRoot).Path
$env:JYPPX_NATIVE_BRIDGE_PATH = (Resolve-Path $BridgePath).Path
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = $TensorRtApi
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_WARMUP = $LayoutWarmup.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_ITERATIONS = $LayoutIterations.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_WARMUP = $TableWarmup.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_ITERATIONS = $TableIterations.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_WARMUP = $SealWarmup.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_ITERATIONS = $SealIterations.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_WARMUP = $ServerSealWarmup.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_ITERATIONS = $ServerSealIterations.ToString([Globalization.CultureInfo]::InvariantCulture)
$trtBin = Join-Path $env:JYPPX_TENSORRT_ROOT 'bin'
$trtLib = Join-Path $env:JYPPX_TENSORRT_ROOT 'lib'
$cudaBin = Join-Path $env:JYPPX_CUDA_ROOT 'bin'
$cudnnBin = Join-Path $env:JYPPX_CUDNN_ROOT 'bin'
$bridgeDir = Split-Path -Parent $env:JYPPX_NATIVE_BRIDGE_PATH
$env:PATH = "$bridgeDir;$trtBin;$trtLib;$cudaBin;$cudnnBin;$env:PATH"

Write-Host "TensorRT root : $env:JYPPX_TENSORRT_ROOT"
Write-Host "TensorRT API  : $TensorRtApi"
Write-Host "CUDA root     : $env:JYPPX_CUDA_ROOT"
Write-Host "cuDNN root    : $env:JYPPX_CUDNN_ROOT"
Write-Host "bridge        : $env:JYPPX_NATIVE_BRIDGE_PATH"
Write-Host "layout timing : warmup=$LayoutWarmup iterations=$LayoutIterations"
Write-Host "table timing  : warmup=$TableWarmup iterations=$TableIterations"
Write-Host "seal timing   : warmup=$SealWarmup iterations=$SealIterations"
Write-Host "server seal   : warmup=$ServerSealWarmup iterations=$ServerSealIterations"

& dotnet test $project -c $Configuration -f net10.0 --no-restore --filter 'FullyQualifiedName~PaddleDocumentTensorRtExternalIntegrationTests|FullyQualifiedName~PaddleDocumentTensorRtServerSealExternalIntegrationTests'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
