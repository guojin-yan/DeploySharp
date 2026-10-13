param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputJson,
    [Parameter(Mandatory = $true)]
    [string]$OutputMarkdown,
    [string]$SupersetBaselinePath
)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextSha256([string]$Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-Value($Object, [string[]]$Names) {
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -ne $property -and $null -ne $property.Value) { return [string]$property.Value }
    }
    return $null
}

function Get-Percentile([double[]]$Values, [double]$Probability) {
    if ($Values.Count -eq 0) { return $null }
    [Array]::Sort($Values)
    $position = ($Values.Count - 1) * $Probability
    $lower = [Math]::Floor($position)
    $upper = [Math]::Ceiling($position)
    if ($lower -eq $upper) { return $Values[$lower] }
    return $Values[$lower] + (($Values[$upper] - $Values[$lower]) * ($position - $lower))
}

function Get-SelectionGroup([string]$Name) {
    if ($Name.StartsWith('OECD', [StringComparison]::OrdinalIgnoreCase)) { return 'OECD' }
    if ($Name.StartsWith('two_col_', [StringComparison]::OrdinalIgnoreCase)) { return 'two_col' }
    return 'other'
}

function Format-Number($Value, [int]$Digits = 4) {
    if ($null -eq $Value) { return '-' }
    return ([double]$Value).ToString("F$Digits", [Globalization.CultureInfo]::InvariantCulture)
}

$inputFullPath = [IO.Path]::GetFullPath($InputPath)
$manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
$report = Get-Content -LiteralPath $inputFullPath -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$rows = @($report.results)
$manifestRows = @($manifest.samples)

if ($rows.Count -eq 0) { throw 'The input report contains no results.' }
if ([int]$report.sampleCount -ne $rows.Count) { throw 'The report sampleCount does not match its result rows.' }
if ([int]$manifest.sampleCount -ne $manifestRows.Count) { throw 'The manifest sampleCount does not match its sample rows.' }
if ($rows.Count -ne $manifestRows.Count) { throw 'The report and manifest sample counts differ.' }
if ([string]$report.sourceRevision -ne [string]$manifest.sourceRevision) { throw 'The report and manifest revisions differ.' }
if ([string]$report.split -ne [string]$manifest.split) { throw 'The report and manifest splits differ.' }

$manifestByName = @{}
foreach ($sample in $manifestRows) {
    if ($manifestByName.ContainsKey([string]$sample.file)) { throw "Duplicate manifest sample: $($sample.file)" }
    $manifestByName[[string]$sample.file] = $sample
}

$rowByName = @{}
foreach ($row in $rows) {
    $name = [string]$row.sample
    if ($rowByName.ContainsKey($name)) { throw "Duplicate report sample: $name" }
    if (!$manifestByName.ContainsKey($name)) { throw "Report sample is absent from the manifest: $name" }
    $sample = $manifestByName[$name]
    if ([string]$row.imageSha256 -ne [string]$sample.imageSha256) { throw "Image SHA mismatch for $name" }
    if ([string]$row.sourceTableSha256 -ne [string]$sample.tableSha256) { throw "Source-table SHA mismatch for $name" }
    $rowByName[$name] = $row
}

$baseline = $null
if (![string]::IsNullOrWhiteSpace($SupersetBaselinePath)) {
    $baselineFullPath = [IO.Path]::GetFullPath($SupersetBaselinePath)
    $baselineReport = Get-Content -LiteralPath $baselineFullPath -Raw | ConvertFrom-Json
    $baselineRows = @($baselineReport.results)
    $mismatches = [Collections.Generic.List[string]]::new()
    foreach ($old in $baselineRows) {
        $name = [string]$old.sample
        if (!$rowByName.ContainsKey($name)) { throw "Baseline sample is absent from the expanded report: $name" }
        $current = $rowByName[$name]
        $oldGenerated = Get-Value $old @('generatedTableSha256', 'generatedTextSha256')
        $currentGenerated = Get-Value $current @('generatedTableSha256', 'generatedTextSha256')
        if ([string]$old.imageSha256 -ne [string]$current.imageSha256 -or
            [string]$old.sourceTableSha256 -ne [string]$current.sourceTableSha256 -or
            $oldGenerated -ne $currentGenerated -or
            [string]$old.finishReason -ne [string]$current.finishReason) {
            $mismatches.Add($name)
        }
    }
    $baseline = [ordered]@{
        source = [IO.Path]::GetFileName($baselineFullPath)
        sourceSha256 = Get-Sha256 $baselineFullPath
        sampleCount = $baselineRows.Count
        retainedSampleCount = $baselineRows.Count
        artifactOrOutputMismatchCount = $mismatches.Count
        artifactOrOutputMismatches = @($mismatches)
    }
}

