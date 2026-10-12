[CmdletBinding()]
param(
    [string]$ManifestRoot,
    [string]$OutputJson,
    [string]$OutputMarkdown,
    [string]$ReportDate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ManifestRoot)) { $ManifestRoot = 'F:\OCRBenchmarkTesting\data\annotations\manifests' }
if ([string]::IsNullOrWhiteSpace($ReportDate)) { $ReportDate = Get-Date -Format 'yyyy-MM-dd' }
try { $reportDateValue = [DateTime]::ParseExact($ReportDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) }
catch { throw "ReportDate must use yyyy-MM-dd: $ReportDate" }
$reportStamp = $reportDateValue.ToString('yyyyMMdd', [Globalization.CultureInfo]::InvariantCulture)
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $scriptRoot "..\verification\hiertext-longtext-coverage-a2-$reportStamp.json" }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $scriptRoot "..\verification\hiertext-longtext-coverage-a2-$reportStamp.md" }

$manifestFiles = @(Get-ChildItem -LiteralPath $ManifestRoot -Filter 'hiertext*.jsonl' -File | Sort-Object Name)
if ($manifestFiles.Count -eq 0) { throw "No HierText manifests found under $ManifestRoot" }
$pages = [System.Collections.Generic.List[object]]::new()
$manifestSummaries = [System.Collections.Generic.List[object]]::new()
$sourceDataset = $null
$sourceSplit = $null
$sourceRevision = $null
$sourceLicense = $null
$eligibleInstanceCount = 0
$ignoredInstanceCount = 0
$emptyInstanceCount = 0
foreach ($file in $manifestFiles) {
    $manifestLines = 0
    foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $manifestLines++
        $record = $line | ConvertFrom-Json
        if ($null -eq $sourceDataset -and $record.source_dataset) { $sourceDataset = [string]$record.source_dataset }
        if ($null -eq $sourceSplit -and $record.source_split) { $sourceSplit = [string]$record.source_split }
        if ($null -eq $sourceRevision -and $record.source_revision) { $sourceRevision = [string]$record.source_revision }
        if ($null -eq $sourceLicense -and $record.license) { $sourceLicense = [string]$record.license }
        $allInstances = @($record.instances)
        $ignoredInstanceCount += @($allInstances | Where-Object { [bool]$_.ignore }).Count
        $emptyInstanceCount += @($allInstances | Where-Object { -not [bool]$_.ignore -and [string]::IsNullOrWhiteSpace([string]$_.text) }).Count
        $instances = @($allInstances | Where-Object { -not [bool]$_.ignore -and -not [string]::IsNullOrWhiteSpace([string]$_.text) })
        $eligibleInstanceCount += $instances.Count
        $lengths = @($instances | ForEach-Object { ([string]$_.text).Length })
        $pageChars = if ($lengths.Count) { [int](($lengths | Measure-Object -Sum).Sum) } else { 0 }
        $maxLine = if ($lengths.Count) { [int](($lengths | Measure-Object -Maximum).Maximum) } else { 0 }
        $pages.Add([ordered]@{ manifest = $file.Name; imageId = [string]$record.image_id; instanceCount = $instances.Count; pageCharacterCount = $pageChars; maximumAnnotatedLineCharacters = $maxLine })
    }
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestSummaries.Add([ordered]@{ name = $file.Name; bytes = $file.Length; lineCount = $manifestLines; sha256 = $hash })
}
$maxLine = [int](($pages | ForEach-Object maximumAnnotatedLineCharacters | Measure-Object -Maximum).Maximum)
$maxPage = [int](($pages | ForEach-Object pageCharacterCount | Measure-Object -Maximum).Maximum)
$longLines = @($pages | Where-Object { $_.maximumAnnotatedLineCharacters -ge 3200 }).Count
$longPages = @($pages | Where-Object { $_.pageCharacterCount -ge 3200 }).Count
$report = [ordered]@{
    schemaVersion = 'deploysharp-hiertext-longtext-coverage-v1'
    reportDate = $ReportDate
    generatedUtc = [DateTimeOffset]::UtcNow
    source = [ordered]@{ root = $ManifestRoot; dataset = $sourceDataset; split = $sourceSplit; revision = $sourceRevision; license = $sourceLicense; manifests = @($manifestSummaries); pageCount = $pages.Count; eligibleInstanceCount = $eligibleInstanceCount; ignoredInstanceCount = $ignoredInstanceCount; emptyInstanceCount = $emptyInstanceCount; licenseBoundary = 'Manifest metadata and source images remain outside this repository; this is an inventory, not a redistributed dataset.' }
    thresholds = [ordered]@{ naturalContinuousLineCharacters = 3200; pageAggregateCharacters = 3200 }
    summary = [ordered]@{ maximumAnnotatedLineCharacters = $maxLine; maximumPageAggregateCharacters = $maxPage; pagesWithNaturalLineAtLeast3200 = $longLines; pagesWithAggregateAtLeast3200 = $longPages; naturalContinuousLongTextAvailable = ($longLines -gt 0) }
    pages = @($pages | Sort-Object -Property maximumAnnotatedLineCharacters,pageCharacterCount -Descending)
    boundary = 'Page aggregate character count is not a continuous OCR line and cannot be used as a long-text accuracy sample. The current fixed HierText selection has no natural annotated line at or above 3200 characters; A2 remains open until a legal, attributable continuous-text corpus is available.'
}
$fullJson = [IO.Path]::GetFullPath($OutputJson); $fullMd = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullJson)) | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fullJson -Encoding UTF8
$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# HierText long-text coverage audit ($ReportDate)"); $md.Add('')
$md.Add('This is a data-availability audit for the fixed local HierText manifests. It does not run OCR and does not turn page-level character totals into long-line accuracy.') ; $md.Add('')
$md.Add("- Source: $sourceDataset / $sourceSplit; revision: ``$sourceRevision``")
$md.Add("- Manifests: $($manifestFiles.Count); pages: $($pages.Count); eligible rows: $eligibleInstanceCount; ignored rows: $ignoredInstanceCount; empty non-ignored rows: $emptyInstanceCount")
$md.Add("- Maximum annotated continuous line: **$maxLine characters**")
$md.Add("- Maximum page aggregate across annotated lines: **$maxPage characters**")
$md.Add("- Natural annotated lines >= 3,200 characters: **$longLines**")
$md.Add("- Pages with aggregate >= 3,200 characters: **$longPages** (not valid continuous-line samples)")
$md.Add('')
$md.Add('| Manifest | Image | Instances | Page aggregate chars | Maximum line chars |'); $md.Add('|---|---|---:|---:|---:|')
foreach ($page in @($report.pages | Select-Object -First 20)) { $md.Add("| $($page.manifest) | ``$($page.imageId)`` | $($page.instanceCount) | $($page.pageCharacterCount) | $($page.maximumAnnotatedLineCharacters) |") }
$md.Add(''); $md.Add('## Manifest provenance'); $md.Add('')
$md.Add('| Manifest | Lines | Bytes | SHA-256 |'); $md.Add('|---|---:|---:|---|')
foreach ($manifest in $manifestSummaries) { $md.Add("| $($manifest.name) | $($manifest.lineCount) | $($manifest.bytes) | ``$($manifest.sha256)`` |") }
$md.Add(''); $md.Add('## Boundary'); $md.Add('')
$md.Add('The fixed selection has no natural annotated line at or above 3,200 characters. Combining separate lines or pages would change the task and cannot close the A2 continuous-text gate. A legal, attributable long-line corpus is still required; the source manifest and images remain outside Git/Release.')
$md | Set-Content -LiteralPath $fullMd -Encoding UTF8
Write-Output "Wrote $fullJson and $fullMd"
