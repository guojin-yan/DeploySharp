#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\paddleocr',
    [string]$BenchmarkImage = 'E:\Data\ocr\demo_1.jpg',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectDirectory '..\..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '..\paddleocr投稿\性能测试包'))
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$packageName = 'DeploySharp-PaddleOCR-Benchmark-win-x64'
$packageDirectory = Join-Path $OutputRoot $packageName
$zipPath = Join-Path $OutputRoot ($packageName + '.zip')
$projectPath = Join-Path $projectDirectory 'DeploySharp.PaddleOcrBenchmark.csproj'

foreach ($requiredPath in @($projectPath, $ModelRoot, $BenchmarkImage)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required input is missing: $requiredPath" }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDirectory) {
    $resolvedPackage = [IO.Path]::GetFullPath($packageDirectory)
    if (-not $resolvedPackage.StartsWith($OutputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedPackage) -ne $packageName) {
        throw "Refusing to replace unexpected package directory: $resolvedPackage"
    }
    Remove-Item -LiteralPath $resolvedPackage -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:DeploySharpPaddleOcrCuda12=true -p:NuGetLockFilePath=obj\paddleocr-cuda12.packages.lock.json -p:PublishSingleFile=false -p:DebugSymbols=false -p:DebugType=None -o $packageDirectory
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

foreach ($portableFile in @('Run-PaddleOcrBenchmark.ps1', 'run-benchmark.cmd', 'build-tensorrt-engines.cmd', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $portableFile) -Destination (Join-Path $packageDirectory $portableFile) -Force
}

$engineBuilder = Join-Path $projectDirectory 'Build-TensorRtEngines.ps1'
if (Test-Path -LiteralPath $engineBuilder) {
    Copy-Item -LiteralPath $engineBuilder -Destination (Join-Path $packageDirectory 'Build-TensorRtEngines.ps1') -Force
}

$packageModelRoot = Join-Path $packageDirectory 'models'
$modelFiles = @(Get-ChildItem -LiteralPath $ModelRoot -Recurse -File | Where-Object { $_.Extension -in @('.onnx', '.txt', '.yml', '.yaml') })
foreach ($modelFile in $modelFiles) {
    $relative = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($ModelRoot), $modelFile.FullName)
    $destination = Join-Path $packageModelRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $modelFile.FullName -Destination $destination -Force
}

$imageDirectory = Join-Path $packageDirectory 'images\benchmark'
New-Item -ItemType Directory -Path $imageDirectory -Force | Out-Null
Copy-Item -LiteralPath $BenchmarkImage -Destination (Join-Path $imageDirectory 'demo_1.jpg') -Force
New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'results') -Force | Out-Null

$sourceRevision = try { (git -C $repositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1) } catch { 'unknown' }
@(
    "package=$packageName",
    "builtAtUtc=$([DateTimeOffset]::UtcNow.ToString('O'))",
    'runtimeIdentifier=win-x64',
    'selfContained=true',
    "modelFileCount=$($modelFiles.Count)",
    "sourceRevision=$sourceRevision"
) | Set-Content -LiteralPath (Join-Path $packageDirectory 'BUILD-INFO.txt') -Encoding UTF8

$manifestLines = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File |
    Where-Object { $_.Name -ne 'manifest.sha256' -and $_.FullName -notlike (Join-Path $packageDirectory 'results\*') } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($packageDirectory, $_.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$relative"
    }
$manifestLines | Set-Content -LiteralPath (Join-Path $packageDirectory 'manifest.sha256') -Encoding ASCII

Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

$packageBytes = (Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Measure-Object Length -Sum).Sum
[pscustomobject]@{
    PackageDirectory = $packageDirectory
    ZipPath = $zipPath
    Files = (Get-ChildItem -LiteralPath $packageDirectory -Recurse -File).Count
    PackageMiB = [Math]::Round($packageBytes / 1MB, 2)
    ZipMiB = [Math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 2)
}
