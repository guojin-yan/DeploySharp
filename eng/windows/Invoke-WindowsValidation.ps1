[CmdletBinding()]
param(
    [ValidateSet('preflight', 'managed', 'native', 'full')]
    [string]$Mode = 'managed',
    [string]$RepositoryRoot,
    [string]$ReportPath,
    [string]$PackageCache,
    [switch]$IncludeExternal,
    [switch]$ContinueOnError,
    [switch]$SkipRestore,
    [switch]$UseGlobalPackageCache
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\', '/')
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ReportPath = Join-Path $repository (Join-Path 'artifacts\windows-validation' "$stamp-$Mode.json")
}
$reportFullPath = [IO.Path]::GetFullPath($ReportPath)
$reportDirectory = Split-Path -Parent $reportFullPath
$logDirectory = Join-Path $reportDirectory ((Split-Path -Leaf $reportFullPath) -replace '\.json$', '.logs')
New-Item -ItemType Directory -Force -Path $reportDirectory, $logDirectory | Out-Null
$ownsPackageCache = $false
if ($Mode -ne 'preflight' -and -not $SkipRestore -and -not $UseGlobalPackageCache -and [string]::IsNullOrWhiteSpace($PackageCache)) {
    $PackageCache = Join-Path ([IO.Path]::GetTempPath()) ('deploysharp-validation-packages-' + [Guid]::NewGuid().ToString('N'))
    $ownsPackageCache = $true
}
if (-not [string]::IsNullOrWhiteSpace($PackageCache)) { New-Item -ItemType Directory -Force -Path $PackageCache | Out-Null }

function Get-GitValue {
    param([string[]]$Arguments)
    try {
        $value = (& git -C $repository @Arguments 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -eq 0) { return $value }
    }
    catch { }
    return $null
}

$startedAt = [DateTimeOffset]::Now
$steps = [Collections.Generic.List[object]]::new()
$stepIndex = 0
$overallStatus = 'passed'

$source = [ordered]@{
    repository = $repository
    commit = Get-GitValue @('rev-parse', 'HEAD')
    branch = Get-GitValue @('branch', '--show-current')
    dirty = [bool](-not [string]::IsNullOrWhiteSpace((Get-GitValue @('status', '--porcelain'))))
}

$runtimeInfo = [ordered]@{
    osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    machineName = [Environment]::MachineName
    userName = [Environment]::UserName
    dotnetVersion = $null
    packageCache = if ($UseGlobalPackageCache) { 'global' } elseif ([string]::IsNullOrWhiteSpace($PackageCache)) { $null } else { $PackageCache }
    configuredDeploySharpVariables = @(Get-ChildItem Env: -ErrorAction SilentlyContinue | Where-Object Name -like 'DEPLOYSHARP_*' | ForEach-Object Name | Sort-Object)
}
try { $runtimeInfo.dotnetVersion = (& dotnet --version 2>$null | Out-String).Trim() } catch { }

$report = [ordered]@{
    schemaVersion = '1.0'
    tool = 'DeploySharp Windows validation runner'
    mode = $Mode
    includeExternal = [bool]$IncludeExternal
    startedAt = $startedAt.ToString('o')
    source = $source
    runtime = $runtimeInfo
    steps = $steps
    status = 'running'
}

function Write-Report {
    $report.status = $overallStatus
    $report.completedAt = [DateTimeOffset]::Now.ToString('o')
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportFullPath -Encoding utf8
}

function Invoke-ValidationStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [switch]$Required
    )

    $script:stepIndex++
    $started = [DateTimeOffset]::Now
    $logPath = Join-Path $logDirectory ('{0:D2}-{1}.log' -f $script:stepIndex, ($Name -replace '[^A-Za-z0-9._-]', '-'))
    Write-Host ("[{0}/{1}] {2}" -f $script:stepIndex, $Mode, $Name)
    $output = @()
    $exitCode = 0
    try {
        $output = @(& $FilePath @Arguments 2>&1)
        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    }
    catch {
        $output += $_
        $exitCode = 1
    }
    $output | Out-File -LiteralPath $logPath -Encoding utf8
    $status = if ($exitCode -eq 0) { 'passed' } elseif ($Required) { 'failed' } else { 'skipped' }
    if ($status -eq 'failed') { $script:overallStatus = 'failed' }
    $steps.Add([ordered]@{
        name = $Name
        command = (($FilePath + ' ' + ($Arguments -join ' ')).Trim())
        status = $status
        exitCode = $exitCode
        startedAt = $started.ToString('o')
        completedAt = [DateTimeOffset]::Now.ToString('o')
        log = [IO.Path]::GetRelativePath($reportDirectory, $logPath)
    })
    Write-Report
    if ($status -eq 'failed' -and -not $ContinueOnError) { throw "Validation step failed: $Name. See $logPath." }
}

