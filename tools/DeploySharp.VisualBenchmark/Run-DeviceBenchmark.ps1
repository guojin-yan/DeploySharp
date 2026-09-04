[CmdletBinding()]
param(
    [string]$Backend = 'all',
    [string]$Kind = 'all',
    [ValidateSet('cold', 'steady', 'both')]
    [string]$Mode = 'both',
    [ValidateRange(1, 100)]
    [int]$Warmup = 3,
    [ValidateRange(1, 1000)]
    [int]$Iterations = 10,
    [ValidateSet('none', 'lock-max', 'lock-custom')]
    [string]$GpuClockMode = 'lock-max',
    [ValidateRange(0, 10000)]
    [int]$GpuGraphicsClockMHz = 0,
    [ValidateRange(0, 20000)]
    [int]$GpuMemoryClockMHz = 0,
    [ValidateRange(0, 31)]
    [int]$GpuIndex = 0,
    [switch]$SkipTensorRtBuild,
    [switch]$SkipSpecialVisual,
    [switch]$SkipPaddleOcr,
    [ValidateRange(0, 100)]
    [int]$OcrWarmup = 5,
    [ValidateRange(1, 1000)]
    [int]$OcrIterations = 10,
    [string]$OcrAutotuneChannels = '1,2,4',
    [string]$OcrAutotuneBatches = '1,2,4,8,16'
)

$ErrorActionPreference = 'Stop'
$bundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$resultRoot = Join-Path $bundleRoot 'results'
$nativeRoot = Join-Path $bundleRoot 'runtimes\win-x64\native'
$privateDotnet = Join-Path $bundleRoot '.dotnet\dotnet.exe'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$csvPath = Join-Path $resultRoot "visual-$runId.csv"
$jsonPath = Join-Path $resultRoot "visual-$runId.json"
$manifestPath = Join-Path $resultRoot "device-$runId.json"
$logPath = Join-Path $resultRoot "console-$runId.log"
$ocrCsvPath = Join-Path $resultRoot "paddleocr-full-$runId.csv"
$specialCsvPath = Join-Path $resultRoot "special-visual-$runId.csv"
$gpuTelemetryPath = Join-Path $resultRoot "gpu-$runId.csv"
$bundledCudaRoot = Join-Path $bundleRoot 'vendor\cuda-12.9'
$bundledOrtCudaRoot = Join-Path $bundleRoot 'vendor\cuda-13.2'
$bundledCudnnRoot = Join-Path $bundleRoot 'vendor\cudnn-9.22'
$bundledTensorRtRoot = Join-Path $bundleRoot 'vendor\tensorrt-11.0'

New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$env:PATH = "$bundleRoot;$nativeRoot;$env:PATH"
$env:DOTNET_ROOT = Join-Path $bundleRoot '.dotnet'
$packagedOnnxRuntime = Join-Path $nativeRoot 'onnxruntime.dll'
if (Test-Path -LiteralPath $packagedOnnxRuntime) { $env:DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH = $packagedOnnxRuntime }
$env:CUDA_VISIBLE_DEVICES = $GpuIndex.ToString([Globalization.CultureInfo]::InvariantCulture)
if (Test-Path -LiteralPath (Join-Path $bundledCudaRoot 'bin')) {
    $env:JYPPX_CUDA_ROOT = $bundledCudaRoot
    $env:PATH = "$(Join-Path $bundledCudaRoot 'bin');$env:PATH"
}
if (Test-Path -LiteralPath (Join-Path $bundledOrtCudaRoot 'bin')) {
    $env:PATH = "$(Join-Path $bundledOrtCudaRoot 'bin');$env:PATH"
}
if (Test-Path -LiteralPath (Join-Path $bundledCudnnRoot 'bin')) {
    $env:JYPPX_CUDNN_ROOT = $bundledCudnnRoot
    $env:PATH = "$(Join-Path $bundledCudnnRoot 'bin');$env:PATH"
}
if (Test-Path -LiteralPath (Join-Path $bundledTensorRtRoot 'bin')) {
    $env:JYPPX_TENSORRT_ROOT = $bundledTensorRtRoot
    $env:PATH = "$(Join-Path $bundledTensorRtRoot 'bin');$env:PATH"
}

