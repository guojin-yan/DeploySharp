param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$DatasetRoot,

    [string]$ModelRoot = 'E:\Model\paddleocr',
    [string]$BenchmarkDll = 'tools/DeploySharp.PaddleOcrBenchmark/bin/Release/net10.0/DeploySharp.PaddleOcrBenchmark.dll',
    [string]$OutputDirectory,
    [string]$Version = 'v5',
    [string]$Variant = 'mobile',
    [string]$Backend = 'onnxruntime',
    [int]$StartIndex = 0,
    [int]$MaxImages = 0,
    [int]$Warmup = 3,
    [int]$Iterations = 5,
    [int]$BatchSize = 16,
    [int]$InferenceChannels = 1,
    [int]$MaximumRegions = 1024,
    [ValidateSet('Clamp', 'Reject', 'SlidingWindow')]
    [string]$OverflowMode = 'Clamp',
    [double]$WindowOverlap = 0.2,
    [int]$MaximumWindowsPerRegion = 32,
    [int]$PipelineTimeoutMs = 60000,
    [switch]$ContinueOnFailure
)

$ErrorActionPreference = 'Stop'

function Resolve-ExistingPath([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$manifest = Resolve-ExistingPath $ManifestPath 'Manifest'
$benchmarkPath = if ([System.IO.Path]::IsPathRooted($BenchmarkDll)) { $BenchmarkDll } else { Join-Path $repoRoot $BenchmarkDll }
$benchmark = Resolve-ExistingPath $benchmarkPath 'Benchmark assembly'
if (-not (Test-Path -LiteralPath $DatasetRoot -PathType Container)) { throw "Dataset root does not exist: $DatasetRoot" }
if (-not (Test-Path -LiteralPath $ModelRoot -PathType Container)) { throw "Model root does not exist: $ModelRoot" }
$dataset = (Resolve-Path -LiteralPath $DatasetRoot).Path
$models = (Resolve-Path -LiteralPath $ModelRoot).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDirectory = Join-Path $repoRoot "artifacts/public-ocr-evaluation/$Version-$Variant-$Backend-$stamp"
} elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Output directory already exists; choose a new path to preserve prior evidence: $OutputDirectory"
}
$null = New-Item -ItemType Directory -Path $OutputDirectory
$rawRoot = Join-Path $OutputDirectory 'raw'
$csvRoot = Join-Path $OutputDirectory 'csv'
$null = New-Item -ItemType Directory -Path $rawRoot
$null = New-Item -ItemType Directory -Path $csvRoot

$records = Get-Content -LiteralPath $manifest | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_ | ConvertFrom-Json }
if ($records.Count -eq 0) { throw "Manifest contains no records: $manifest" }
if ($StartIndex -lt 0 -or $StartIndex -ge $records.Count) { throw "StartIndex must be within 0..$($records.Count - 1)." }
if ($Warmup -lt 0 -or $Iterations -lt 1 -or $BatchSize -lt 1 -or $InferenceChannels -lt 1 -or $MaximumRegions -lt 1 -or $MaximumWindowsPerRegion -lt 2 -or $PipelineTimeoutMs -lt 1) {
    throw 'Warmup must be non-negative; iterations, batch, inference channels, maximum regions, maximum windows, and timeout must be positive.'
}
if ($WindowOverlap -le 0 -or $WindowOverlap -gt 0.5) { throw 'WindowOverlap must be greater than zero and no greater than 0.5.' }
$selected = @($records | Select-Object -Skip $StartIndex)
if ($MaxImages -gt 0) { $selected = @($selected | Select-Object -First $MaxImages) }
$selectedManifestPath = Join-Path $OutputDirectory 'selected-manifest.jsonl'
$selectedLines = @($selected | ForEach-Object { $_ | ConvertTo-Json -Depth 30 -Compress })
[System.IO.File]::WriteAllLines($selectedManifestPath, $selectedLines, [System.Text.UTF8Encoding]::new($false))

