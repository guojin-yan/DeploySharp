[CmdletBinding()]
param(
    [string]$ReportPath = (Join-Path $PSScriptRoot '..\verification\hiertext-natural-wide-coverage-a2-20261017.json'),
    [string]$DestinationRoot,
    [string]$AcquisitionManifestPath,
    [string]$BenchmarkManifestPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Resolve-File([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

$reportFullPath = Resolve-File $ReportPath 'A2 coverage report'
$report = Get-Content -LiteralPath $reportFullPath -Raw | ConvertFrom-Json
if ([string]$report.schemaVersion -ne 'deploysharp-paddleocr-hiertext-natural-wide-a2-v1') {
    throw "Unsupported A2 report schema: $($report.schemaVersion)"
}

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = [string]$report.source.imageRoot
}
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    throw 'DestinationRoot is empty and the report has no source.imageRoot.'
}
$destination = [IO.Path]::GetFullPath($DestinationRoot)
$null = New-Item -ItemType Directory -Force -Path $destination

if ([string]::IsNullOrWhiteSpace($AcquisitionManifestPath)) {
    $AcquisitionManifestPath = Join-Path (Split-Path -Parent $destination) 'natural-wide-a2-20261017-acquisition.json'
}
$acquisitionFullPath = [IO.Path]::GetFullPath($AcquisitionManifestPath)
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $acquisitionFullPath)

if ([string]::IsNullOrWhiteSpace($BenchmarkManifestPath)) {
    $BenchmarkManifestPath = Join-Path (Split-Path -Parent $destination) 'natural-wide-a2-20261017-manifest.jsonl'
}
$benchmarkManifestFullPath = [IO.Path]::GetFullPath($BenchmarkManifestPath)
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $benchmarkManifestFullPath)

$candidateIds = @($report.candidates | ForEach-Object { [string]$_.imageId } | Sort-Object -Unique)
if ($candidateIds.Count -eq 0) {
    throw 'The A2 report contains no candidate image IDs.'
}

# HierText's official repository documents the CVDF archive at
# s3://open-images-dataset/ocr/validation.tgz.  The public S3 object layout also
# exposes each validation JPEG under this prefix, which avoids downloading the
# complete ~577 MB archive for a nine-image audit.  Do not replace this with an
# unrelated mirror: the object URL and per-file SHA are part of the evidence.
$sourceTemplate = 'https://open-images-dataset.s3.amazonaws.com/ocr/validation/{image_id}.jpg'
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($imageId in $candidateIds) {
    if ($imageId -notmatch '^[0-9a-fA-F]{16}$') {
        throw "Unexpected HierText/Open Images image ID: $imageId"
    }
    $target = Join-Path $destination ($imageId.ToLowerInvariant() + '.jpg')
    $url = $sourceTemplate.Replace('{image_id}', $imageId.ToLowerInvariant())
    if ((Test-Path -LiteralPath $target -PathType Leaf) -and -not $Force) {
        $bytes = (Get-Item -LiteralPath $target).Length
        if ($bytes -le 0) { throw "Existing candidate image is empty: $target (use -Force to replace it)." }
        $status = 'existing'
    } else {
        $temporary = "$target.download"
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -OutFile $temporary -TimeoutSec 120
            if (-not (Test-Path -LiteralPath $temporary -PathType Leaf)) {
                throw "The download did not create a file: $url"
            }
            $bytes = (Get-Item -LiteralPath $temporary).Length
            if ($bytes -le 0) { throw "The downloaded object is empty: $url" }
            Move-Item -LiteralPath $temporary -Destination $target -Force
            $status = 'downloaded'
        } catch {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
            throw "Unable to acquire HierText candidate '$imageId' from '$url': $($_.Exception.Message)"
        }
    }

    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $rows.Add([ordered]@{
        imageId = $imageId.ToLowerInvariant()
        path = $target
        sourceUrl = $url
        status = $status
        sizeBytes = [int64]$bytes
        sha256 = $hash
    })
    Write-Host "$imageId`t$status`t$bytes bytes`t$hash"
}

$result = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-hiertext-natural-wide-a2-acquisition-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    reportPath = $reportFullPath
    reportSha256 = (Get-FileHash -LiteralPath $reportFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceRevision = [string]$report.source.sourceRevision
    annotationVersion = [string]$report.source.annotationVersion
    datasetBoundary = 'Images are fetched from the official Open Images CVDF object path for local verification only. Per-image Open Images license/attribution metadata was not present in the supplied local CSV; do not redistribute these files or claim a release license from this manifest.'
    sourceTemplate = $sourceTemplate
    destinationRoot = $destination
    benchmarkManifestPath = $benchmarkManifestFullPath
    candidates = @($rows)
}
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $acquisitionFullPath -Encoding utf8

$datasetParent = (Split-Path -Parent $destination)
$manifestLines = @($candidateIds | ForEach-Object {
    $imageId = $_
    $imageCandidates = @($report.candidates | Where-Object { [string]$_.imageId -eq $imageId } | ForEach-Object {
        [ordered]@{
            paragraphIndex = [int]$_.paragraphIndex
            lineIndex = [int]$_.lineIndex
            textLength = [int]$_.textLength
            projectedWidth = [int]$_.projectedWidth
            bboxWidth = [double]$_.bboxWidth
            bboxHeight = [double]$_.bboxHeight
            vertices = @($_.vertices)
        }
    })
    [ordered]@{
        schemaVersion = 1
        image_id = $imageId.ToLowerInvariant()
        image_relpath = 'validation/' + $imageId.ToLowerInvariant() + '.jpg'
        image_path = (Join-Path $destination ($imageId.ToLowerInvariant() + '.jpg'))
        candidate_lines = $imageCandidates
    } | ConvertTo-Json -Depth 8 -Compress
})
[IO.File]::WriteAllLines($benchmarkManifestFullPath, $manifestLines, [Text.UTF8Encoding]::new($false))
Write-Host "HIERtext_A2_ACQUISITION=$acquisitionFullPath"
Write-Host "HIERtext_A2_CANDIDATES=$($rows.Count)"
Write-Host "HIERtext_A2_BENCHMARK_MANIFEST=$benchmarkManifestFullPath"
