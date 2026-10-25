[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BundleRoot,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [ValidateSet('OpenCV', 'TensorRT')] [string[]] $Backend = @('OpenCV', 'TensorRT'),
    [ValidateSet('embedding', 'head', 'vision', 'decoder', 'prefill')] [string[]] $Graph = @('embedding', 'head', 'vision', 'decoder'),
    [ValidateRange(10, 3600)] [int] $TimeoutSeconds = 300,
    [ValidateSet('ImporterOrBuilder', 'ComponentInference')] [string] $ProbeMode = 'ImporterOrBuilder',
    [string] $EngineRoot,
    [string] $FixtureRoot,
    [ValidateSet('10', '11')] [string] $TensorRtApi = '10',
    [ValidateSet('baseline', 'compact-official-tasks', 'all-official-tasks', 'spotting-fixed')] [string] $ProfilePreset = 'baseline',
    [ValidateSet('Float32', 'Float16')] [string] $Precision = 'Float32',
    [ValidateRange(1, 4)] [int] $WorkspaceGiB = 1,
    [string] $TensorRtRoot,
    [string] $CudaRoot,
    [string] $CudnnRoot,
    [string] $NativeBridgeRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$bundle = (Resolve-Path -LiteralPath $BundleRoot).Path
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($role in $Graph) {
    if (-not (Test-Path -LiteralPath (Join-Path $bundle "$role.onnx") -PathType Leaf)) { throw "Missing graph: $role" }
}
if ($ProbeMode -eq 'ComponentInference') {
    if (@($Backend | Where-Object { $_ -ne 'TensorRT' }).Count -gt 0) {
        throw 'ComponentInference currently accepts only TensorRT.'
    }
    if (-not $EngineRoot) { throw 'ComponentInference requires EngineRoot from a prior successful Builder probe.' }
    $EngineRoot = (Resolve-Path -LiteralPath $EngineRoot).Path
    if ($Graph -contains 'vision') {
        if (-not $FixtureRoot) { throw 'Vision component inference requires FixtureRoot with the pinned OCR Patch.' }
        $env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_FIXTURE_ROOT = (Resolve-Path -LiteralPath $FixtureRoot).Path
    }
}
if ($TensorRtRoot) { $env:JYPPX_TENSORRT_ROOT = (Resolve-Path -LiteralPath $TensorRtRoot).Path }
if ($CudaRoot) { $env:JYPPX_CUDA_ROOT = (Resolve-Path -LiteralPath $CudaRoot).Path }
if ($CudnnRoot) { $env:JYPPX_CUDNN_ROOT = (Resolve-Path -LiteralPath $CudnnRoot).Path }
if ($NativeBridgeRoot) { $env:JYPPX_NATIVE_BRIDGE_PATH = (Resolve-Path -LiteralPath $NativeBridgeRoot).Path }
$env:DEPLOYSHARP_PADDLEOCR_VL_BACKEND_PROBE = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT = $bundle
$env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_TENSORRT_API = $TensorRtApi
$env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_PROFILE = $ProfilePreset
$env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_PRECISION = $Precision
$env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_WORKSPACE_GIB = [string]$WorkspaceGiB
$env:DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot record source commit.' }
$rows = [Collections.Generic.List[object]]::new()
foreach ($selectedBackend in $Backend) {
    $projectName = "DeploySharp.Backend.$selectedBackend.Tests"
    $project = Join-Path $repoRoot "tests\$projectName\$projectName.csproj"
    & dotnet build $project -c Release -f net10.0 --no-restore --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Probe build failed: $selectedBackend" }
    $assembly = Join-Path $repoRoot "tests\$projectName\bin\Release\net10.0\$projectName.dll"
    $testClass = if ($ProbeMode -eq 'ComponentInference') { 'PaddleOcrVlComponentInferenceProbeTests' }
        elseif ($selectedBackend -eq 'OpenCV') { 'PaddleOcrVlImporterProbeTests' }
        else { 'PaddleOcrVlBuilderProbeTests' }
    foreach ($role in $Graph) {
        $directory = Join-Path $output "$selectedBackend-$role"
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $reportPath = Join-Path $directory 'probe.json'
        if (Test-Path -LiteralPath $reportPath) { throw "Use a fresh output directory; report already exists: $reportPath" }
        $env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_GRAPH = $role
        $env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_REPORT = $reportPath
        if ($ProbeMode -eq 'ComponentInference') {
            $builderDirectory = Join-Path $EngineRoot "TensorRT-$role"
            $builderReportPath = Join-Path $builderDirectory 'probe.json'
            $builderReport = Get-Content -LiteralPath $builderReportPath -Raw | ConvertFrom-Json
            if ($builderReport.status -ne 'engine-built-no-inference') { throw "Builder did not produce an engine: $builderReportPath" }
            if ($builderReport.api -ne "TensorRt$TensorRtApi") { throw "Engine API does not match selected TensorRT API: $builderReportPath" }
            $enginePath = Join-Path $builderDirectory "$role.plan"
            if (-not (Test-Path -LiteralPath $enginePath -PathType Leaf)) { throw "Missing engine: $enginePath" }
            $env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_ENGINE = $enginePath
            $env:DEPLOYSHARP_PADDLEOCR_VL_PROBE_ENGINE_SHA256 = $builderReport.engineSha256
        }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = 'dotnet'
        $start.WorkingDirectory = $repoRoot
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        foreach ($argument in @('vstest', $assembly, "/TestCaseFilter:FullyQualifiedName~$testClass", '/Logger:console;verbosity=normal')) {
            $start.ArgumentList.Add($argument)
        }
        Write-Host "VL_BACKEND_PROBE backend=$selectedBackend graph=$role"
        $process = [Diagnostics.Process]::Start($start)
        if ($null -eq $process) { throw 'Unable to start isolated probe.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $timedOut = $false
        try {
            while (-not $process.WaitForExit(1000)) {
                if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
                    $timedOut = $true
                    $process.Kill($true)
                    $process.WaitForExit()
                    break
                }
            }
            $exitCode = $process.ExitCode
        }
        finally {
            if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
            [IO.File]::WriteAllText((Join-Path $directory 'stdout.log'), $stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $directory 'stderr.log'), $stderr.GetAwaiter().GetResult())
            $process.Dispose()
        }
        $result = if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
        $status = if ($timedOut) { 'timeout-unverified' }
            elseif ($null -eq $result) { 'no-report-unverified' }
            elseif ($result.status -eq 'probing') { 'process-ended-unverified' }
            else { $result.status }
        $rows.Add([pscustomobject][ordered]@{ backend = $selectedBackend; graph = $role; status = $status; exitCode = $exitCode
            timedOut = $timedOut; processMilliseconds = $watch.Elapsed.TotalMilliseconds; report = $reportPath
            reportSha256 = if ($result) { (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null } })
        Write-Host "VL_BACKEND_RESULT backend=$selectedBackend graph=$role status=$status"
        $summary = [ordered]@{ schemaVersion = 'deploysharp-paddleocr-vl-backend-probes-v1'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
            sourceCommit = $env:DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT; isolatedPerGraph = $true; timeoutSeconds = $TimeoutSeconds
            probeMode = $ProbeMode
            boundary = if ($ProbeMode -eq 'ComponentInference') { 'Selected component execution and fixed-input ONNX Runtime CPU comparison only. Untested graphs, empty-KV prefill, token parity and full generation remain unverified.' }
                else { 'Importer/Builder only. Imported/built graphs are not admitted as full inference; timeouts and missing/unfinished reports remain unverified.' }
            graphs = @($rows) }
        $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
    }
}
$rows | Format-Table backend, graph, status, exitCode
if (@($rows | Where-Object { $_.exitCode -ne 0 -or $_.status -match 'unverified|blocked|parity-failed' }).Count -gt 0) { exit 2 }
