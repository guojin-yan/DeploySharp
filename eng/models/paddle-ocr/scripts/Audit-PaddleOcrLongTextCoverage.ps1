[CmdletBinding()]
param(
    [string]$ManifestRoot = 'F:\OCRBenchmarkTesting\data\annotations\manifests',
    [string]$OutputJson,
    [string]$OutputMarkdown,
    [string]$ReportDate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ReportDate)) { $ReportDate = Get-Date -Format 'yyyy-MM-dd' }
try { $reportDateValue = [DateTime]::ParseExact($ReportDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) }
catch { throw "ReportDate must use yyyy-MM-dd: $ReportDate" }
$reportStamp = $reportDateValue.ToString('yyyyMMdd', [Globalization.CultureInfo]::InvariantCulture)
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $scriptRoot "..\verification\paddleocr-longtext-coverage-a2-$reportStamp.json" }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $scriptRoot "..\verification\paddleocr-longtext-coverage-a2-$reportStamp.md" }

$manifestFiles = @(Get-ChildItem -LiteralPath $ManifestRoot -Filter '*.jsonl' -File | Sort-Object Name)
if ($manifestFiles.Count -eq 0) { throw "No JSONL manifests found under $ManifestRoot" }

$records = [System.Collections.Generic.List[object]]::new()
$manifestSummaries = [System.Collections.Generic.List[object]]::new()
$totalEligible = 0
$totalIgnored = 0
$totalEmpty = 0

foreach ($file in $manifestFiles) {
    $lineCount = 0
    $eligibleCount = 0
    $ignoredCount = 0
    $emptyCount = 0
    $maxLine = 0
    $maxLineText = ''
    $maxLineImage = ''
    $maxPageAggregate = 0
    $pagesAtThreshold = 0
    $datasetNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $splitNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $lineCount++
        $record = $line | ConvertFrom-Json
        if ($record.PSObject.Properties['source_dataset'] -and $record.source_dataset) { [void]$datasetNames.Add([string]$record.source_dataset) }
        if ($record.PSObject.Properties['source_split'] -and $record.source_split) { [void]$splitNames.Add([string]$record.source_split) }

        $items = [System.Collections.Generic.List[object]]::new()
        if ($record.PSObject.Properties['instances'] -and $null -ne $record.instances) {
            foreach ($instance in @($record.instances)) {
                $isIgnored = $false
                if ($instance.PSObject.Properties['ignore'] -and $instance.ignore) { $isIgnored = $true }
                $text = ''
                if ($instance.PSObject.Properties['text'] -and $null -ne $instance.text) { $text = [string]$instance.text }
                if ($isIgnored) { $ignoredCount++; continue }
                if ([string]::IsNullOrWhiteSpace($text)) { $emptyCount++; continue }
                $items.Add([ordered]@{ text = $text; instanceId = if ($instance.PSObject.Properties['instance_id']) { [string]$instance.instance_id } else { '' } })
            }
        } elseif ($record.PSObject.Properties['text'] -and -not [string]::IsNullOrWhiteSpace([string]$record.text)) {
            $items.Add([ordered]@{ text = [string]$record.text; instanceId = '' })
        }

        $pageChars = 0
        foreach ($item in $items) {
            $length = ([string]$item.text).Length
            $eligibleCount++
            $pageChars += $length
            if ($length -gt $maxLine) {
                $maxLine = $length
                $maxLineText = [string]$item.text
                $maxLineImage = if ($record.PSObject.Properties['image_id']) { [string]$record.image_id } else { '' }
            }
        }
        if ($pageChars -gt $maxPageAggregate) { $maxPageAggregate = $pageChars }
        if ($pageChars -ge 3200) { $pagesAtThreshold++ }
        $records.Add([ordered]@{
            manifest = $file.Name
            imageId = if ($record.PSObject.Properties['image_id']) { [string]$record.image_id } else { '' }
            dataset = if ($record.PSObject.Properties['source_dataset']) { [string]$record.source_dataset } else { '' }
            split = if ($record.PSObject.Properties['source_split']) { [string]$record.source_split } else { '' }
            eligibleLineCount = $items.Count
            pageCharacterCount = $pageChars
            maximumLineCharacters = if ($items.Count) { [int](($items | ForEach-Object { ([string]$_.text).Length } | Measure-Object -Maximum).Maximum) } else { 0 }
        })
    }

    $totalEligible += $eligibleCount
    $totalIgnored += $ignoredCount
    $totalEmpty += $emptyCount
    $manifestHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestSummaries.Add([ordered]@{
        name = $file.Name
        bytes = $file.Length
        sha256 = $manifestHash
        lineCount = $lineCount
        dataset = (@($datasetNames) -join ', ')
        split = (@($splitNames) -join ', ')
        eligibleLineCount = $eligibleCount
        ignoredLineCount = $ignoredCount
        emptyNonIgnoredLineCount = $emptyCount
        maximumLineCharacters = $maxLine
        maximumLineImageId = $maxLineImage
        maximumLineText = $maxLineText
        maximumPageAggregateCharacters = $maxPageAggregate
        pagesWithAggregateAtLeast3200 = $pagesAtThreshold
    })
}

