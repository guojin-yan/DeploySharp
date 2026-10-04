param(
    [Parameter(Mandatory = $true)]
    [string]$OrtReport,
    [Parameter(Mandatory = $true)]
    [string]$TensorRtReport,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [string]$MarkdownPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path

function Resolve-RepoPath([string]$PathValue) {
    $candidate = if ([IO.Path]::IsPathRooted($PathValue)) { $PathValue } else { Join-Path $repoRoot $PathValue }
    return [IO.Path]::GetFullPath($candidate)
}

function Read-Report([string]$PathValue) {
    $path = Resolve-RepoPath $PathValue
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing report: $PathValue" }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-SampleMap($Report) {
    $map = @{}
    foreach ($row in @($Report.results)) {
        if ([string]::IsNullOrWhiteSpace($row.sample)) { throw 'A report row is missing sample.' }
        if ($map.ContainsKey($row.sample)) { throw "Duplicate sample in report: $($row.sample)" }
        $map[$row.sample] = $row
    }
    return $map
}

$ort = Read-Report $OrtReport
$tensorrt = Read-Report $TensorRtReport
foreach ($property in @('sourceRevision', 'split')) {
    if (-not [string]::Equals([string]$ort.$property, [string]$tensorrt.$property, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Reports do not use the same $property. ORT=$($ort.$property); TensorRT=$($tensorrt.$property)"
    }
}

$ortSamples = Get-SampleMap $ort
$tensorSamples = Get-SampleMap $tensorrt
$names = @($ortSamples.Keys | Sort-Object)
if ($names.Count -eq 0 -or $names.Count -ne $tensorSamples.Count) { throw 'Reports do not contain the same number of samples.' }
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($name in $names) {
    if (!$tensorSamples.ContainsKey($name)) { throw "TensorRT report is missing sample: $name" }
    $left = $ortSamples[$name]
    $right = $tensorSamples[$name]
    $leftStructure = $left.structure
    $rightStructure = $right.structure
    $rows.Add([ordered]@{
        sample = $name
        imageSha256Match = [string]::Equals([string]$left.imageSha256, [string]$right.imageSha256, [StringComparison]::OrdinalIgnoreCase)
        finishReasonMatch = [string]::Equals([string]$left.finishReason, [string]$right.finishReason, [StringComparison]::Ordinal)
        ortFinishReason = $left.finishReason
        tensorRtFinishReason = $right.finishReason
        structureMatchesMatch = [bool]$leftStructure.structureMatches -eq [bool]$rightStructure.structureMatches
        ortStructureMatches = [bool]$leftStructure.structureMatches
        tensorRtStructureMatches = [bool]$rightStructure.structureMatches
        expectedCellCountMatch = [int]$leftStructure.expectedCells -eq [int]$rightStructure.expectedCells
        ortCellExactMatchCount = [int]$leftStructure.cellExactMatchCount
        tensorRtCellExactMatchCount = [int]$rightStructure.cellExactMatchCount
        cellExactMatchDeltaTensorRtMinusOrt = [int]$rightStructure.cellExactMatchCount - [int]$leftStructure.cellExactMatchCount
        tokenCountDeltaTensorRtMinusOrt = [int]$right.tokenCountIncludingEos - [int]$left.tokenCountIncludingEos
        ortTotalMs = [double]$left.totalMs
        tensorRtTotalMs = [double]$right.totalMs
        totalMsDeltaTensorRtMinusOrt = [double]$right.totalMs - [double]$left.totalMs
        totalMsRatioTensorRtOverOrt = if ([double]$left.totalMs -gt 0) { [double]$right.totalMs / [double]$left.totalMs } else { $null }
        ortDecodeP50Ms = [double]$left.decodeP50Ms
        tensorRtDecodeP50Ms = [double]$right.decodeP50Ms
        ortDecodeP95Ms = [double]$left.decodeP95Ms
        tensorRtDecodeP95Ms = [double]$right.decodeP95Ms
    })
}

$output = [ordered]@{
    schemaVersion = 'deploysharp-chart2table-backend-alignment-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    dataset = $ort.dataset
    split = $ort.split
    sourceRepository = $ort.sourceRepository
    sourceRevision = $ort.sourceRevision
    ortBackend = $ort.backend
    tensorRtBackend = $tensorrt.backend
    sampleCount = $rows.Count
    imageSha256Matches = @($rows | Where-Object { $_['imageSha256Match'] }).Count
    finishReasonMatches = @($rows | Where-Object { $_['finishReasonMatch'] }).Count
    structureMatchesMatches = @($rows | Where-Object { $_['structureMatchesMatch'] }).Count
    expectedCellCountMatches = @($rows | Where-Object { $_['expectedCellCountMatch'] }).Count
    ortEosCount = @($rows | Where-Object { $_['ortFinishReason'] -eq 'EndOfSequence' }).Count
    tensorRtEosCount = @($rows | Where-Object { $_['tensorRtFinishReason'] -eq 'EndOfSequence' }).Count
    totalCellExactMatchesOrt = (@($rows | ForEach-Object { [int]$_['ortCellExactMatchCount'] }) | Measure-Object -Sum).Sum
    totalCellExactMatchesTensorRt = (@($rows | ForEach-Object { [int]$_['tensorRtCellExactMatchCount'] }) | Measure-Object -Sum).Sum
    rows = $rows
    boundary = 'This compares two already-generated bounded evidence files. It is not a new inference run, a dataset accuracy score, or a controlled cross-backend performance benchmark.'
}

$outputFull = Resolve-RepoPath $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputFull) | Out-Null
$output | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputFull -Encoding utf8

if (-not [string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $markdownFull = Resolve-RepoPath $MarkdownPath
    $speedRows = @($rows | Sort-Object { $_['totalMsRatioTensorRtOverOrt'] } -Descending)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# Chart2Table ORT/TensorRT evidence alignment')
    $lines.Add('')
    $lines.Add(('This report compares the same {0} `{1}` samples at ChartQA revision `{2}`. It does not rerun inference and does not claim split accuracy or a controlled benchmark.' -f $rows.Count, $ort.split, $ort.sourceRevision))
    $lines.Add('')
    $lines.Add('| Measure | Result |')
    $lines.Add('| --- | ---: |')
    $lines.Add("| Image SHA matches | $($output.imageSha256Matches)/$($output.sampleCount) |")
    $lines.Add("| Finish reason matches | $($output.finishReasonMatches)/$($output.sampleCount) |")
    $lines.Add("| EOS | ORT $($output.ortEosCount)/$($output.sampleCount); TensorRT $($output.tensorRtEosCount)/$($output.sampleCount) |")
    $lines.Add("| Structure flag matches | $($output.structureMatchesMatches)/$($output.sampleCount) |")
    $lines.Add("| Expected cell count matches | $($output.expectedCellCountMatches)/$($output.sampleCount) |")
    $lines.Add("| Exact cells | ORT $($output.totalCellExactMatchesOrt); TensorRT $($output.totalCellExactMatchesTensorRt) |")
    $lines.Add('')
    $lines.Add('| Sample | ORT ms | TensorRT ms | Ratio | ORT cells | TensorRT cells | Structure same |')
    $lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | :---: |')
    foreach ($row in $speedRows) {
        $ratio = if ($null -eq $row['totalMsRatioTensorRtOverOrt']) { 'n/a' } else { '{0:N3}' -f $row['totalMsRatioTensorRtOverOrt'] }
        $same = if ($row['structureMatchesMatch']) { 'yes' } else { 'no' }
        $lines.Add(('| `{0}` | {1:N2} | {2:N2} | {3} | {4} | {5} | {6} |' -f $row['sample'], $row['ortTotalMs'], $row['tensorRtTotalMs'], $ratio, $row['ortCellExactMatchCount'], $row['tensorRtCellExactMatchCount'], $same))
    }
    $lines.Add('')
    $lines.Add('TensorRT and ORT can agree on EOS while disagreeing on table structure or cells; those dimensions are kept separate above. Timing ratios are host- and plan-specific observations only.')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFull) | Out-Null
    $lines -join [Environment]::NewLine | Set-Content -LiteralPath $markdownFull -Encoding utf8
}

Write-Output ("Compared {0} Chart2Table samples; imageSha={1}; structureMatches={2}; output={3}" -f $output.sampleCount, $output.imageSha256Matches, $output.structureMatchesMatches, $outputFull)
