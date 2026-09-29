[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $OnnxRoot,
    [Parameter(Mandatory = $true)] [string] $TensorRtRoot,
    [Parameter(Mandatory = $true)] [string] $CudaRoot,
    [Parameter(Mandatory = $true)] [string] $CudnnRoot,
    [Parameter(Mandatory = $true)] [string] $OutputRoot,
    [string] $TextGraphRoot,
    [switch] $EnableTextTf32,
    [switch] $TextOnly,
    [ValidateRange(0, 5)] [int] $BuilderOptimizationLevel = 0
)

$ErrorActionPreference = 'Stop'
$onnxPath = [System.IO.Path]::GetFullPath($OnnxRoot)
$trtPath = [System.IO.Path]::GetFullPath($TensorRtRoot)
$cudaPath = [System.IO.Path]::GetFullPath($CudaRoot)
$cudnnPath = [System.IO.Path]::GetFullPath($CudnnRoot)
$outputPath = [System.IO.Path]::GetFullPath($OutputRoot)
$textPath = if ([string]::IsNullOrWhiteSpace($TextGraphRoot)) { Join-Path $onnxPath 'text-onnx-verify-20260923' } else { [System.IO.Path]::GetFullPath($TextGraphRoot) }
$trtexec = Join-Path $trtPath 'bin\trtexec.exe'
$optimizationSuffix = if ($BuilderOptimizationLevel -gt 0) { "-opt$BuilderOptimizationLevel" } else { '' }
$textPrecisionSuffix = if ($EnableTextTf32) { '-fp32-tf32' } else { '-fp32' }

foreach ($requiredPath in @($onnxPath, $trtPath, $cudaPath, $cudnnPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) { throw "Required directory is missing: $requiredPath" }
}
if (-not (Test-Path -LiteralPath $trtexec -PathType Leaf)) { throw "TensorRT trtexec.exe is missing: $trtexec" }

$graphs = @(
    @{ Role = 'vision-projector'; Source = 'chart-vision'; Engine = 'vision-fp16.plan'; Precision = 'fp16'; Profiles = @() },
    @{ Role = 'token-embedding'; Source = 'chart-token-embedding-dynamic'; Engine = 'token-embedding-fp16.plan'; Precision = 'fp16'; Profiles = @('input_ids:1x1', 'input_ids:1x286', 'input_ids:1x2333') },
    @{ Role = 'text-prefill'; Source = 'chart-text-prefill-full'; Engine = "text-prefill$textPrecisionSuffix$optimizationSuffix.plan"; Precision = $(if ($EnableTextTf32) { 'fp32-tf32-enabled' } else { 'fp32-strict' }); Profiles = @() },
    @{ Role = 'text-decode-with-past'; Source = 'chart-text-decoder-dynamic-past-full'; Engine = "text-decode$textPrecisionSuffix$optimizationSuffix.plan"; Precision = $(if ($EnableTextTf32) { 'fp32-tf32-enabled' } else { 'fp32-strict' }); Profiles = @('dynamic-kv') }
)

function Get-GraphSource([hashtable] $Graph) {
    $sourceRoot = if ($Graph.Role -eq 'vision-projector') { $onnxPath } else { $textPath }
    foreach ($candidate in @((Join-Path $sourceRoot ($Graph.Source + '.onnx')), (Join-Path $sourceRoot ($Graph.Source + '-epsilon.onnx')))) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw "Required ONNX graph is missing: $($Graph.Role) below $sourceRoot"
}

$graphsToValidate = if ($TextOnly) { @($graphs[2], $graphs[3]) } else { $graphs }
foreach ($graph in $graphsToValidate) { $null = Get-GraphSource $graph }

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$env:PATH = "$trtPath\bin;$trtPath\lib;$cudaPath\bin;$cudnnPath\bin;$env:PATH"

function Invoke-TensorRtExec([string[]] $Arguments, [string] $LogPath) {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $trtexec
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $null = $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw 'Could not start TensorRT trtexec.' }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    [System.IO.File]::WriteAllText($LogPath, $stdout + [Environment]::NewLine + $stderr, [System.Text.UTF8Encoding]::new($false))
    return $process.ExitCode
}