$revision = (git -C $repoRoot rev-parse HEAD).Trim()
$sourceStatus = @(git -C $repoRoot status --porcelain=v1 --untracked-files=all)
$sourceStatusText = [string]::Join("`n", $sourceStatus)
$sourceStatusSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($sourceStatusText))).ToLowerInvariant()
$benchmarkSha256 = (Get-FileHash -LiteralPath $benchmark -Algorithm SHA256).Hash.ToLowerInvariant()
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = $revision
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = $Backend
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = "$Version-$Variant"
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = [string]$Warmup
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = [string]$Iterations
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = [string]$InferenceChannels
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = [string]$BatchSize
$env:DEPLOYSHARP_PADDLEOCR_MAX_REGIONS = [string]$MaximumRegions
$env:DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE = $OverflowMode
$env:DEPLOYSHARP_PADDLEOCR_WINDOW_OVERLAP = [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:R}', $WindowOverlap)
$env:DEPLOYSHARP_PADDLEOCR_WINDOWS_PER_REGION = [string]$MaximumWindowsPerRegion
$env:DEPLOYSHARP_PADDLEOCR_PIPELINE_TIMEOUT_MS = [string]$PipelineTimeoutMs
$env:DEPLOYSHARP_PADDLEOCR_INTER_TEST_DELAY_MS = '0'
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = $rawRoot

$index = 0
foreach ($record in $selected) {
    $index++
    $relativeImage = ([string]$record.image_relpath).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $image = Join-Path $dataset $relativeImage
    if (-not (Test-Path -LiteralPath $image -PathType Leaf)) {
        throw "Manifest image is missing: $image"
    }
    $safeId = ([string]$record.image_id) -replace '[^A-Za-z0-9._-]', '_'
    $env:DEPLOYSHARP_PADDLEOCR_IMAGE = (Resolve-Path -LiteralPath $image).Path
    $csv = Join-Path $csvRoot "$safeId.csv"
    $log = Join-Path $OutputDirectory "$safeId.log"
    $imageReportDirectory = Join-Path $rawRoot $safeId
    $null = New-Item -ItemType Directory -Path $imageReportDirectory
    $env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = $imageReportDirectory
    Write-Host "[$index/$($selected.Count)] $($record.image_id)"
    & dotnet $benchmark $models $csv *> $log
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Get-Content -LiteralPath $log -Tail 80 | ForEach-Object { [Console]::Error.WriteLine($_) }
        if ($ContinueOnFailure) {
            Write-Warning "Benchmark process failed for $($record.image_id) with exit code $exitCode; preserving the failure row and continuing."
            continue
        }
        throw "Benchmark failed for $($record.image_id) with exit code $exitCode. See $log"
    }
    $runCsv = Import-Csv -LiteralPath $csv
    $runRows = @($runCsv)
    if ($runRows.Count -ne 1 -or $runRows[0].status -ne 'pass') {
        if ($ContinueOnFailure -and $runRows.Count -eq 1) {
            Write-Warning "Benchmark status for $($record.image_id) is '$($runRows[0].status)'; preserving its failure row and continuing."
            continue
        }
        throw "Expected exactly one passing model/backend row for $($record.image_id). See $csv"
    }
    $report = Get-ChildItem -LiteralPath $imageReportDirectory -Filter "$Version-$Variant-$Backend-*.width.json" -File | Select-Object -First 1
    if ($null -eq $report) {
        if ($ContinueOnFailure) {
            Write-Warning "No successful width report was produced for $($record.image_id); preserving the timing failure row and continuing."
            continue
        }
        throw "The benchmark did not produce a matching width report for $($record.image_id)."
    }
}

$runMetadata = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceRevision = $revision
    sourceWorkingTreeDirty = ($sourceStatus.Count -gt 0)
    sourceWorkingTreeStatusSha256 = $sourceStatusSha256
    benchmarkAssemblySha256 = $benchmarkSha256
    model = "$Version/$Variant"
    backend = $Backend
    manifest = (Resolve-Path -LiteralPath $manifest).Path
    manifestSha256 = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
    selectedManifest = $selectedManifestPath
    selectedManifestSha256 = (Get-FileHash -LiteralPath $selectedManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    datasetRoot = $dataset
    selectedImageCount = $selected.Count
    startIndex = $StartIndex
    warmup = $Warmup
    iterations = $Iterations
    batchSize = $BatchSize
    inferenceChannels = $InferenceChannels
    overflowMode = $OverflowMode
    windowOverlap = $WindowOverlap
    maximumWindowsPerRegion = $MaximumWindowsPerRegion
    pipelineTimeoutMs = $PipelineTimeoutMs
    continueOnFailure = [bool]$ContinueOnFailure
    modelRoot = $models
    machine = $env:COMPUTERNAME
    operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    processorCount = [Environment]::ProcessorCount
}
$runMetadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'run.json') -Encoding utf8
Write-Host "PADDLEOCR_DATASET_RAW=$rawRoot"
Write-Host "PADDLEOCR_DATASET_CSV=$csvRoot"
Write-Host "PADDLEOCR_DATASET_RUN=$(Join-Path $OutputDirectory 'run.json')"
