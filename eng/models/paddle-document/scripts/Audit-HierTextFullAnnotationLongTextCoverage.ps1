[CmdletBinding()]
param(
    [string]$AnnotationGzip = 'F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation-70b6620b2b112597d8219e11eee9773a1403827c.jsonl.gz',
    [string]$OutputJson,
    [string]$OutputMarkdown,
    [string]$ReportDate = '2026-10-12'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $scriptRoot '..\verification\hiertext-full-annotation-longtext-a2-20261012.json' }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $scriptRoot '..\verification\hiertext-full-annotation-longtext-a2-20261012.md' }
if (-not (Test-Path -LiteralPath $AnnotationGzip -PathType Leaf)) { throw "Annotation gzip not found: $AnnotationGzip" }

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
$fileBytes = (Get-Item -LiteralPath $AnnotationGzip).Length
$gzip = [IO.File]::OpenRead($AnnotationGzip)
try {
    $stream = [IO.Compression.GzipStream]::new($gzip, [IO.Compression.CompressionMode]::Decompress)
    try {
        $reader = [IO.StreamReader]::new($stream)
        try { $document = $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}
finally { $gzip.Dispose() }

$imageCount = 0
$lineCount = 0
$characterCount = 0
$ignoredLineCount = 0
$longLines = [System.Collections.Generic.List[object]]::new()
$maximumLine = 0
$maximumImageAggregate = 0
$sourceRevision = '70b6620b2b112597d8219e11eee9773a1403827c'
foreach ($record in @($document.annotations)) {
    $imageCount++
    $imageAggregate = 0
    foreach ($paragraph in @($record.paragraphs)) {
        foreach ($line in @($paragraph.lines)) {
            $lineCount++
            $text = [string]$line.text
            $length = $text.Length
            $characterCount += $length
            $imageAggregate += $length
            if ([bool]$line.legible -eq $false) { $ignoredLineCount++ }
            if ($length -gt $maximumLine) { $maximumLine = $length }
            if ($length -ge 3200) {
                $longLines.Add([ordered]@{ imageId = [string]$record.image_id; characters = $length; textSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() })
            }
        }
    }
    if ($imageAggregate -gt $maximumImageAggregate) { $maximumImageAggregate = $imageAggregate }
}

$report = [ordered]@{
    schemaVersion = 'deploysharp-hiertext-full-annotation-longtext-coverage-v1'
    reportDate = $ReportDate
    generatedUtc = [DateTimeOffset]::UtcNow
    source = [ordered]@{
        dataset = 'google-research-datasets/hiertext'
        split = 'validation'
        revision = $sourceRevision
        annotationGzip = [ordered]@{ path = $AnnotationGzip; compressedBytes = $fileBytes; sha256 = (Get-Sha256 $AnnotationGzip) }
        imageCount = $imageCount
        annotationLicense = 'CC-BY-SA-4.0; source images retain per-image Open Images terms and are not redistributed by this report.'
    }
    thresholds = [ordered]@{ naturalContinuousLineCharacters = 3200; pageAggregateCharacters = 3200 }
    summary = [ordered]@{ annotationLines = $lineCount; totalAnnotatedCharacters = $characterCount; ignoredOrIllegibleLines = $ignoredLineCount; maximumAnnotatedLineCharacters = $maximumLine; maximumPageAggregateCharacters = $maximumImageAggregate; linesAtLeast3200 = $longLines.Count; naturalContinuousLongTextAvailable = ($longLines.Count -gt 0) }
    longLines = @($longLines)
    boundary = 'This audits the complete official validation annotation file, not downloaded images or OCR predictions. No natural annotation line reaches 3200 characters; page aggregates are not continuous-line samples. A2 quality and sliding-window accuracy therefore remain open.'
}
$fullJson = [IO.Path]::GetFullPath($OutputJson); $fullMd = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullJson)) | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fullJson -Encoding UTF8
$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# HierText full annotation long-text coverage audit ($ReportDate)"); $md.Add('')
$md.Add('This report audits the complete official validation annotation archive. It is a data-availability/provenance result only; it does not run OCR or redistribute images.') ; $md.Add('')
$md.Add("- Images: **$imageCount**; annotated lines: **$lineCount**; total annotated characters: **$characterCount**")
$md.Add("- Annotation gzip bytes: **$fileBytes**; SHA-256: ``$($report.source.annotationGzip.sha256)``")
$md.Add("- HierText revision: ``$sourceRevision``; annotation license: CC-BY-SA-4.0")
$md.Add("- Maximum natural line: **$maximumLine characters**")
$md.Add("- Maximum page aggregate: **$maximumImageAggregate characters** (not a continuous line)")
$md.Add("- Natural lines >= 3,200 characters: **$($longLines.Count)**")
$md.Add(''); $md.Add('## Boundary'); $md.Add('')
$md.Add('The complete validation annotation archive contains no natural continuous line at or above 3,200 characters. This closes the question of whether the current local annotation cache merely omitted a long line, but it does not close A2: a legal, attributable long-line corpus or a different threshold is still required for real accuracy and seam-quality acceptance.')
$md | Set-Content -LiteralPath $fullMd -Encoding UTF8
Write-Output "Wrote $fullJson and $fullMd"