function Invoke-PlanBuild([hashtable] $Graph, [string[]] $ShapeArguments) {
    $source = Get-GraphSource $Graph
    $engine = Join-Path $outputPath $Graph.Engine
    $log = Join-Path $outputPath ($Graph.Role + '-trtexec.log')
    # TensorRT 10.11 parses its large KV shape triplets reliably before the
    # remaining builder options; preserve this order in the generated argv.
    $arguments = @("--onnx=$source") + $ShapeArguments + @("--saveEngine=$engine", '--memPoolSize=workspace:1024', "--builderOptimizationLevel=$BuilderOptimizationLevel", '--skipInference')
    if ($Graph.Precision -eq 'fp16') { $arguments += '--fp16' }
    elseif (-not $EnableTextTf32) { $arguments += '--noTF32' }
    $exitCode = Invoke-TensorRtExec -Arguments $arguments -LogPath $log
    if ($exitCode -ne 0) { throw "TensorRT failed to build $($Graph.Role) (exit $exitCode); inspect $log" }
    if (-not (Test-Path -LiteralPath $engine -PathType Leaf) -or (Get-Item -LiteralPath $engine).Length -lt 8) { throw "TensorRT did not produce a valid plan for $($Graph.Role); inspect $log" }
    $hash = (Get-FileHash -LiteralPath $engine -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{ Role = $Graph.Role; Precision = $Graph.Precision; Plan = $engine; Bytes = (Get-Item -LiteralPath $engine).Length; Sha256 = $hash; Log = $log }
}

$built = [System.Collections.Generic.List[object]]::new()
if (-not $TextOnly) {
    $built.Add((Invoke-PlanBuild $graphs[0] @()))
    $built.Add((Invoke-PlanBuild $graphs[1] @('--minShapes=input_ids:1x1', '--optShapes=input_ids:1x286', '--maxShapes=input_ids:1x2333')))
}
$built.Add((Invoke-PlanBuild $graphs[2] @()))

$minimum = [System.Collections.Generic.List[string]]::new()
$optimum = [System.Collections.Generic.List[string]]::new()
$maximum = [System.Collections.Generic.List[string]]::new()
$minimum.Add('attention_mask:1x287')
$optimum.Add('attention_mask:1x513')
$maximum.Add('attention_mask:1x2333')
for ($layer = 0; $layer -lt 24; $layer++) {
    foreach ($kind in @('key', 'value')) {
        $minimum.Add("past_${kind}_${layer}:1x286x16x64")
        $optimum.Add("past_${kind}_${layer}:1x512x16x64")
        $maximum.Add("past_${kind}_${layer}:1x2332x16x64")
    }
}
$decodeMinArgument = '--minShapes=' + ($minimum -join ',')
$decodeOptArgument = '--optShapes=' + ($optimum -join ',')
$decodeMaxArgument = '--maxShapes=' + ($maximum -join ',')
$decodeSource = Get-GraphSource $graphs[3]
$decodeEngine = Join-Path $outputPath $graphs[3].Engine
$decodeLog = Join-Path $outputPath 'text-decode-with-past-trtexec.log'
# Keep the long 48-tensor profile invocation at script scope. This mirrors
# the tested manual native call without array parameter binding in a helper.
$decodeArguments = @("--onnx=$decodeSource", $decodeMinArgument, $decodeOptArgument, $decodeMaxArgument, "--saveEngine=$decodeEngine", '--memPoolSize=workspace:1024', "--builderOptimizationLevel=$BuilderOptimizationLevel", '--skipInference')
if (-not $EnableTextTf32) { $decodeArguments += '--noTF32' }
$decodeExitCode = Invoke-TensorRtExec -Arguments $decodeArguments -LogPath $decodeLog
if ($decodeExitCode -ne 0) { throw "TensorRT failed to build text-decode-with-past; inspect $decodeLog" }
if (-not (Test-Path -LiteralPath $decodeEngine -PathType Leaf) -or (Get-Item -LiteralPath $decodeEngine).Length -lt 8) { throw "TensorRT did not produce a valid dynamic-KV plan; inspect $decodeLog" }
$built.Add([pscustomobject]@{ Role = $graphs[3].Role; Precision = $graphs[3].Precision; Plan = $decodeEngine; Bytes = (Get-Item -LiteralPath $decodeEngine).Length; Sha256 = (Get-FileHash -LiteralPath $decodeEngine -Algorithm SHA256).Hash.ToLowerInvariant(); Log = $decodeLog })

$record = [pscustomobject]@{
    schemaVersion = 'deploysharp-chart2table-tensorrt-plans-v1'
    apiLine = 'TensorRT 10.11'
    graphPolicy = if ($EnableTextTf32) { 'FP16 vision/embedding, FP32 language graphs with TensorRT TF32 tactics enabled for measured accuracy/performance comparison.' } else { 'FP16 vision/embedding, strict FP32 language graphs with TF32 disabled; TensorRT FP16 lm_head was zero on the verified RTX 3060 Laptop.' }
    deviceProfile = 'RTX 3060 Laptop, compute capability 8.6'
    plans = @($built)
}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath 'chart2table-tensorrt-plans.json') -Encoding utf8
$record | ConvertTo-Json -Depth 8