function Invoke-NvidiaSmi {
    param([string[]]$Arguments)
    try {
        return (& nvidia-smi @Arguments 2>&1 | Out-String).Trim()
    }
    catch {
        return "unavailable=$($_.Exception.Message)"
    }
}

function Resolve-TensorRtExecutable {
    $command = Get-Command trtexec.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }

    $roots = @($env:JYPPX_TENSORRT_ROOT, 'D:\TensorRt', 'C:\TensorRT', 'C:\Program Files\NVIDIA GPU Computing Toolkit\TensorRT') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }
    foreach ($root in $roots) {
        $candidate = Get-ChildItem -LiteralPath $root -Recurse -File -Filter 'trtexec.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $candidate) { return $candidate.FullName }
    }
    return $null
}

function Build-TensorRtEngine {
    param([string]$Kind, [string]$OnnxPath, [string]$TensorRtExecutable)
    if ([IO.Path]::GetExtension($OnnxPath) -ne '.onnx') {
        Write-Host "TensorRT unsupported artifact format for ${Kind}: $([IO.Path]::GetExtension($OnnxPath))"
        return
    }
    $enginePath = "$OnnxPath.engine"
    if (Test-Path -LiteralPath $enginePath) { return }
    Write-Host "Building device-specific TensorRT engine: $([IO.Path]::GetFileName($OnnxPath))"
    $arguments = @("--onnx=$OnnxPath", "--saveEngine=$enginePath", '--builderOptimizationLevel=3', '--skipInference')
    $shapeProfiles = @{
        deimv2 = 'images:1x3x640x640,orig_target_sizes:1x2'
        ppyoloe = 'image:1x3x640x640,scale_factor:1x2'
        rfdetr = 'input:1x3x512x512'
        'rfdetr-seg' = 'input:1x3x432x432'
        'rtdetr-decoded-onnx' = 'image:1x3x640x640,im_shape:1x2,scale_factor:1x2'
        'rtdetr-raw' = 'image:1x3x640x640'
        rmbg20 = 'pixel_values:1x3x1024x1024'
        'rmbg20-int8' = 'pixel_values:1x3x1024x1024'
    }
    if ($shapeProfiles.ContainsKey($Kind)) {
        $shape = $shapeProfiles[$Kind]
        $arguments += "--minShapes=$shape", "--optShapes=$shape", "--maxShapes=$shape"
    }
    & $TensorRtExecutable @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $enginePath)) {
        Write-Warning "TensorRT engine build failed for $OnnxPath; the TensorRT row will be recorded as unavailable."
    }
}

$nvidiaBefore = Invoke-NvidiaSmi @("--id=$GpuIndex", '--query-gpu=name,driver_version,compute_cap,memory.total,pstate,clocks.current.graphics,clocks.current.memory,temperature.gpu,utilization.gpu,power.draw', '--format=csv,noheader,nounits')
try {
    $computeCapability = (& nvidia-smi "--id=$GpuIndex" '--query-gpu=compute_cap' '--format=csv,noheader,nounits' 2>$null | Select-Object -First 1).Trim()
    if (-not [string]::IsNullOrWhiteSpace($computeCapability) -and [string]::IsNullOrWhiteSpace($env:DEPLOYSHARP_CUDA_ARCHITECTURE)) {
        $env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_' + ($computeCapability -replace '\.', '')
    }
}
catch { }