$expectedRows = [int](($rows | ForEach-Object { $_.structure.expectedRows } | Measure-Object -Sum).Sum)
$expectedCells = [int](($rows | ForEach-Object { $_.structure.expectedCells } | Measure-Object -Sum).Sum)
$rowExact = [int](($rows | ForEach-Object { $_.structure.rowExactMatchCount } | Measure-Object -Sum).Sum)
$cellExact = [int](($rows | ForEach-Object { $_.structure.cellExactMatchCount } | Measure-Object -Sum).Sum)
$invalid = @($rows | Where-Object { !$_.structure.actualStructurallyValid } | ForEach-Object {
    [ordered]@{
        sample = $_.sample
        finishReason = $_.finishReason
        expectedRows = $_.structure.expectedRows
        expectedColumns = $_.structure.expectedColumns
        actualRows = $_.structure.actualRows
        actualColumns = $_.structure.actualColumns
        actualCells = $_.structure.actualCells
    }
})
$groups = @($rows | Group-Object { Get-SelectionGroup ([string]$_.sample) } | Sort-Object Name | ForEach-Object {
    [ordered]@{ name = $_.Name; sampleCount = $_.Count }
})
$selectionIdentity = [ordered]@{
    dataset = $manifest.dataset
    split = $manifest.split
    sourceRepository = $manifest.sourceRepository
    sourceRevision = $manifest.sourceRevision
    samples = @($manifestRows | ForEach-Object {
        [ordered]@{
            file = $_.file
            tableFile = $_.tableFile
            expectedText = $_.expectedText
            imageSha256 = $_.imageSha256
            tableSha256 = $_.tableSha256
            imageUri = $_.imageUri
            tableUri = $_.tableUri
        }
    })
}
$selectionIdentityJson = $selectionIdentity | ConvertTo-Json -Depth 8 -Compress

$summary = [ordered]@{
    schemaVersion = 'deploysharp-chart2table-quality-summary-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    backend = $report.backend
    modelId = $report.modelId
    sourceReport = [IO.Path]::GetFileName($inputFullPath)
    sourceReportSha256 = Get-Sha256 $inputFullPath
    manifest = [ordered]@{
        dataset = $manifest.dataset
        split = $manifest.split
        sourceRepository = $manifest.sourceRepository
        sourceRevision = $manifest.sourceRevision
        sampleCount = $manifestRows.Count
        fileSha256 = Get-Sha256 $manifestFullPath
        selectionIdentitySha256 = Get-TextSha256 $selectionIdentityJson
        selectionGroups = $groups
    }
    maximumNewTokens = $report.maximumNewTokens
    metrics = [ordered]@{
        sampleCount = $rows.Count
        endOfSequenceCount = @($rows | Where-Object finishReason -eq 'EndOfSequence').Count
        structurallyValidCount = @($rows | Where-Object { $_.structure.actualStructurallyValid }).Count
        structureMatchCount = @($rows | Where-Object { $_.structure.structureMatches }).Count
        exactTextMatchCount = @($rows | Where-Object { $_.structure.exactTextMatch }).Count
        rowExactMatchCount = $rowExact
        expectedRowCount = $expectedRows
        rowMicroAccuracy = if ($expectedRows -eq 0) { $null } else { $rowExact / $expectedRows }
        cellExactMatchCount = $cellExact
        expectedCellCount = $expectedCells
        cellMicroAccuracy = if ($expectedCells -eq 0) { $null } else { $cellExact / $expectedCells }
        totalMillisecondsP50 = Get-Percentile @($rows | ForEach-Object { [double]$_.totalMs }) .50
        totalMillisecondsP95 = Get-Percentile @($rows | ForEach-Object { [double]$_.totalMs }) .95
        visionMillisecondsP50 = Get-Percentile @($rows | ForEach-Object { [double]$_.visionMs }) .50
        visionMillisecondsP95 = Get-Percentile @($rows | ForEach-Object { [double]$_.visionMs }) .95
        tokenCountP50 = Get-Percentile @($rows | ForEach-Object { [double]$_.tokenCountIncludingEos }) .50
        tokenCountP95 = Get-Percentile @($rows | ForEach-Object { [double]$_.tokenCountIncludingEos }) .95
    }
    invalidStructureSamples = $invalid
    supersetBaseline = $baseline
    boundary = 'Bounded official ChartQA table-reconstruction evidence. These metrics are not split-level ChartQA accuracy, question answering accuracy, a controlled backend performance comparison, or a cross-device guarantee.'
}

$jsonDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputJson))
$markdownDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputMarkdown))
New-Item -ItemType Directory -Force -Path $jsonDirectory, $markdownDirectory | Out-Null
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputJson -Encoding utf8

$metrics = $summary.metrics
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("# Chart2Table $($rows.Count)-image $($summary.backend) quality expansion (2026-10-13)")
$lines.Add('')
$lines.Add(('This report summarizes a deterministic {0}-image selection from ChartQA `{1}` at revision `{2}`. It validates the external manifest and every image/table hash before aggregating the checked-in inference report.' -f $rows.Count, $manifest.split, $manifest.sourceRevision))
$lines.Add('')
$lines.Add('| Metric | Result |')
$lines.Add('|---|---:|')
$lines.Add("| Samples / EOS | $($metrics.sampleCount) / $($metrics.endOfSequenceCount) |")
$lines.Add("| Structurally valid | $($metrics.structurallyValidCount)/$($metrics.sampleCount) |")
$lines.Add("| Matching dimensions | $($metrics.structureMatchCount)/$($metrics.sampleCount) |")
$lines.Add("| Exact table text | $($metrics.exactTextMatchCount)/$($metrics.sampleCount) |")
$lines.Add("| Row exact (micro) | $($metrics.rowExactMatchCount)/$($metrics.expectedRowCount) ($(Format-Number (100 * $metrics.rowMicroAccuracy) 2)%) |")
$lines.Add("| Cell exact (micro) | $($metrics.cellExactMatchCount)/$($metrics.expectedCellCount) ($(Format-Number (100 * $metrics.cellMicroAccuracy) 2)%) |")
$lines.Add("| Total latency P50 / P95 | $(Format-Number $metrics.totalMillisecondsP50 3) / $(Format-Number $metrics.totalMillisecondsP95 3) ms |")
$lines.Add("| Vision latency P50 / P95 | $(Format-Number $metrics.visionMillisecondsP50 3) / $(Format-Number $metrics.visionMillisecondsP95 3) ms |")
$lines.Add("| Generated tokens P50 / P95 | $(Format-Number $metrics.tokenCountP50 2) / $(Format-Number $metrics.tokenCountP95 2) |")
$lines.Add('')
$lines.Add('## Provenance and consistency')
$lines.Add('')
$lines.Add("- Source report: ``$($summary.sourceReport)``; SHA-256 ``$($summary.sourceReportSha256)``.")
$lines.Add("- Selection identity SHA-256: ``$($summary.manifest.selectionIdentitySha256)``; manifest file SHA-256: ``$($summary.manifest.fileSha256)``.")
$lines.Add("- Selection groups: " + (($groups | ForEach-Object { "$($_.name)=$($_.sampleCount)" }) -join ', ') + '. The identity hash excludes the non-deterministic manifest generation timestamp.')
if ($null -ne $baseline) {
    $lines.Add("- Superset check: all $($baseline.sampleCount) rows from ``$($baseline.source)`` are retained; artifact/output mismatches: $($baseline.artifactOrOutputMismatchCount).")
}
if ($invalid.Count -gt 0) {
    $lines.Add("- Structurally invalid outputs: " + (($invalid | ForEach-Object { "``$($_.sample)`` ($($_.expectedRows)x$($_.expectedColumns) expected, $($_.actualRows)x$($_.actualColumns) generated)" }) -join '; ') + '.')
}
$lines.Add('')
$lines.Add('## Boundary')
$lines.Add('')
$lines.Add('- This is a bounded table-reconstruction selection, not ChartQA split accuracy or question-answering accuracy.')
$lines.Add('- Latency is an observation from the quality run. It is not a warm-up-controlled benchmark or a backend ranking.')
$lines.Add('- Exact/row/cell metrics are literal table-text diagnostics and do not provide semantic equivalence or chart-understanding guarantees.')
$lines.Add('- The external images, CSV files and manifest remain outside Git; the repository retains their hashes and the generated evidence only.')
$lines | Set-Content -LiteralPath $OutputMarkdown -Encoding utf8

Write-Output "Summary JSON: $([IO.Path]::GetFullPath($OutputJson))"
Write-Output "Summary Markdown: $([IO.Path]::GetFullPath($OutputMarkdown))"
Write-Output "Samples=$($rows.Count); EOS=$($metrics.endOfSequenceCount); valid=$($metrics.structurallyValidCount); exact=$($metrics.exactTextMatchCount)"