function Add-Step {
    param([string]$Name, [string]$FilePath, [string[]]$Arguments = @(), [bool]$Required = $true)
    [pscustomobject]@{ Name = $Name; FilePath = $FilePath; Arguments = $Arguments; Required = $Required }
}

$stepDefinitions = [Collections.Generic.List[object]]::new()
$stepDefinitions.Add((Add-Step 'dotnet-info' 'dotnet' @('--info')))
$stepDefinitions.Add((Add-Step 'git-version' 'git' @('--version')))
if ($Mode -in @('managed', 'native', 'full')) {
    if (-not $SkipRestore) {
        $restoreArguments = @('restore', 'DeploySharp.sln', '--locked-mode')
        if (-not [string]::IsNullOrWhiteSpace($PackageCache)) { $restoreArguments += @('--packages', $PackageCache) }
        $stepDefinitions.Add((Add-Step 'locked-restore' 'dotnet' $restoreArguments))
    }
}
if ($Mode -in @('managed', 'full')) {
    $stepDefinitions.Add((Add-Step 'solution-build' 'dotnet' @('build', 'DeploySharp.sln', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'solution-test' 'dotnet' @('test', 'DeploySharp.sln', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'core-sample' 'dotnet' @('run', '--project', 'samples/01-core/CoreContractInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'visual-sample' 'dotnet' @('run', '--project', 'samples/02-visual/VisualProfileInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'backend-sample' 'dotnet' @('run', '--project', 'samples/03-backends/OpenCvDnnContractInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'multimodal-sample' 'dotnet' @('run', '--project', 'samples/04-multimodal/MultimodalContractInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'llm-sample' 'dotnet' @('run', '--project', 'samples/05-llm/LlmPromptInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'model-factory-sample' 'dotnet' @('run', '--project', 'samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj', '-c', 'Release', '--no-restore')))
    $stepDefinitions.Add((Add-Step 'modelfactory-cli-contract' 'pwsh' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', './eng/model-catalog/Test-ModelFactoryCli.ps1')))
    $stepDefinitions.Add((Add-Step 'inference-speed-benchmark-contract' 'pwsh' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', './eng/benchmarks/Test-InferenceSpeedBenchmark.ps1')))
    $stepDefinitions.Add((Add-Step 'model-case-coverage' 'pwsh' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', './eng/model-catalog/Test-ModelSampleCoverage.ps1')))
    $stepDefinitions.Add((Add-Step 'platform-support-matrix' 'pwsh' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', './eng/platform/Test-PlatformSupportMatrix.ps1')))
}
if ($Mode -in @('native', 'full')) {
    $benchmarkOutput = (Join-Path $logDirectory 'benchmark.json').Replace('\', '/')
    $stepDefinitions.Add((Add-Step 'inference-speed-benchmark' 'dotnet' @('run', '--project', 'samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj', '-c', 'Release', '--no-build', '--no-restore', '--', '--backend', 'all', '--warmup', '10', '--iterations', '100', '--output', $benchmarkOutput) $false))
}
if ($IncludeExternal) {
    $externalProjects = @(
        'tests/DeploySharp.Backend.OnnxRuntime.Tests/DeploySharp.Backend.OnnxRuntime.Tests.csproj',
        'tests/DeploySharp.Backend.OpenVINO.Tests/DeploySharp.Backend.OpenVINO.Tests.csproj',
        'tests/DeploySharp.Backend.OpenCV.Tests/DeploySharp.Backend.OpenCV.Tests.csproj',
        'tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj',
        'tests/DeploySharp.Backend.TensorRT.Tests/DeploySharp.Backend.TensorRT.Tests.csproj'
    )
    foreach ($project in $externalProjects) {
        $name = [IO.Path]::GetFileNameWithoutExtension($project) + '-external'
        $stepDefinitions.Add((Add-Step $name 'dotnet' @('test', $project, '-c', 'Release', '--no-build', '--no-restore', '--filter', 'TestCategory=ExternalModels')))
    }
}

Push-Location $repository
try {
    foreach ($definition in $stepDefinitions) {
        Invoke-ValidationStep -Name $definition.Name -FilePath $definition.FilePath -Arguments $definition.Arguments -Required:$definition.Required
    }
}
finally {
    Pop-Location
    Write-Report
    if ($ownsPackageCache -and (Test-Path -LiteralPath $PackageCache)) { Remove-Item -LiteralPath $PackageCache -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($overallStatus -eq 'failed') { exit 1 }
Write-Output ("DEPLOYSHARP_WINDOWS_VALIDATION_OK mode={0} steps={1} report={2}" -f $Mode, $steps.Count, $reportFullPath)