$packagedBridge = Join-Path $bundledTensorRtRoot 'bin\jyppxtrtbridge.dll'
if (-not (Test-Path -LiteralPath $packagedBridge)) { $packagedBridge = Join-Path $bundleRoot 'vendor\tensorrt11\jyppxtrtbridge.dll' }
$usingPackagedBridge = $false
if ([string]::IsNullOrWhiteSpace($env:JYPPX_NATIVE_BRIDGE_PATH) -and (Test-Path -LiteralPath $packagedBridge)) {
    $env:JYPPX_NATIVE_BRIDGE_PATH = $packagedBridge
    $usingPackagedBridge = $true
}

$trtexec = Resolve-TensorRtExecutable
if (-not [string]::IsNullOrWhiteSpace($trtexec)) {
    $tensorRtBin = Split-Path -Parent $trtexec
    $tensorRtRoot = Split-Path -Parent $tensorRtBin
    if ([string]::IsNullOrWhiteSpace($env:JYPPX_TENSORRT_ROOT)) { $env:JYPPX_TENSORRT_ROOT = $tensorRtRoot }
    $env:PATH = "$tensorRtBin;$(Join-Path $tensorRtRoot 'lib');$env:PATH"
    $detectedTensorRtApi = if (Get-ChildItem -LiteralPath $tensorRtRoot -Recurse -File -Filter 'nvinfer_11.dll' -ErrorAction SilentlyContinue | Select-Object -First 1) { '11' }
        elseif (Get-ChildItem -LiteralPath $tensorRtRoot -Recurse -File -Filter 'nvinfer_10.dll' -ErrorAction SilentlyContinue | Select-Object -First 1) { '10' }
        elseif (Get-ChildItem -LiteralPath $tensorRtRoot -Recurse -File -Filter 'nvinfer.dll' -ErrorAction SilentlyContinue | Select-Object -First 1) { '8' }
        else { $null }
    if ([string]::IsNullOrWhiteSpace($env:DEPLOYSHARP_TENSORRT_API_VERSION) -and -not [string]::IsNullOrWhiteSpace($detectedTensorRtApi)) { $env:DEPLOYSHARP_TENSORRT_API_VERSION = $detectedTensorRtApi }
    if ($usingPackagedBridge -and $detectedTensorRtApi -ne '11') {
        Write-Warning 'The packaged bridge targets TensorRT 11 and will not be used with this TensorRT installation. Set JYPPX_NATIVE_BRIDGE_PATH to a matching bridge.'
        Remove-Item Env:JYPPX_NATIVE_BRIDGE_PATH -ErrorAction SilentlyContinue
    }
    if (-not [string]::IsNullOrWhiteSpace($env:JYPPX_NATIVE_BRIDGE_PATH)) { $env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1' }
}

$modelPaths = [ordered]@{
    yolov5n = Join-Path $bundleRoot 'models\visual\yolov5n.onnx'
    yolov6s = Join-Path $bundleRoot 'models\visual\yolov6s.onnx'
    yolov7 = Join-Path $bundleRoot 'models\visual\yolov7.onnx'
    yolov8n = Join-Path $bundleRoot 'models\visual\yolov8n.onnx'
    yolov9s = Join-Path $bundleRoot 'models\visual\yolov9s.onnx'
    yolov10n = Join-Path $bundleRoot 'models\visual\yolov10n.onnx'
    yolo11n = Join-Path $bundleRoot 'models\visual\yolo11n.onnx'
    yolo12n = Join-Path $bundleRoot 'models\visual\yolo12n.onnx'
    yolo13n = Join-Path $bundleRoot 'models\visual\yolo13n.onnx'
    yolo26n = Join-Path $bundleRoot 'models\visual\yolo26n.onnx'
    'yolov8s-cls' = Join-Path $bundleRoot 'models\visual\yolov8s-cls.onnx'
    'yolov5s-seg' = Join-Path $bundleRoot 'models\visual\yolov5s-seg.onnx'
    'yolov8n-seg' = Join-Path $bundleRoot 'models\visual\yolov8n-seg.onnx'
    'yolov9c-seg' = Join-Path $bundleRoot 'models\visual\yolov9c-seg.onnx'
    'yolo11s-seg' = Join-Path $bundleRoot 'models\visual\yolo11s-seg.onnx'
    'yolo26s-seg' = Join-Path $bundleRoot 'models\visual\yolo26s-seg.onnx'
    'yolov8s-pose' = Join-Path $bundleRoot 'models\visual\yolov8s-pose.onnx'
    'yolo11s-pose' = Join-Path $bundleRoot 'models\visual\yolo11s-pose.onnx'
    'yolo26s-pose' = Join-Path $bundleRoot 'models\visual\yolo26s-pose.onnx'
    'yolov8s-obb' = Join-Path $bundleRoot 'models\visual\yolov8s-obb.onnx'
    'yolo11s-obb' = Join-Path $bundleRoot 'models\visual\yolo11s-obb.onnx'
    'yolo26s-obb' = Join-Path $bundleRoot 'models\visual\yolo26s-obb.onnx'
    deimv2 = Join-Path $bundleRoot 'models\visual\deimv2.onnx'
    ppyoloe = Join-Path $bundleRoot 'models\visual\ppyoloe.onnx'
    rfdetr = Join-Path $bundleRoot 'models\visual\rfdetr.onnx'
    'rfdetr-seg' = Join-Path $bundleRoot 'models\visual\rfdetr-seg.onnx'
    'rtdetr-decoded-ir' = Join-Path $bundleRoot 'models\visual\rtdetr-decoded-ir.xml'
    'rtdetr-decoded-onnx' = Join-Path $bundleRoot 'models\visual\rtdetr-decoded-onnx.onnx'
    'rtdetr-raw' = Join-Path $bundleRoot 'models\visual\rtdetr-raw.onnx'
    padim = Join-Path $bundleRoot 'models\visual\padim.onnx'
    rmbg14 = Join-Path $bundleRoot 'models\visual\bria-rmbg-1.4.onnx'
    rmbg20 = Join-Path $bundleRoot 'models\visual\rmbg-2.0.onnx'
    'rmbg20-int8' = Join-Path $bundleRoot 'models\visual\rmbg-2.0-int8.onnx'
}

$tensorRtRequested = $Backend -eq 'all' -or $Backend -match '(^|,)(tensorrt|tensorrt-cuda)(,|$)'
if ($tensorRtRequested -and -not $SkipTensorRtBuild -and -not [string]::IsNullOrWhiteSpace($trtexec)) {
    $selectedKinds = if ($Kind -eq 'all') { @($modelPaths.Keys) } else { @($Kind.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() }) }
    foreach ($selectedKind in $selectedKinds) {
        if ($modelPaths.Contains($selectedKind)) { Build-TensorRtEngine -Kind $selectedKind -OnnxPath $modelPaths[$selectedKind] -TensorRtExecutable $trtexec }
    }
}

