param(
    [string]$DatasetRoot = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815',
    [switch]$Download,
    [switch]$Extract
)

$ErrorActionPreference = 'Stop'
$archivePath = Join-Path $DatasetRoot 'realFormula.zip'
$extractedRoot = Join-Path $DatasetRoot 'extracted\realFormula-public'
$expectedMd5 = '67a54c3708d3e9fa324819a9325731d3'
$downloadUrl = 'https://zenodo.org/records/11296815/files/realFormula.zip?download=1'

New-Item -ItemType Directory -Force -Path $DatasetRoot | Out-Null
if ($Download) {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
}

if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
    $actualMd5 = (Get-FileHash -LiteralPath $archivePath -Algorithm MD5).Hash.ToLowerInvariant()
    if ($actualMd5 -ne $expectedMd5) {
        throw "realFormula archive checksum mismatch. Expected $expectedMd5; got $actualMd5."
    }
}
elseif ($Extract -or !(Test-Path -LiteralPath $extractedRoot -PathType Container)) {
    throw "Archive is missing: $archivePath. Pass -Download to fetch the pinned Zenodo v1 file."
}

if ($Extract) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $normalized = $entry.FullName.Replace('\', '/')
            if ($normalized.StartsWith('/') -or $normalized -match '^[A-Za-z]:' -or $normalized -match '(^|/)\.\.(?:/|$)') {
                throw "Unsafe path in dataset archive: $($entry.FullName)"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
    $extractParent = Join-Path $DatasetRoot 'extracted'
    New-Item -ItemType Directory -Force -Path $extractParent | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractParent -Force
}

$csvPath = Join-Path $extractedRoot 'test-corrected_normalized.csv'
$imageRoot = Join-Path $extractedRoot 'img-corrected'
if (!(Test-Path -LiteralPath $csvPath -PathType Leaf) -or !(Test-Path -LiteralPath $imageRoot -PathType Container)) {
    throw "Expected realFormula v1 files were not found under $extractedRoot."
}

$rows = @(Import-Csv -LiteralPath $csvPath)
if ($rows.Count -ne 121) {
    throw "Expected 121 manually annotated realFormula rows; found $($rows.Count)."
}
$csvSha256 = (Get-FileHash -LiteralPath $csvPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifestPath = Join-Path $extractedRoot 'realFormula-manifest.jsonl'
$manifestRows = [System.Collections.Generic.List[string]]::new()
$seenImages = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $rows) {
    $properties = @($row.PSObject.Properties)
    if ($properties.Count -lt 2) { throw 'CSV row has fewer than two fields.' }
    $reference = [string]$properties[0].Value
    $image = [string]$properties[1].Value
    if ([string]::IsNullOrWhiteSpace($reference) -or [string]::IsNullOrWhiteSpace($image)) {
        throw 'CSV contains an empty formula label or image name.'
    }
    if (!$seenImages.Add($image)) { throw "Duplicate image name in labels: $image" }
    $imagePath = Join-Path $imageRoot $image
    if (!(Test-Path -LiteralPath $imagePath -PathType Leaf)) { throw "Annotated image is missing: $imagePath" }
    $manifestRows.Add(([ordered]@{
        Image = $image
        ImageRelPath = "img-corrected/$image"
        ImageSha256 = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        ReferenceLatex = $reference
    } | ConvertTo-Json -Compress))
}
$manifestRows -join [Environment]::NewLine | Set-Content -LiteralPath $manifestPath -Encoding utf8
$manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()

[pscustomobject]@{
    dataset = 'MathNet realFormula'
    zenodoRecord = '11296815'
    version = 'v1'
    doi = '10.5281/zenodo.11296815'
    license = 'CC-BY-4.0'
    archivePath = $archivePath
    archiveMd5 = if (Test-Path -LiteralPath $archivePath -PathType Leaf) { (Get-FileHash -LiteralPath $archivePath -Algorithm MD5).Hash.ToLowerInvariant() } else { $null }
    archiveSha256 = if (Test-Path -LiteralPath $archivePath -PathType Leaf) { (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    csvSha256 = $csvSha256
    annotationCount = $rows.Count
    uniqueImageCount = $seenImages.Count
    verifiedImageCount = $manifestRows.Count
    manifestPath = $manifestPath
    manifestSha256 = $manifestSha256
    attribution = 'Schmitt-Koopmann, Felix; Huang, Elaine; Hutter, Hans-Peter; Stadelmann, Thilo; Darvishy, Alireza. MER dataset realFormula. Zenodo, v1, 2024. https://doi.org/10.5281/zenodo.11296815'
} | Format-List