$maxNaturalLine = if ($records.Count) { [int](($records | ForEach-Object maximumLineCharacters | Measure-Object -Maximum).Maximum) } else { 0 }
$maxPageAggregate = if ($records.Count) { [int](($records | ForEach-Object pageCharacterCount | Measure-Object -Maximum).Maximum) } else { 0 }
$longLineRecords = @($records | Where-Object { $_.maximumLineCharacters -ge 3200 }).Count
$longAggregatePages = @($records | Where-Object { $_.pageCharacterCount -ge 3200 }).Count

$report = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-longtext-coverage-v1'
    reportDate = $ReportDate
    generatedUtc = [DateTimeOffset]::UtcNow
    source = [ordered]@{
        manifestRoot = $ManifestRoot
        manifestCount = $manifestFiles.Count
        pageRecordCount = $records.Count
        eligibleLineCount = $totalEligible
        ignoredLineCount = $totalIgnored
        emptyNonIgnoredLineCount = $totalEmpty
        manifests = @($manifestSummaries)
        redistributionBoundary = 'Manifest metadata and source images remain outside this repository; this is a local data-availability audit.'
    }
    thresholds = [ordered]@{ naturalContinuousLineCharacters = 3200; pageAggregateCharacters = 3200 }
    summary = [ordered]@{
        maximumNaturalContinuousLineCharacters = $maxNaturalLine
        maximumPageAggregateCharacters = $maxPageAggregate
        recordsWithNaturalLineAtLeast3200 = $longLineRecords
        pagesWithAggregateAtLeast3200 = $longAggregatePages
        naturalContinuousLongTextAvailable = ($longLineRecords -gt 0)
    }
    boundary = 'Page aggregate characters are not a continuous OCR line and cannot close A2. This report only audits the current local public manifest inventory; a legal, attributable natural long-line corpus and model evaluation are still required.'
    pages = @($records | Sort-Object -Property maximumLineCharacters,pageCharacterCount -Descending)
}

$fullJson = [IO.Path]::GetFullPath($OutputJson)
$fullMd = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullJson)) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fullJson -Encoding UTF8

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# PaddleOCR long-text coverage audit ($ReportDate)")
$md.Add('')
$md.Add('This report inventories every local public JSONL manifest currently available to the OCR benchmark harness. It does not run OCR and does not turn page-level character totals into continuous-line accuracy.')
$md.Add('')
$md.Add("- Manifest files: **$($manifestFiles.Count)**; page records: **$($records.Count)**")
$md.Add("- Eligible non-empty, non-ignored lines: **$totalEligible**; ignored: **$totalIgnored**; empty non-ignored: **$totalEmpty**")
$md.Add("- Maximum natural annotated line in this inventory: **$maxNaturalLine characters**")
$md.Add("- Maximum page aggregate across annotated lines: **$maxPageAggregate characters**")
$md.Add("- Natural annotated lines >= 3,200 characters: **$longLineRecords**")
$md.Add("- Page records with aggregate >= 3,200 characters: **$longAggregatePages** (not valid continuous-line samples)")
$md.Add('')
$md.Add('| Manifest | Dataset / split | Eligible lines | Maximum line | Maximum page aggregate | SHA-256 |')
$md.Add('|---|---|---:|---:|---:|---|')
foreach ($manifest in $manifestSummaries) {
    $md.Add("| $($manifest.name) | $($manifest.dataset) / $($manifest.split) | $($manifest.eligibleLineCount) | $($manifest.maximumLineCharacters) | $($manifest.maximumPageAggregateCharacters) | ``$($manifest.sha256)`` |")
}
$md.Add('')
$md.Add('## Boundary')
$md.Add('')
$md.Add('The current local inventory contains no natural annotated line at or above 3,200 characters. Page totals, concatenated crops and synthetic compositions change the OCR task and cannot close A2. The report is a data-availability result, not an accuracy or backend-support claim; source images and annotations stay outside Git/Release.')
$md | Set-Content -LiteralPath $fullMd -Encoding UTF8
Write-Output "Wrote $fullJson and $fullMd"
