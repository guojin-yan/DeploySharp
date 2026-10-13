param(
    [Parameter(Mandatory = $true)]
    [string]$OrtReport,
    [Parameter(Mandatory = $true)]
    [Alias('TensorRtReport')]
    [string]$BackendReport,
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

function Get-SampleMap($Report, [string]$BackendName) {
    $map = @{}
    foreach ($row in @($Report.results)) {
        if ([string]::IsNullOrWhiteSpace($row.sample)) { throw "$BackendName report has a row missing sample." }
        if ($map.ContainsKey($row.sample)) { throw "$BackendName report has a duplicate sample: $($row.sample)" }
        $map[$row.sample] = $row
    }
    return $map
}

function Get-FirstStringProperty($Object, [string[]]$Names) {
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -ne $property -and $null -ne $property.Value -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return [string]$property.Value
        }
    }
    return $null
}

$ort = Read-Report $OrtReport
$other = Read-Report $BackendReport
$leftName = [string]$ort.backend
$rightName = [string]$other.backend
if ([string]::IsNullOrWhiteSpace($leftName) -or [string]::IsNullOrWhiteSpace($rightName)) {
    throw 'Both reports must declare a non-empty backend.'
}
if (-not [string]::Equals([string]$ort.modelId, [string]$other.modelId, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Reports do not use the same modelId. $leftName=$($ort.modelId); $rightName=$($other.modelId)"
}
if ([int]$ort.sampleCount -ne @($ort.results).Count -or [int]$other.sampleCount -ne @($other.results).Count) {
    throw 'At least one report sampleCount does not match its result rows.'
}
foreach ($property in @('dataset', 'sourceRepository', 'sourceRevision', 'split')) {
    if (-not [string]::Equals([string]$ort.$property, [string]$other.$property, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Reports do not use the same $property. $leftName=$($ort.$property); $rightName=$($other.$property)"
    }
}
if ([int]$ort.maximumNewTokens -ne [int]$other.maximumNewTokens) {
    throw "Reports use different generation budgets. $leftName=$($ort.maximumNewTokens); $rightName=$($other.maximumNewTokens)"
}

$leftSamples = Get-SampleMap $ort $leftName
$rightSamples = Get-SampleMap $other $rightName
$names = @($leftSamples.Keys | Sort-Object)
if ($names.Count -eq 0 -or $names.Count -ne $rightSamples.Count) { throw 'Reports do not contain the same number of samples.' }
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($name in $names) {
    if (!$rightSamples.ContainsKey($name)) { throw "$rightName report is missing sample: $name" }
    $left = $leftSamples[$name]
    $right = $rightSamples[$name]
    $leftStructure = $left.structure
    $rightStructure = $right.structure
    $rows.Add([ordered]@{
        sample = $name
        imageSha256Match = [string]::Equals([string]$left.imageSha256, [string]$right.imageSha256, [StringComparison]::OrdinalIgnoreCase)
        sourceTableSha256Match = [string]::Equals((Get-FirstStringProperty $left @('sourceTableSha256')), (Get-FirstStringProperty $right @('sourceTableSha256')), [StringComparison]::OrdinalIgnoreCase)
        expectedTableSha256Match = [string]::Equals((Get-FirstStringProperty $left @('expectedTableSha256', 'expectedTextSha256')), (Get-FirstStringProperty $right @('expectedTableSha256', 'expectedTextSha256')), [StringComparison]::OrdinalIgnoreCase)
        generatedTableSha256Match = [string]::Equals((Get-FirstStringProperty $left @('generatedTableSha256', 'generatedTextSha256')), (Get-FirstStringProperty $right @('generatedTableSha256', 'generatedTextSha256')), [StringComparison]::OrdinalIgnoreCase)
        finishReasonMatch = [string]::Equals([string]$left.finishReason, [string]$right.finishReason, [StringComparison]::Ordinal)
        leftFinishReason = $left.finishReason
        rightFinishReason = $right.finishReason
        tokenCountMatch = [int]$left.tokenCountIncludingEos -eq [int]$right.tokenCountIncludingEos
        leftTokenCount = [int]$left.tokenCountIncludingEos
        rightTokenCount = [int]$right.tokenCountIncludingEos
        expectedDimensionsMatch = ([int]$leftStructure.expectedRows -eq [int]$rightStructure.expectedRows) -and ([int]$leftStructure.expectedColumns -eq [int]$rightStructure.expectedColumns)
        generatedDimensionsMatch = ([int]$leftStructure.actualRows -eq [int]$rightStructure.actualRows) -and ([int]$leftStructure.actualColumns -eq [int]$rightStructure.actualColumns)
        actualStructurallyValidMatch = [bool]$leftStructure.actualStructurallyValid -eq [bool]$rightStructure.actualStructurallyValid
        leftActualStructurallyValid = [bool]$leftStructure.actualStructurallyValid
        rightActualStructurallyValid = [bool]$rightStructure.actualStructurallyValid
        structureMatchesMatch = [bool]$leftStructure.structureMatches -eq [bool]$rightStructure.structureMatches
        leftStructureMatches = [bool]$leftStructure.structureMatches
        rightStructureMatches = [bool]$rightStructure.structureMatches
        exactTextMatchFlagMatch = [bool]$leftStructure.exactTextMatch -eq [bool]$rightStructure.exactTextMatch
        leftExactTextMatch = [bool]$leftStructure.exactTextMatch
        rightExactTextMatch = [bool]$rightStructure.exactTextMatch
        expectedCellCountMatch = [int]$leftStructure.expectedCells -eq [int]$rightStructure.expectedCells
        actualCellCountMatch = [int]$leftStructure.actualCells -eq [int]$rightStructure.actualCells
        leftActualCellCount = [int]$leftStructure.actualCells
        rightActualCellCount = [int]$rightStructure.actualCells
        leftCellExactMatchCount = [int]$leftStructure.cellExactMatchCount
        rightCellExactMatchCount = [int]$rightStructure.cellExactMatchCount
        leftRowExactMatchCount = [int]$leftStructure.rowExactMatchCount
        rightRowExactMatchCount = [int]$rightStructure.rowExactMatchCount
        cellExactMatchDeltaRightMinusLeft = [int]$rightStructure.cellExactMatchCount - [int]$leftStructure.cellExactMatchCount
        rowExactMatchDeltaRightMinusLeft = [int]$rightStructure.rowExactMatchCount - [int]$leftStructure.rowExactMatchCount
        leftTotalMs = [double]$left.totalMs
        rightTotalMs = [double]$right.totalMs
        totalMsDeltaRightMinusLeft = [double]$right.totalMs - [double]$left.totalMs
        totalMsRatioRightOverLeft = if ([double]$left.totalMs -gt 0) { [double]$right.totalMs / [double]$left.totalMs } else { $null }
        leftDecodeP50Ms = [double]$left.decodeP50Ms
        rightDecodeP50Ms = [double]$right.decodeP50Ms
        leftDecodeP95Ms = [double]$left.decodeP95Ms
        rightDecodeP95Ms = [double]$right.decodeP95Ms
    })
}

function Count-Matches([string]$Property) {
    return @($rows | Where-Object { [bool]($_.$Property) }).Count
}

$output = [ordered]@{
    schemaVersion = 'deploysharp-chart2table-backend-alignment-v3'
    generatedUtc = [DateTimeOffset]::UtcNow
    dataset = if ([string]::IsNullOrWhiteSpace([string]$ort.dataset)) { 'ChartQA' } else { $ort.dataset }
    split = $ort.split
    sourceRepository = $ort.sourceRepository
    sourceRevision = $ort.sourceRevision
    maximumNewTokens = [int]$ort.maximumNewTokens
    leftBackend = $leftName
    rightBackend = $rightName
    sampleCount = $rows.Count
    imageSha256Matches = Count-Matches 'imageSha256Match'
    sourceTableSha256Matches = Count-Matches 'sourceTableSha256Match'
    expectedTableSha256Matches = Count-Matches 'expectedTableSha256Match'
    generatedTableSha256Matches = Count-Matches 'generatedTableSha256Match'
    finishReasonMatches = Count-Matches 'finishReasonMatch'
    tokenCountMatches = Count-Matches 'tokenCountMatch'
    expectedDimensionsMatches = Count-Matches 'expectedDimensionsMatch'
    generatedDimensionsMatches = Count-Matches 'generatedDimensionsMatch'
    actualStructurallyValidFlags = Count-Matches 'actualStructurallyValidMatch'
    structureMatchesFlags = Count-Matches 'structureMatchesMatch'
    exactTextMatchFlags = Count-Matches 'exactTextMatchFlagMatch'
    expectedCellCountMatches = Count-Matches 'expectedCellCountMatch'
    actualCellCountMatches = Count-Matches 'actualCellCountMatch'
    totalRowExactMatchesLeft = [int]((@($rows | ForEach-Object { [int]$_['leftRowExactMatchCount'] }) | Measure-Object -Sum).Sum)
    totalRowExactMatchesRight = [int]((@($rows | ForEach-Object { [int]$_['rightRowExactMatchCount'] }) | Measure-Object -Sum).Sum)
    leftEosCount = @($rows | Where-Object { $_.leftFinishReason -eq 'EndOfSequence' }).Count
    rightEosCount = @($rows | Where-Object { $_.rightFinishReason -eq 'EndOfSequence' }).Count
    totalCellExactMatchesLeft = [int]((@($rows | ForEach-Object { [int]$_['leftCellExactMatchCount'] }) | Measure-Object -Sum).Sum)
    totalCellExactMatchesRight = [int]((@($rows | ForEach-Object { [int]$_['rightCellExactMatchCount'] }) | Measure-Object -Sum).Sum)
    rows = $rows
    boundary = 'This compares two already-generated bounded evidence files. It is not a new inference run, a dataset accuracy score, or a controlled cross-backend performance benchmark.'
}

$outputFull = Resolve-RepoPath $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputFull) | Out-Null
$output | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputFull -Encoding utf8

if (-not [string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $markdownFull = Resolve-RepoPath $MarkdownPath
    $speedRows = @($rows | Sort-Object { $_['totalMsRatioRightOverLeft'] } -Descending)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Chart2Table $leftName / $rightName evidence alignment")
    $lines.Add('')
    $lines.Add(('This report compares the same {0} `{1}` samples at ChartQA revision `{2}` with a {3}-token generation budget. It does not rerun inference and does not claim split accuracy or a controlled benchmark.' -f $rows.Count, $ort.split, $ort.sourceRevision, $ort.maximumNewTokens))
    $lines.Add('')
    $lines.Add('| Measure | Result |')
    $lines.Add('| --- | ---: |')
    $lines.Add("| Image SHA matches | $($output.imageSha256Matches)/$($output.sampleCount) |")
    $lines.Add("| Source table / expected text SHA matches | $($output.sourceTableSha256Matches)/$($output.sampleCount); $($output.expectedTableSha256Matches)/$($output.sampleCount) |")
    $lines.Add("| Generated table SHA matches | $($output.generatedTableSha256Matches)/$($output.sampleCount) |")
    $lines.Add("| Finish reason / token count matches | $($output.finishReasonMatches)/$($output.sampleCount); $($output.tokenCountMatches)/$($output.sampleCount) |")
    $lines.Add("| EOS | $leftName $($output.leftEosCount)/$($output.sampleCount); $rightName $($output.rightEosCount)/$($output.sampleCount) |")
    $lines.Add("| Expected / generated dimensions match | $($output.expectedDimensionsMatches)/$($output.sampleCount); $($output.generatedDimensionsMatches)/$($output.sampleCount) |")
    $lines.Add("| Parseable / structure / exact-text flags match | $($output.actualStructurallyValidFlags)/$($output.sampleCount); $($output.structureMatchesFlags)/$($output.sampleCount); $($output.exactTextMatchFlags)/$($output.sampleCount) |")
    $lines.Add("| Expected / actual cell counts match | $($output.expectedCellCountMatches)/$($output.sampleCount); $($output.actualCellCountMatches)/$($output.sampleCount) |")
    $lines.Add("| Exact rows | $leftName $($output.totalRowExactMatchesLeft); $rightName $($output.totalRowExactMatchesRight) |")
    $lines.Add("| Exact cells | $leftName $($output.totalCellExactMatchesLeft); $rightName $($output.totalCellExactMatchesRight) |")
    $lines.Add('')
    $lines.Add("| Sample | $leftName ms | $rightName ms | Ratio | $leftName cells | $rightName cells | Generated SHA same |")
    $lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | :---: |')
    foreach ($row in $speedRows) {
        $ratio = if ($null -eq $row['totalMsRatioRightOverLeft']) { 'n/a' } else { '{0:N3}' -f $row['totalMsRatioRightOverLeft'] }
        $same = if ($row['generatedTableSha256Match']) { 'yes' } else { 'no' }
        $lines.Add(('| `{0}` | {1:N2} | {2:N2} | {3} | {4} | {5} | {6} |' -f $row['sample'], $row['leftTotalMs'], $row['rightTotalMs'], $ratio, $row['leftCellExactMatchCount'], $row['rightCellExactMatchCount'], $same))
    }
    $lines.Add('')
    $lines.Add("Backends can agree on EOS while disagreeing on table structure or cells; these dimensions are kept separate. Timing ratios are host- and runtime-specific observations only.")
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFull) | Out-Null
    $lines -join [Environment]::NewLine | Set-Content -LiteralPath $markdownFull -Encoding utf8
}

Write-Output ("Compared {0} Chart2Table samples; imageSha={1}; generatedTableSha={2}; output={3}" -f $output.sampleCount, $output.imageSha256Matches, $output.generatedTableSha256Matches, $outputFull)