$visualImagePath = Join-Path $bundleRoot 'data\bus.jpg'
$arguments = @(
    '--kind', $Kind,
    '--backend', $Backend,
    '--mode', $Mode,
    '--warmup', $Warmup,
    '--iterations', $Iterations,
    '--output', $csvPath,
    '--json-output', $jsonPath
)
if (Test-Path -LiteralPath $visualImagePath -PathType Leaf) {
    $arguments = @('--kind', $Kind, '--backend', $Backend, '--mode', $Mode, '--warmup', $Warmup, '--iterations', $Iterations, '--image', $visualImagePath, '--output', $csvPath, '--json-output', $jsonPath)
}
foreach ($entry in $modelPaths.GetEnumerator()) {
    $arguments += "--model-$($entry.Key)"
    $arguments += $entry.Value
}

$gpuBackendRequested = $Backend -eq 'all' -or $Backend -match '(^|,)(onnxruntime-cuda|tensorrt|tensorrt-cuda)(,|$)'
$graphicsClockChanged = $false
$memoryClockChanged = $false
$gpuClockControl = [ordered]@{
    mode = $GpuClockMode
    gpu_index = $GpuIndex
    attempted = $false
    graphics_target_mhz = $null
    memory_target_mhz = $null
    graphics_lock_exit_code = $null
    graphics_lock_output = $null
    memory_lock_exit_code = $null
    memory_lock_output = $null
    state_after_lock = $null
    graphics_reset_exit_code = $null
    graphics_reset_output = $null
    memory_reset_exit_code = $null
    memory_reset_output = $null
}
if ($GpuClockMode -ne 'none' -and $gpuBackendRequested) {
    $gpuClockControl.attempted = $true
    try {
        $graphicsTarget = $GpuGraphicsClockMHz
        $memoryTarget = $GpuMemoryClockMHz
        if ($GpuClockMode -eq 'lock-max') {
            $graphicsTarget = [int][double]((& nvidia-smi "--id=$GpuIndex" '--query-gpu=clocks.max.graphics' '--format=csv,noheader,nounits' 2>$null | Select-Object -First 1).Trim())
            $memoryTarget = [int][double]((& nvidia-smi "--id=$GpuIndex" '--query-gpu=clocks.max.memory' '--format=csv,noheader,nounits' 2>$null | Select-Object -First 1).Trim())
        }
        if ($graphicsTarget -gt 0) {
            $gpuClockControl.graphics_target_mhz = $graphicsTarget
            $gpuClockControl.graphics_lock_output = (& nvidia-smi "--id=$GpuIndex" "--lock-gpu-clocks=$graphicsTarget,$graphicsTarget" 2>&1 | Out-String).Trim()
            $gpuClockControl.graphics_lock_exit_code = $LASTEXITCODE
            $graphicsClockChanged = $LASTEXITCODE -eq 0
        }
        if ($memoryTarget -gt 0) {
            $gpuClockControl.memory_target_mhz = $memoryTarget
            $gpuClockControl.memory_lock_output = (& nvidia-smi "--id=$GpuIndex" "--lock-memory-clocks=$memoryTarget,$memoryTarget" 2>&1 | Out-String).Trim()
            $gpuClockControl.memory_lock_exit_code = $LASTEXITCODE
            $memoryClockChanged = $LASTEXITCODE -eq 0
        }
        Start-Sleep -Milliseconds 1000
        $gpuClockControl.state_after_lock = Invoke-NvidiaSmi @("--id=$GpuIndex", '--query-gpu=pstate,clocks.current.graphics,clocks.current.sm,clocks.current.memory,power.draw,temperature.gpu', '--format=csv,noheader,nounits')
    }
    catch {
        $gpuClockControl.state_after_lock = "lock-error=$($_.Exception.Message)"
    }
}
elseif ($GpuClockMode -ne 'none') {
    $gpuClockControl.state_after_lock = 'skipped=no-gpu-backend-selected'
}

