[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AnnotationsPath,
    [string]$ImageRoot = 'F:\OCRBenchmarkTesting\data\images\hiertext\validation',
    [string]$MetadataPath = 'F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation-images-with-rotation.csv',
    [int]$TargetHeight = 48,
    [int]$MinimumProjectedWidth = 3200,
    [string]$OutputJsonPath = 'eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.json',
    [string]$OutputMarkdownPath = 'eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.md'
)

$ErrorActionPreference = 'Stop'

function Resolve-RepositoryRoot {
    $candidate = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    if (-not (Test-Path -LiteralPath (Join-Path $candidate '.git'))) {
        throw "Unable to resolve the DeploySharp repository root from $PSScriptRoot."
    }
    return $candidate
}

function Get-GzipJson {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionMode]::Decompress)
        try {
            $reader = [IO.StreamReader]::new($gzip)
            try { return ($reader.ReadToEnd() | ConvertFrom-Json) }
            finally { $reader.Dispose() }
        }
        finally { $gzip.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$repositoryRoot = Resolve-RepositoryRoot
$annotationsFullPath = (Resolve-Path -LiteralPath $AnnotationsPath).Path
$annotationDocument = Get-GzipJson -Path $annotationsFullPath
$annotationRevision = 'unknown'
if ([IO.Path]::GetFileName($annotationsFullPath) -match 'validation-([0-9a-f]{40})\.jsonl\.gz$') {
    $annotationRevision = $Matches[1]
}
$annotationVersion = 'unknown'
if ($annotationDocument.info -and $annotationDocument.info.version) {
    $annotationVersion = [string]$annotationDocument.info.version
}
$annotationRelativePath = $annotationsFullPath
if ($annotationsFullPath.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    $annotationRelativePath = $annotationsFullPath.Substring($repositoryRoot.Length).TrimStart('\', '/')
}
$metadataFullPath = $null
$metadataById = @{}
if (Test-Path -LiteralPath $MetadataPath -PathType Leaf) {
    $metadataFullPath = (Resolve-Path -LiteralPath $MetadataPath).Path
    foreach ($row in (Import-Csv -LiteralPath $metadataFullPath)) {
        $id = [string]$row.ImageID
        if (-not [string]::IsNullOrWhiteSpace($id) -and -not $metadataById.ContainsKey($id)) {
            $metadataById[$id] = $row
        }
    }
}

$candidates = [System.Collections.Generic.List[object]]::new()
$annotations = @($annotationDocument.annotations)
for ($paragraphIndex = 0; $paragraphIndex -lt $annotations.Count; $paragraphIndex++) {
    $annotation = $annotations[$paragraphIndex]
    $paragraphs = @($annotation.paragraphs)
    for ($p = 0; $p -lt $paragraphs.Count; $p++) {
        $lines = @($paragraphs[$p].lines)
        for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
            $line = $lines[$lineIndex]
            $text = [string]$line.text
            if ([string]::IsNullOrWhiteSpace($text)) { continue }
            $vertices = @($line.vertices)
            if ($vertices.Count -lt 4) { continue }
            $xs = @($vertices | ForEach-Object { [double]$_[0] })
            $ys = @($vertices | ForEach-Object { [double]$_[1] })
            $width = (($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum)
            $height = (($ys | Measure-Object -Maximum).Maximum - ($ys | Measure-Object -Minimum).Minimum)
            if ($height -le 0 -or $width -le 0) { continue }
            $projectedWidth = [int][Math]::Ceiling($width * $TargetHeight / $height)
            if ($projectedWidth -lt $MinimumProjectedWidth) { continue }

            $imageId = [string]$annotation.image_id
            $imagePath = Join-Path $ImageRoot ($imageId + '.jpg')
            $exists = Test-Path -LiteralPath $imagePath -PathType Leaf
            $metadata = if ($metadataById.ContainsKey($imageId)) { $metadataById[$imageId] } else { $null }
            $imageSha = $null
            $imageSize = $null
            if ($exists) {
                $imageItem = Get-Item -LiteralPath $imagePath
                $imageSize = [int64]$imageItem.Length
                $imageSha = Get-Sha256 -Path $imagePath
            }

            $candidates.Add([ordered]@{
                imageId = $imageId
                paragraphIndex = $p
                lineIndex = $lineIndex
                textLength = $text.Length
                projectedWidth = $projectedWidth
                bboxWidth = [Math]::Round($width, 3)
                bboxHeight = [Math]::Round($height, 3)
                vertices = @($vertices | ForEach-Object { @([double]$_[0], [double]$_[1]) })
                imagePath = $imagePath
                imageExists = $exists
                imageSizeBytes = $imageSize
                imageSha256 = $imageSha
                metadataExists = ($null -ne $metadata)
                metadataLicense = if ($metadata) { [string]$metadata.License } else { $null }
                metadataOriginalLandingUrl = if ($metadata) { [string]$metadata.OriginalLandingURL } else { $null }
            })
        }
    }
}

$relativeJsonPath = $OutputJsonPath.Replace('\', '/')
$relativeMarkdownPath = $OutputMarkdownPath.Replace('\', '/')
$candidateImageIds = @($candidates | ForEach-Object { [string]$_['imageId'] } | Sort-Object -Unique)
$availableImageIds = @($candidates | Where-Object { $_['imageExists'] } | ForEach-Object { [string]$_['imageId'] } | Sort-Object -Unique)
$missingImageIds = @($candidates | Where-Object { -not $_['imageExists'] } | ForEach-Object { [string]$_['imageId'] } | Sort-Object -Unique)
$metadataImageIds = @($candidates | Where-Object { $_['metadataExists'] } | ForEach-Object { [string]$_['imageId'] } | Sort-Object -Unique)
$maximumProjectedWidth = if ($candidates.Count -gt 0) { [int](($candidates | ForEach-Object { [int]$_['projectedWidth'] } | Measure-Object -Maximum).Maximum) } else { 0 }
$maximumTextLength = if ($candidates.Count -gt 0) { [int](($candidates | ForEach-Object { [int]$_['textLength'] } | Measure-Object -Maximum).Maximum) } else { 0 }
$result = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-hiertext-natural-wide-a2-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    source = [ordered]@{
        annotationPath = $annotationRelativePath.Replace('\', '/')
        annotationSha256 = Get-Sha256 -Path $annotationsFullPath
        sourceRevision = $annotationRevision
        annotationVersion = $annotationVersion
        annotationInfo = $annotationDocument.info
        annotationCount = $annotations.Count
        metadataPath = $metadataFullPath
        metadataSha256 = if ($metadataFullPath) { Get-Sha256 -Path $metadataFullPath } else { $null }
        metadataRowCount = $metadataById.Count
        imageRoot = $ImageRoot
        imageSourceTemplate = 'https://open-images-dataset.s3.amazonaws.com/validation/{image_id}.jpg'
    }
    protocol = [ordered]@{
        targetHeight = $TargetHeight
        minimumProjectedWidth = $MinimumProjectedWidth
        projection = 'bbox_width * targetHeight / bbox_height; this is a geometry/recognition-width audit, not a character-count claim.'
    }
    summary = [ordered]@{
        candidateLineCount = $candidates.Count
        candidateImageCount = $candidateImageIds.Count
        availableImageCount = $availableImageIds.Count
        missingImageCount = $missingImageIds.Count
        metadataImageCount = $metadataImageIds.Count
        maximumProjectedWidth = $maximumProjectedWidth
        maximumTextLength = $maximumTextLength
    }
    candidates = @($candidates | Sort-Object projectedWidth -Descending)
    boundary = 'These are natural annotated lines whose projected recognition geometry exceeds the threshold; they are not natural 3,200-character lines. OCR, SlidingWindow seam mapping, CER/WER, memory budget and backend parity remain unmeasured until the source images are available locally.'
}

$jsonFullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputJsonPath))
$markdownFullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputMarkdownPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonFullPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFullPath) | Out-Null
$result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $jsonFullPath -Encoding utf8

$markdown = [Text.StringBuilder]::new()
$annotationSha = [string]$result['source']['annotationSha256']
$candidateLineCount = [int]$result['summary']['candidateLineCount']
$candidateImageCount = [int]$result['summary']['candidateImageCount']
$availableImageCount = [int]$result['summary']['availableImageCount']
$missingImageCount = [int]$result['summary']['missingImageCount']
$metadataImageCount = [int]$result['summary']['metadataImageCount']
$sourceImageTemplate = [string]$result['source']['imageSourceTemplate']
[void]$markdown.AppendLine('# HierText natural wide-line coverage audit for A2 (2026-10-17)')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("This report scans the pinned HierText annotation source using a target recognition height of **$TargetHeight** and keeps every natural line whose projected recognition width is at least **$MinimumProjectedWidth**. The projection is a geometry signal, not a claim that the line contains that many characters.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- Annotation source: $annotationRelativePath (SHA-256 $annotationSha, source revision $annotationRevision, annotation version $annotationVersion).")
[void]$markdown.AppendLine("- Candidate lines: **$candidateLineCount** across **$candidateImageCount** images; local images present: **$availableImageCount**; missing: **$missingImageCount**.")
[void]$markdown.AppendLine("- Candidate image IDs found in the supplied Open Images metadata CSV: **$metadataImageCount**. A missing metadata row is a source/catalog boundary and must be resolved before treating the remote template as downloadable evidence.")
[void]$markdown.AppendLine("- Maximum projected width: **$maximumProjectedWidth**; maximum annotated text length among candidates: **$maximumTextLength** characters.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('| Image ID | Paragraph/line | Text length | Projected width | Local image | SHA-256 |')
[void]$markdown.AppendLine('| --- | ---: | ---: | ---: | --- | --- |')
foreach ($candidate in $result.candidates) {
    $candidateId = [string]$candidate['imageId']
    $candidateParagraph = [int]$candidate['paragraphIndex']
    $candidateLine = [int]$candidate['lineIndex']
    $candidateTextLength = [int]$candidate['textLength']
    $candidateProjectedWidth = [int]$candidate['projectedWidth']
    $shortSha = if ($candidate['imageSha256']) { ([string]$candidate['imageSha256']).Substring(0, 16) } else { '—' }
    $presence = if ($candidate['imageExists']) { 'yes' } else { 'missing' }
    [void]$markdown.AppendLine("| $candidateId | $candidateParagraph/$candidateLine | $candidateTextLength | $candidateProjectedWidth | $presence | $shortSha |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('## Interpretation and next step')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('The candidates are useful for testing geometry-driven SlidingWindow behavior, overlap seam attribution, source-region mapping and memory budgets. They do **not** close the A2 requirement for a naturally occurring continuous line of at least 3,200 characters. This run also does not claim OCR quality: no detector/recognizer invocation, CER/WER, seam merge result or backend parity was measured.')
if ($missingImageCount -gt 0) {
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("The local image cache is incomplete. Obtain the missing Open Images JPEGs from the official template $sourceImageTemplate, verify their per-image license/attribution from the pinned metadata, then rerun this script before running OCR. If an ID is absent from the metadata CSV or the official URL returns 404, record that source boundary instead of substituting an unrelated image.")
}

Set-Content -LiteralPath $markdownFullPath -Value $markdown.ToString().TrimEnd() -Encoding utf8
Write-Host "HIERText_WIDE_JSON=$jsonFullPath"
Write-Host "HIERText_WIDE_MARKDOWN=$markdownFullPath"
Write-Host "HIERText_WIDE_CANDIDATE_LINES=$($result['summary']['candidateLineCount'])"
Write-Host "HIERText_WIDE_AVAILABLE_IMAGES=$($result['summary']['availableImageCount'])"
Write-Host 'HIERText_WIDE_STATUS=pass'
