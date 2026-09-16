#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Backends = 'onnxruntime',
    [string]$Versions = 'v4,v5,v6',
    [ValidateRange(0, 1000)][int]$Warmup = 5,
    [ValidateRange(1, 10000)][int]$Iterations = 20,
    [switch]$Steady,
    [switch]$FixedConfiguration,
    [ValidateRange(1, 64)][int]$BatchSize = 4,
    [ValidateRange(1, 64)][int]$TensorRtBatchSize = 1,
    [ValidateRange(1, 64)][int]$StageConcurrency = 1,
    [ValidateRange(0, 600000)][int]$InterTestDelayMs = 1000,
    [string]$TensorRtApiVersion = '',
    [string]$ImagePath = '',
    [string]$DeviceLabel = ''
)

$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$isWindows = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
if ([string]::IsNullOrWhiteSpace($TensorRtApiVersion)) {
    # The published bridge follows the native TensorRT major line: Windows
    # packages use the TRT 10 API line, Linux packages use TRT 11.
    $TensorRtApiVersion = if ($isWindows) { '10' } else { '11' }
}
if (@('8', '10', '11') -notcontains $TensorRtApiVersion) {
    throw "TensorRtApiVersion must be one of: 8, 10, 11"
}

$executableName = if ($isWindows) { 'DeploySharp.PaddleOcrBenchmark.exe' } else { 'DeploySharp.PaddleOcrBenchmark' }
$executable = Join-Path $packageRoot $executableName
$modelRoot = Join-Path $packageRoot 'models'
if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    $ImagePath = Join-Path $packageRoot (Join-Path 'images' (Join-Path 'benchmark' 'demo_1.jpg'))
}

foreach ($requiredPath in @($executable, $modelRoot, $ImagePath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required package item is missing: $requiredPath"
    }
}