$startedAt = [DateTimeOffset]::UtcNow
$exitCode = -1
$visualExitCode = -1
$specialVisualExitCode = $null
$ocrExitCode = $null
$gpuTelemetryJob = $null
$gpuTelemetryQuery = 'timestamp,name,pstate,utilization.gpu,utilization.memory,clocks.current.graphics,clocks.current.sm,clocks.current.memory,power.draw,power.limit,temperature.gpu,clocks_event_reasons.sw_power_cap,clocks_event_reasons.hw_slowdown,clocks_event_reasons.hw_thermal_slowdown'
try {
    $nvidiaSmiCommand = (Get-Command nvidia-smi -ErrorAction Stop).Source
    'timestamp,name,pstate,gpu_utilization_percent,memory_utilization_percent,graphics_clock_mhz,sm_clock_mhz,memory_clock_mhz,power_draw_w,power_limit_w,temperature_c,sw_power_cap,hw_slowdown,hw_thermal_slowdown' | Set-Content -LiteralPath $gpuTelemetryPath -Encoding utf8
    & $nvidiaSmiCommand "--id=$GpuIndex" "--query-gpu=$gpuTelemetryQuery" '--format=csv,noheader,nounits' | Add-Content -LiteralPath $gpuTelemetryPath -Encoding utf8
    $gpuTelemetryJob = Start-Job -ArgumentList $gpuTelemetryPath, $gpuTelemetryQuery, $nvidiaSmiCommand, $GpuIndex -ScriptBlock {
        param($path, $query, $executable, $gpuIndex)
        while ($true) {
            & $executable "--id=$gpuIndex" "--query-gpu=$query" '--format=csv,noheader,nounits' | Add-Content -LiteralPath $path -Encoding utf8
            Start-Sleep -Milliseconds 500
        }
    }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ((Get-Content -LiteralPath $gpuTelemetryPath -ErrorAction SilentlyContinue).Count -ge 3) { break }
        Start-Sleep -Milliseconds 100
    }
}
catch { }
Start-Transcript -Path $logPath -Force | Out-Null
try {
    & $privateDotnet (Join-Path $bundleRoot 'DeploySharp.VisualBenchmark.dll') @arguments
    $visualExitCode = $LASTEXITCODE

    $specialTool = Join-Path $bundleRoot 'tools\special-visual\DeploySharp.SpecialVisualBenchmark.dll'
    if (-not $SkipSpecialVisual -and (Test-Path -LiteralPath $specialTool)) {
        Write-Host 'Running CLIP, SAM, and BLIP complete multi-artifact pipelines.'
        $specialArguments = @('--kind', 'all', '--backend', $Backend, '--model-root', (Join-Path $bundleRoot 'models\special'), '--warmup', $Warmup, '--iterations', $Iterations, '--output', $specialCsvPath)
        if (Test-Path -LiteralPath $visualImagePath -PathType Leaf) { $specialArguments = @('--kind', 'all', '--backend', $Backend, '--model-root', (Join-Path $bundleRoot 'models\special'), '--image', $visualImagePath, '--warmup', $Warmup, '--iterations', $Iterations, '--output', $specialCsvPath) }
        & $privateDotnet $specialTool @specialArguments
        $specialVisualExitCode = $LASTEXITCODE
    }
    elseif (-not $SkipSpecialVisual) {
        Write-Warning "Special visual benchmark tool is not packaged: $specialTool"
        $specialVisualExitCode = 2
    }

    $ocrTool = Join-Path $bundleRoot 'tools\paddleocr\DeploySharp.PaddleOcrBenchmark.dll'
    $ocrModelRoot = Join-Path $bundleRoot 'models\paddleocr'
    $ocrImage = Join-Path $bundleRoot 'data\ocr-demo.jpg'
    if (-not $SkipPaddleOcr -and (Test-Path -LiteralPath $ocrTool)) {
        $selectedOcrBackends = if ($Backend -eq 'all') {
            @('onnxruntime', 'onnxruntime-cuda', 'openvino', 'opencv-dnn', 'tensorrt')
        }
        else {
            @($Backend.Split(',', [StringSplitOptions]::RemoveEmptyEntries) |
                ForEach-Object { $_.Trim().ToLowerInvariant() } |
                ForEach-Object { if ($_ -eq 'tensorrt-cuda') { 'tensorrt' } else { $_ } } |
                Where-Object { $_ -in @('onnxruntime', 'onnxruntime-cuda', 'openvino', 'opencv-dnn', 'tensorrt') } |
                Select-Object -Unique)
        }
        if ($selectedOcrBackends.Count -gt 0) {
            if ($selectedOcrBackends -contains 'tensorrt' -and -not $SkipTensorRtBuild -and -not [string]::IsNullOrWhiteSpace($trtexec)) {
                $ocrEngineBuilder = Join-Path $bundleRoot 'tools\paddleocr\Build-TensorRtEngines.ps1'
                $ocrEngineRoot = Join-Path $bundleRoot 'models\paddleocr-device-engines'
                $expectedOcrEngines = @(Get-ChildItem -LiteralPath $ocrModelRoot -Recurse -File -Filter '*.onnx' -ErrorAction SilentlyContinue).Count
                $actualOcrEngines = @(Get-ChildItem -LiteralPath $ocrEngineRoot -Recurse -File -Filter '*.engine' -ErrorAction SilentlyContinue).Count
                if ($expectedOcrEngines -gt 0 -and $actualOcrEngines -lt $expectedOcrEngines -and (Test-Path -LiteralPath $ocrEngineBuilder)) {
                    try {
                        Write-Host "Building device-specific PaddleOCR TensorRT engines (opt batch 8, max batch 16)."
                        & powershell -NoProfile -ExecutionPolicy Bypass -File $ocrEngineBuilder -ModelRoot $ocrModelRoot -OutputRoot $ocrEngineRoot -TensorRtRoot $env:JYPPX_TENSORRT_ROOT -StageOptBatch 8 -StageMaxBatch 16
                        if ($LASTEXITCODE -ne 0) { throw "PaddleOCR TensorRT engine builder exited with code $LASTEXITCODE." }
                        $actualOcrEngines = @(Get-ChildItem -LiteralPath $ocrEngineRoot -Recurse -File -Filter '*.engine' -ErrorAction SilentlyContinue).Count
                    }
                    catch {
                        Write-Warning "PaddleOCR TensorRT engine build did not complete: $($_.Exception.Message)"
                    }
                }
                if ($expectedOcrEngines -gt 0 -and $actualOcrEngines -eq $expectedOcrEngines) {
                    $ocrModelRoot = $ocrEngineRoot
                }
            }
            $env:DOTNET_ROLL_FORWARD = 'Major'
            $env:DEPLOYSHARP_PADDLEOCR_ROOT = $ocrModelRoot
            if (Test-Path -LiteralPath $ocrImage -PathType Leaf) { $env:DEPLOYSHARP_PADDLEOCR_IMAGE = $ocrImage }
            else { Remove-Item Env:DEPLOYSHARP_PADDLEOCR_IMAGE -ErrorAction SilentlyContinue }
            $env:DEPLOYSHARP_PADDLEOCR_BACKENDS = $selectedOcrBackends -join ','
            $env:DEPLOYSHARP_PADDLEOCR_WARMUP = $OcrWarmup.ToString([Globalization.CultureInfo]::InvariantCulture)
            $env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = $OcrIterations.ToString([Globalization.CultureInfo]::InvariantCulture)
            $env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '1'
            $env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE_CONCURRENCY = $OcrAutotuneChannels
            $env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE_BATCHES = $OcrAutotuneBatches
            $env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '0'
            Write-Host "Running PaddleOCR full pipelines with automatic batch/channel tuning: $($selectedOcrBackends -join ',')"
            & $privateDotnet $ocrTool $ocrModelRoot $ocrCsvPath
            $ocrExitCode = $LASTEXITCODE
        }
        else {
            Write-Host 'Skipping PaddleOCR because the selected backend list contains no OCR backend.'
            $ocrExitCode = 0
        }
    }
    elseif (-not $SkipPaddleOcr) {
        Write-Warning "PaddleOCR benchmark tool is not packaged: $ocrTool"
        $ocrExitCode = 2
    }
    $exitCode = if ($visualExitCode -ne 0) { $visualExitCode } elseif ($null -ne $specialVisualExitCode -and $specialVisualExitCode -ne 0) { $specialVisualExitCode } elseif ($null -ne $ocrExitCode -and $ocrExitCode -ne 0) { $ocrExitCode } else { 0 }
}
finally {
    Stop-Transcript | Out-Null
    if ($null -ne $gpuTelemetryJob) {
        Stop-Job -Job $gpuTelemetryJob -ErrorAction SilentlyContinue
        Remove-Job -Job $gpuTelemetryJob -Force -ErrorAction SilentlyContinue
    }
    if ($graphicsClockChanged) {
        $gpuClockControl.graphics_reset_output = (& nvidia-smi "--id=$GpuIndex" '--reset-gpu-clocks' 2>&1 | Out-String).Trim()
        $gpuClockControl.graphics_reset_exit_code = $LASTEXITCODE
    }
    if ($memoryClockChanged) {
        $gpuClockControl.memory_reset_output = (& nvidia-smi "--id=$GpuIndex" '--reset-memory-clocks' 2>&1 | Out-String).Trim()
        $gpuClockControl.memory_reset_exit_code = $LASTEXITCODE
    }
}
$finishedAt = [DateTimeOffset]::UtcNow
$nvidiaAfter = Invoke-NvidiaSmi @("--id=$GpuIndex", '--query-gpu=name,driver_version,compute_cap,memory.total,pstate,clocks.current.graphics,clocks.current.memory,temperature.gpu,utilization.gpu,power.draw', '--format=csv,noheader,nounits')

