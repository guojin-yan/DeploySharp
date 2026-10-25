[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BundleRoot,
    [Parameter(Mandatory)] [string] $ModelRoot,
    [Parameter(Mandatory)] [string] $FixtureRoot,
    [Parameter(Mandatory)] [string] $ReferencePath,
    [Parameter(Mandatory)] [string] $EngineRoot,
    [Parameter(Mandatory)] [string] $PrefillEngine,
    [string] $VisionEngine,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [ValidateSet('ocr-demo-1', 'table-official-example', 'formula-official-example', 'chart-official-example', 'spotting-demo-1', 'seal-official-example')]
    [string[]] $CaseId = @('ocr-demo-1', 'table-official-example', 'formula-official-example', 'chart-official-example', 'seal-official-example'),
    [ValidateRange(1, 512)] [int] $MaximumNewTokens = 512,
    [Parameter(Mandatory)] [string] $TensorRtRoot,
    [Parameter(Mandatory)] [string] $CudaRoot,
    [Parameter(Mandatory)] [string] $CudnnRoot,
    [Parameter(Mandatory)] [string] $NativeBridgePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$env:DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT = (Resolve-Path -LiteralPath $BundleRoot).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT = (Resolve-Path -LiteralPath $ModelRoot).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES = (Resolve-Path -LiteralPath $FixtureRoot).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_REFERENCE = (Resolve-Path -LiteralPath $ReferencePath).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_ENGINE_ROOT = (Resolve-Path -LiteralPath $EngineRoot).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_PREFILL_ENGINE = (Resolve-Path -LiteralPath $PrefillEngine).Path
if ($VisionEngine) { $env:DEPLOYSHARP_PADDLEOCR_VL_VISION_ENGINE = (Resolve-Path -LiteralPath $VisionEngine).Path }
else { Remove-Item Env:DEPLOYSHARP_PADDLEOCR_VL_VISION_ENGINE -ErrorAction SilentlyContinue }
$env:JYPPX_TENSORRT_ROOT = (Resolve-Path -LiteralPath $TensorRtRoot).Path
$env:JYPPX_CUDA_ROOT = (Resolve-Path -LiteralPath $CudaRoot).Path
$env:JYPPX_CUDNN_ROOT = (Resolve-Path -LiteralPath $CudnnRoot).Path
$env:JYPPX_NATIVE_BRIDGE_PATH = (Resolve-Path -LiteralPath $NativeBridgePath).Path
$env:DEPLOYSHARP_PADDLEOCR_VL_RUN_TENSORRT = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_MAX_TOKENS = [string]$MaximumNewTokens
$env:DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
$testSource = Join-Path $repoRoot 'tests\DeploySharp.Backend.TensorRT.Tests\PaddleOcrVlFiveGraphIntegrationTests.cs'
$env:DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256 = (Get-FileHash -LiteralPath $testSource -Algorithm SHA256).Hash.ToLowerInvariant()
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$project = Join-Path $repoRoot 'tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj'
& dotnet build $project -c Release -f net10.0 --no-restore --nologo -v quiet -p:SuppressTfmSupportBuildWarnings=true
if ($LASTEXITCODE -ne 0) { throw 'The TensorRT test project did not build.' }
$assembly = Join-Path $repoRoot 'tests\DeploySharp.Backend.TensorRT.Tests\bin\Release\net10.0\DeploySharp.Backend.TensorRT.Tests.dll'
$rows = [Collections.Generic.List[object]]::new()
foreach ($id in $CaseId) {
    $report = Join-Path $output "$id.json"
    if (Test-Path -LiteralPath $report) { throw "Use a fresh output directory; report already exists: $report" }
    $env:DEPLOYSHARP_PADDLEOCR_VL_CASE_ID = $id
    $env:DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_REPORT = $report
    Write-Host "VL_TENSORRT_GENERATION case=$id"
    & dotnet vstest $assembly '/TestCaseFilter:FullyQualifiedName~PaddleOcrVlFiveGraphIntegrationTests' '/Logger:console;verbosity=normal'
    $exitCode = $LASTEXITCODE
    $result = if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Raw | ConvertFrom-Json } else { $null }
    $rows.Add([pscustomobject][ordered]@{
        caseId = $id
        status = if ($result) { $result.status } else { 'no-report' }
        exitCode = $exitCode
        report = $report
        reportSha256 = if ($result) { (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    })
    Write-Host "VL_TENSORRT_RESULT case=$id status=$($rows[-1].status) exit=$exitCode"
}
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-tensorrt-five-graph-summary-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceCommit = $env:DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT
    testSourceSha256 = $env:DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256
    maximumNewTokens = $MaximumNewTokens
    cases = @($rows)
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
$rows | Format-Table caseId, status, exitCode
if (@($rows | Where-Object { $_.exitCode -ne 0 -or $_.status -ne 'eos-token-text-exact' }).Count -gt 0) { exit 2 }