$allowedBackends = @('onnxruntime', 'openvino', 'opencv-dnn', 'onnxruntime-cuda', 'tensorrt')
$requestedBackends = @($Backends.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() })
if ($requestedBackends.Count -eq 0 -or @($requestedBackends | Where-Object { $_ -notin $allowedBackends }).Count -ne 0) {
    throw "Backends must be a comma-separated subset of: $($allowedBackends -join ', ')"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$resolvedDeviceLabel = if ([string]::IsNullOrWhiteSpace($DeviceLabel)) {
    if ([string]::IsNullOrWhiteSpace($env:COMPUTERNAME)) { 'unknown-device' } else { $env:COMPUTERNAME }
} else { $DeviceLabel.Trim() }
$resultDirectory = Join-Path (Join-Path $packageRoot 'results') $timestamp
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$csvPath = Join-Path $resultDirectory 'paddleocr-benchmark.csv'
$logPath = Join-Path $resultDirectory 'benchmark.log'
$environmentPath = Join-Path $resultDirectory 'environment.json'
$summaryPath = Join-Path $resultDirectory 'summary.md'

function Get-CimSafely([string]$ClassName) {
    try { return @(Get-CimInstance -ClassName $ClassName -ErrorAction Stop) }
    catch { return @() }
}

$operatingSystems = Get-CimSafely 'Win32_OperatingSystem'
$processors = Get-CimSafely 'Win32_Processor'
$videoControllers = Get-CimSafely 'Win32_VideoController'
$computers = Get-CimSafely 'Win32_ComputerSystem'
$powerPlan = try { (powercfg.exe /GETACTIVESCHEME 2>&1 | Out-String).Trim() } catch { $null }
$nvidiaTelemetry = $null
$nvidiaCommandName = if ($isWindows) { 'nvidia-smi.exe' } else { 'nvidia-smi' }
$nvidiaCommand = Get-Command $nvidiaCommandName -ErrorAction SilentlyContinue
if ($null -ne $nvidiaCommand) {
    try {
        $nvidiaTelemetry = (& $nvidiaCommand.Source --query-gpu=name,uuid,driver_version,memory.total,compute_cap,pstate,temperature.gpu,power.limit --format=csv,noheader,nounits 2>&1 | Out-String).Trim()
    }
    catch { $nvidiaTelemetry = $_.Exception.Message }
}

$manifestPath = Join-Path $packageRoot 'manifest.sha256'
$bridgeFileName = if ($isWindows) { 'jyppxtrtbridge.dll' } else { 'libjyppxtrtbridge.so' }
$packagedBridge = Join-Path $packageRoot $bridgeFileName
$environment = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-portable-environment-v1'
    capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    runId = $timestamp
    deviceLabel = $resolvedDeviceLabel
    machineName = if ($isWindows) { $env:COMPUTERNAME } else { $env:HOSTNAME }
    processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    os = @($operatingSystems | ForEach-Object {
        [ordered]@{
            caption = $_.Caption
            version = $_.Version
            buildNumber = $_.BuildNumber
            architecture = $_.OSArchitecture
            totalVisibleMemoryBytes = if ($null -ne $_.TotalVisibleMemorySize) { [long]$_.TotalVisibleMemorySize * 1KB } else { $null }
            freePhysicalMemoryBytes = if ($null -ne $_.FreePhysicalMemory) { [long]$_.FreePhysicalMemory * 1KB } else { $null }
        }
    })
    computer = @($computers | ForEach-Object {
        [ordered]@{
            manufacturer = $_.Manufacturer
            model = $_.Model
            totalPhysicalMemoryBytes = $_.TotalPhysicalMemory
        }
    })
    cpu = @($processors | ForEach-Object {
        [ordered]@{
            name = $_.Name
            manufacturer = $_.Manufacturer
            physicalCores = $_.NumberOfCores
            logicalProcessors = $_.NumberOfLogicalProcessors
            maxClockMHz = $_.MaxClockSpeed
        }
    })
    gpu = @($videoControllers | ForEach-Object {
        [ordered]@{
            name = $_.Name
            driverVersion = $_.DriverVersion
            adapterCompatibility = $_.AdapterCompatibility
            adapterRamBytes = $_.AdapterRAM
        }
    })
    nvidiaSmi = $nvidiaTelemetry
    powerPlan = $powerPlan
    benchmark = [ordered]@{
        deviceLabel = $resolvedDeviceLabel
        executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
        packageManifestSha256 = if (Test-Path -LiteralPath $manifestPath) { (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        imagePath = [IO.Path]::GetFullPath($ImagePath)
        imageSha256 = (Get-FileHash -LiteralPath $ImagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        backends = $requestedBackends
        versions = $Versions
        warmup = $Warmup
        iterations = $Iterations
        inputMode = if ($Steady) { 'steady-prepared-input' } else { 'cold-decode-and-preprocess' }
        autoTune = -not $FixedConfiguration
        batchSize = $BatchSize
        tensorRtBatchSize = $TensorRtBatchSize
        stageConcurrency = $StageConcurrency
        interTestDelayMs = $InterTestDelayMs
    }
    tensorRt = [ordered]@{
        requestedApiVersion = if ($requestedBackends -contains 'tensorrt') { $TensorRtApiVersion } else { $null }
        cudaTargetArchitecture = $env:DEPLOYSHARP_CUDA_ARCHITECTURE
        cudaSequenceArgMaxEnabled = -not [string]::IsNullOrWhiteSpace($env:DEPLOYSHARP_CUDA_ARCHITECTURE)
        bundledBridgePath = if (Test-Path -LiteralPath $packagedBridge) { [IO.Path]::GetFullPath($packagedBridge) } else { $null }
        bundledBridgeSha256 = if (Test-Path -LiteralPath $packagedBridge) { (Get-FileHash -LiteralPath $packagedBridge -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        cudaRoot = $env:JYPPX_CUDA_ROOT
        cudnnRoot = $env:JYPPX_CUDNN_ROOT
        tensorRtRoot = $env:JYPPX_TENSORRT_ROOT
    }
}
$environment | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $environmentPath -Encoding UTF8

$env:DEPLOYSHARP_PADDLEOCR_ROOT = $modelRoot
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = [IO.Path]::GetFullPath($ImagePath)
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = $requestedBackends -join ','
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = $Versions
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = $Warmup.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = $Iterations.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = if ($Steady) { '1' } else { '0' }
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = if ($FixedConfiguration) { '0' } else { '1' }
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = $BatchSize.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = $TensorRtBatchSize.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = $StageConcurrency.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:DEPLOYSHARP_PADDLEOCR_INTER_TEST_DELAY_MS = $InterTestDelayMs.ToString([Globalization.CultureInfo]::InvariantCulture)
# Native providers are consumer-owned. Add their conventional bin/lib folders
# to this process only so CUDA 12.x DLLs are found without copying them into the
# portable package.
$nativePathEntries = New-Object System.Collections.Generic.List[string]
foreach ($nativeRoot in @($env:JYPPX_TENSORRT_ROOT, $env:JYPPX_CUDA_ROOT, $env:JYPPX_CUDNN_ROOT)) {
    if ([string]::IsNullOrWhiteSpace($nativeRoot)) { continue }
    foreach ($nativeSubdirectory in @('bin', 'lib')) {
        $nativeDirectory = Join-Path $nativeRoot $nativeSubdirectory
        if (Test-Path -LiteralPath $nativeDirectory) { $nativePathEntries.Add([IO.Path]::GetFullPath($nativeDirectory)) }
    }
}
if ($nativePathEntries.Count -gt 0) {
    $env:PATH = (($nativePathEntries.ToArray() + @($env:PATH)) -join [IO.Path]::PathSeparator)
    if (-not $isWindows) {
        # Linux's dynamic loader does not search PATH for shared libraries.
        # Keep the consumer-owned CUDA/cuDNN/TensorRT directories explicit.
        $env:LD_LIBRARY_PATH = (($nativePathEntries.ToArray() + @($env:LD_LIBRARY_PATH)) -join [IO.Path]::PathSeparator)
    }
}
if ($requestedBackends -contains 'tensorrt') {
    $env:DEPLOYSHARP_TENSORRT_API_VERSION = $TensorRtApiVersion
    $env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
    if ((Test-Path -LiteralPath $packagedBridge) -and [string]::IsNullOrWhiteSpace($env:JYPPX_NATIVE_BRIDGE_PATH)) {
        $env:JYPPX_NATIVE_BRIDGE_PATH = [IO.Path]::GetFullPath($packagedBridge)
    }
}

Push-Location $packageRoot
try {
    # Native CUDA providers commonly print diagnostic lines to stderr even when
    # the managed benchmark catches the backend as unavailable. Do not let
    # Windows PowerShell 5.1 promote those lines into a terminating error that
    # prevents the remaining backend rows and summary from being written.
    $savedErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $executable $modelRoot $csvPath 2>&1 | Tee-Object -FilePath $logPath
        $benchmarkExitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedErrorActionPreference }
}
finally {
    Pop-Location
}

$summary = @(
    '# DeploySharp PaddleOCR benchmark result',
    '',
    "- Device: $resolvedDeviceLabel (machine=$env:COMPUTERNAME)",
    "- Run ID: $timestamp",
    "- Captured: $([DateTimeOffset]::Now.ToString('O'))",
    "- Mode: $(if ($Steady) { 'steady prepared-input' } else { 'cold decode + preprocessing' })",
    "- Backends: $($requestedBackends -join ', ')",
    "- Versions: $Versions",
    "- Warm-up / iterations: $Warmup / $Iterations",
    "- Exit code: $benchmarkExitCode",
    '',
    '| Model | Backend | Status | Fastest ms | Average ms | P50 ms | P95 ms | Slowest ms | Regions |',
    '|---|---|---|---:|---:|---:|---:|---:|---:|'
)
if (Test-Path -LiteralPath $csvPath) {
    foreach ($row in @(Import-Csv -LiteralPath $csvPath)) {
        $summary += "| $($row.version) $($row.variant) | $($row.backend) | $($row.status) | $($row.total_min_ms) | $($row.total_ms) | $($row.total_p50_ms) | $($row.total_p95_ms) | $($row.total_max_ms) | $($row.regions) |"
    }
}
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if (Test-Path -LiteralPath $csvPath) {
    (Get-FileHash -LiteralPath $csvPath -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath (Join-Path $resultDirectory 'paddleocr-benchmark.csv.sha256') -Encoding ASCII
}

Write-Host ''
Write-Host "Result directory: $resultDirectory"
if ($benchmarkExitCode -ne 0) { exit $benchmarkExitCode }