try { $cpuInfo = @(Get-CimInstance Win32_Processor | Select-Object Name,Manufacturer,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed) } catch { $cpuInfo = @("unavailable=$($_.Exception.Message)") }
try { $physicalMemoryBytes = [long](Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory } catch { $physicalMemoryBytes = $null }
try { $displayAdapters = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,AdapterRAM) } catch { $displayAdapters = @("unavailable=$($_.Exception.Message)") }
try { $powerPlan = (& powercfg /getactivescheme 2>&1 | Out-String).Trim() } catch { $powerPlan = "unavailable=$($_.Exception.Message)" }

$manifest = [ordered]@{
    schema_version = 1
    run_id = $runId
    started_at_utc = $startedAt.ToString('O')
    finished_at_utc = $finishedAt.ToString('O')
    elapsed_seconds = [Math]::Round(($finishedAt - $startedAt).TotalSeconds, 3)
    exit_code = $exitCode
    computer_name = $env:COMPUTERNAME
    os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    process_architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    logical_processor_count = [Environment]::ProcessorCount
    cpu = $cpuInfo
    physical_memory_bytes = $physicalMemoryBytes
    display_adapters = $displayAdapters
    power_plan = $powerPlan
    powershell_version = $PSVersionTable.PSVersion.ToString()
    benchmark = [ordered]@{
        backend = $Backend
        kind = $Kind
        mode = $Mode
        warmup = $Warmup
        iterations = $Iterations
        visual_exit_code = $visualExitCode
        special_visual = [ordered]@{ skipped = [bool]$SkipSpecialVisual; exit_code = $specialVisualExitCode }
        paddleocr = [ordered]@{
            skipped = [bool]$SkipPaddleOcr
            warmup = $OcrWarmup
            iterations = $OcrIterations
            autotune_channels = $OcrAutotuneChannels
            autotune_batches = $OcrAutotuneBatches
            exit_code = $ocrExitCode
        }
    }
    gpu_clock_control = $gpuClockControl
    gpu_before = $nvidiaBefore
    gpu_after = $nvidiaAfter
    cuda_architecture = $env:DEPLOYSHARP_CUDA_ARCHITECTURE
    tensorrt_api_version = $env:DEPLOYSHARP_TENSORRT_API_VERSION
    tensorrt_root = $env:JYPPX_TENSORRT_ROOT
    cuda_root = $env:JYPPX_CUDA_ROOT
    native_bridge_path = $env:JYPPX_NATIVE_BRIDGE_PATH
    trtexec_path = $trtexec
    outputs = [ordered]@{ csv = $csvPath; json = $jsonPath; special_visual_csv = if (Test-Path -LiteralPath $specialCsvPath) { $specialCsvPath } else { $null }; paddleocr_csv = if (Test-Path -LiteralPath $ocrCsvPath) { $ocrCsvPath } else { $null }; console_log = $logPath; gpu_telemetry = if (Test-Path -LiteralPath $gpuTelemetryPath) { $gpuTelemetryPath } else { $null } }
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Results: $resultRoot"
Write-Host "Return every file created under results for this run, including GPU telemetry when present."
exit $exitCode
