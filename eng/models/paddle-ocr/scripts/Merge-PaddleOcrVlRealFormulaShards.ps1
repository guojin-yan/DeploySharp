[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string[]]$ReportPaths,
    [Parameter(Mandatory)][string]$OutputJsonPath,
    [ValidateRange(1, 121)][int]$ExpectedCount = 121
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Same($Expected, $Actual, [string]$Field) {
    if (($Expected | ConvertTo-Json -Depth 40 -Compress) -cne ($Actual | ConvertTo-Json -Depth 40 -Compress)) {
        throw "Inconsistent PaddleOCR-VL shard: $Field"
    }
}

$manifest = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifestRows = @(Get-Content -LiteralPath $manifest | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_ | ConvertFrom-Json -Depth 20 })
if ($manifestRows.Count -ne 121) { throw 'The pinned realFormula manifest must contain exactly 121 rows.' }
$manifestSha256 = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()

$output = [IO.Path]::GetFullPath($OutputJsonPath)
$paths = @($ReportPaths | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if ($paths.Count -ne @($paths | Sort-Object -Unique).Count) { throw 'Shard paths must be distinct.' }
if ($output -in $paths) { throw 'Output cannot overwrite a shard report.' }
$shards = @($paths | ForEach-Object {
    [pscustomobject]@{
        path = $_
        sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
        report = Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json -Depth 40
    }
} | Sort-Object { [int]$_.report.dataset.offset })
if ($shards.Count -eq 0) { throw 'At least one completed shard is required.' }

$first = $shards[0].report
$rows = [System.Collections.Generic.List[object]]::new()
$sources = [System.Collections.Generic.List[object]]::new()
foreach ($shard in $shards) {
    $report = $shard.report
    Assert-Same 'deploysharp-paddleocr-vl-realformula-quality-v1' $report.schemaVersion 'schema'
    Assert-Same 'completed' $report.status 'status'
    if ($null -ne $report.failure -or $null -ne $report.currentImage) {
        throw "Completed shard has failure or currentImage: $($shard.path)"
    }
    if ($null -eq $report.dataset.PSObject.Properties['offset']) {
        throw "Shard is missing dataset.offset: $($shard.path)"
    }
    foreach ($field in @('name', 'version', 'doi', 'license', 'csvSha256', 'manifestSha256', 'annotatedCount')) {
        Assert-Same $first.dataset.$field $report.dataset.$field "dataset/$field"
    }
    Assert-Same $manifestSha256 $report.dataset.manifestSha256 'manifest SHA-256'
    Assert-Same 121 $report.dataset.annotatedCount 'annotated count'
    Assert-Same @() @($report.dataset.selectedImages) 'image-name filter'
    Assert-Same $first.model $report.model 'model'
    Assert-Same $first.environment $report.environment 'environment/build'
    Assert-Same $first.protocol $report.protocol 'protocol'

    $offset = [int]$report.dataset.offset
    $limit = [int]$report.dataset.limit
    $shardRows = @($report.results)
    if ($offset -ne $rows.Count) { throw "Shard gap or overlap at offset $offset; expected $($rows.Count)." }
    if ($limit -lt 1 -or $limit -gt 5 -or $shardRows.Count -lt 1 -or $shardRows.Count -gt $limit) {
        throw "Invalid bounded shard size at offset $offset."
    }
    Assert-Same $shardRows.Count $report.dataset.selectedCount "selected count at $offset"
    Assert-Same $shardRows.Count $report.summary.completedCount "completed count at $offset"
    if ($offset + $shardRows.Count -gt $manifestRows.Count) { throw 'Shard extends beyond the manifest.' }
    for ($index = 0; $index -lt $shardRows.Count; $index++) {
        $expected = $manifestRows[$offset + $index]
        $actual = $shardRows[$index]
        foreach ($pair in @(@('Image', 'image'), @('ImageSha256', 'imageSha256'), @('ReferenceLatex', 'referenceLatex'))) {
            Assert-Same $expected.($pair[0]) $actual.($pair[1]) "manifest row $($offset + $index)/$($pair[1])"
        }
        if ($null -ne $actual.PSObject.Properties['generatedTokenIds']) {
            Assert-Same $actual.generatedTokens @($actual.generatedTokenIds).Count "token count at $($offset + $index)"
        }
        $rows.Add($actual)
    }
    $sources.Add([ordered]@{
        file = [IO.Path]::GetFileName($shard.path)
        sha256 = $shard.sha256
        offset = $offset
        count = $shardRows.Count
    })
}
if ($rows.Count -ne $ExpectedCount) {
    throw "Merged $($rows.Count) manifest rows; expected $ExpectedCount. No output was written."
}

$resultRows = $rows.ToArray()
$edits = ($resultRows | Measure-Object -Property characterEdits -Sum).Sum
$referenceCharacters = ($resultRows | Measure-Object -Property referenceLength -Sum).Sum
$exactMatches = @($resultRows | Where-Object exactMatch).Count
$summary = [ordered]@{
    completedCount = $resultRows.Count
    exactMatches = $exactMatches
    exactMatchRate = [double]$exactMatches / $resultRows.Count
    corpusCharacterErrorRate = [double]$edits / [Math]::Max(1, $referenceCharacters)
    meanCharacterErrorRate = ($resultRows | Measure-Object -Property characterErrorRate -Average).Average
    endOfSequenceCount = @($resultRows | Where-Object { $_.finishReason -ceq 'EndOfSequence' }).Count
    maxTokensCount = @($resultRows | Where-Object { $_.finishReason -ceq 'MaxTokens' }).Count
    emptyPredictionCount = @($resultRows | Where-Object { $_.predictionLength -eq 0 }).Count
}

$merged = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-realformula-quality-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    status = $(if ($resultRows.Count -eq 121) { 'completed' } else { 'partial' })
    failure = $null
    currentImage = $null
    dataset = [ordered]@{
        name = $first.dataset.name
        version = $first.dataset.version
        doi = $first.dataset.doi
        license = $first.dataset.license
        csvSha256 = $first.dataset.csvSha256
        manifestSha256 = $manifestSha256
        annotatedCount = 121
        selectedCount = $resultRows.Count
        selectedImages = @()
        offset = 0
        limit = $(if ($resultRows.Count -eq 121) { 0 } else { $resultRows.Count })
    }
    model = $first.model
    environment = $first.environment
    protocol = $first.protocol
    summary = $summary
    results = $resultRows
    aggregation = [ordered]@{
        mode = 'separate-process-contiguous-manifest-shards'
        shardCount = $sources.Count
        sources = $sources.ToArray()
    }
    boundary = 'Every shard uses a new process and model Sessions. This aggregate is labelled-corpus decision evidence, not one-Session stability, controlled latency or mathematical semantic accuracy.'
}

$null = New-Item -ItemType Directory -Force -Path (Split-Path $output)
$merged | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output "PADDLEOCR_VL_MERGED_STATUS=$($merged.status)"
Write-Output "PADDLEOCR_VL_MERGED_COUNT=$($resultRows.Count)"
Write-Output "PADDLEOCR_VL_MERGED_JSON=$output"
