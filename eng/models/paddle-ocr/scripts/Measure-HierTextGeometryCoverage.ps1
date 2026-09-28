[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$PredictionsPath,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Get-Polygon([object]$value) {
    $points = [System.Collections.Generic.List[object]]::new()
    foreach ($point in @($value)) {
        if ($null -eq $point -or @($point).Count -lt 2) { continue }
        $points.Add([double[]]@([double]$point[0], [double]$point[1]))
    }
    return ,$points.ToArray()
}

function Get-SignedArea([object[]]$polygon) {
    if ($null -eq $polygon -or $polygon.Count -lt 3) { return 0.0 }
    [double]$sum = 0
    for ($index = 0; $index -lt $polygon.Count; $index++) {
        $next = ($index + 1) % $polygon.Count
        $sum += $polygon[$index][0] * $polygon[$next][1] - $polygon[$next][0] * $polygon[$index][1]
    }
    return $sum / 2.0
}

function Get-Area([object[]]$polygon) {
    return [Math]::Abs((Get-SignedArea $polygon))
}

function Get-Cross([double[]]$a, [double[]]$b, [double[]]$c) {
    return ($b[0] - $a[0]) * ($c[1] - $a[1]) - ($b[1] - $a[1]) * ($c[0] - $a[0])
}

function Clip-Polygon([object[]]$subject, [double[]]$a, [double[]]$b, [double]$orientation) {
    if ($null -eq $subject -or $subject.Count -lt 3) { return ,@() }
    $clippedVertices = [System.Collections.Generic.List[object]]::new()
    $count = $subject.Count
    for ($index = 0; $index -lt $count; $index++) {
        $current = [double[]]$subject[$index]
        $previous = [double[]]$subject[($index + $count - 1) % $count]
        $currentCross = Get-Cross $a $b $current
        $previousCross = Get-Cross $a $b $previous
        $currentInside = $orientation * $currentCross -ge -1e-8
        $previousInside = $orientation * $previousCross -ge -1e-8
        if ($currentInside -ne $previousInside) {
            [double]$denominator = [double]$previousCross - [double]$currentCross
            [double]$fraction = if ([Math]::Abs($denominator) -gt 1e-12) { [double]$previousCross / $denominator } else { 0.0 }
            [double]$deltaX = [double]$current[0] - [double]$previous[0]
            [double]$deltaY = [double]$current[1] - [double]$previous[1]
            [double[]]$intersection = [double[]]::new(2)
            $intersection[0] = [double]$previous[0] + $deltaX * $fraction
            $intersection[1] = [double]$previous[1] + $deltaY * $fraction
            $clippedVertices.Add($intersection)
        }
        if ($currentInside) { $clippedVertices.Add($current) }
    }
    return $clippedVertices.ToArray()
}

function Get-IntersectionArea([object[]]$subject, [object[]]$clipper) {
    if ($null -eq $subject -or $null -eq $clipper -or $subject.Count -lt 3 -or $clipper.Count -lt 3) { return 0.0 }
    $orientation = if ((Get-SignedArea $clipper) -ge 0) { 1.0 } else { -1.0 }
    $current = @($subject)
    for ($index = 0; $index -lt $clipper.Count; $index++) {
        $current = @(Clip-Polygon $current ([double[]]$clipper[$index]) ([double[]]$clipper[($index + 1) % $clipper.Count]) $orientation)
        if ($current.Count -lt 3) { return 0.0 }
    }
    return Get-Area $current
}

function Get-IoU([object[]]$left, [object[]]$right) {
    $intersection = Get-IntersectionArea $left $right
    $union = (Get-Area $left) + (Get-Area $right) - $intersection
    if ($union -le 0) { return 0.0 }
    return $intersection / $union
}

$manifestFullPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$predictionsFullPath = (Resolve-Path -LiteralPath $PredictionsPath).Path
$manifestById = @{}
foreach ($line in Get-Content -LiteralPath $manifestFullPath) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $record = $line | ConvertFrom-Json
    $manifestById[[string]$record.image_id] = $record
}
$predictionDocument = Get-Content -Raw -LiteralPath $predictionsFullPath | ConvertFrom-Json
$rows = [System.Collections.Generic.List[object]]::new()
$imageRows = [System.Collections.Generic.List[object]]::new()

foreach ($image in @($predictionDocument.images)) {
    $imageId = [string]$image.image_id
    if (-not $manifestById.ContainsKey($imageId)) { throw "Prediction image is not present in the manifest: $imageId" }
    $groundTruth = $manifestById[$imageId]
    $predictions = @(
        foreach ($region in @($image.regions)) {
            $polygon = Get-Polygon $region.polygon
            if ($polygon.Count -ge 3) { [pscustomobject]@{ Polygon = $polygon } }
        }
    )
    $imageGt = 0
    $imageMatched = 0
    $predictionGroundTruthCoverageCounts = @{}
    foreach ($instance in @($groundTruth.instances)) {
        if ([bool]$instance.ignore) { continue }
        $gtPolygon = Get-Polygon $instance.polygon
        if ($gtPolygon.Count -lt 3) { continue }
        $gtArea = Get-Area $gtPolygon
        if ($gtArea -le 0) { continue }
        $overlaps = [System.Collections.Generic.List[object]]::new()
        $predictionIndex = 0
        foreach ($prediction in $predictions) {
            $intersection = Get-IntersectionArea $gtPolygon $prediction.Polygon
            if ($intersection -gt 0) {
                $coverage = $intersection / $gtArea
                $predictionArea = Get-Area $prediction.Polygon
                $predictionCoverage = if ($predictionArea -gt 0) { $intersection / $predictionArea } else { 0.0 }
                $iou = Get-IoU $gtPolygon $prediction.Polygon
                $overlaps.Add([pscustomobject]@{ Coverage = $coverage; PredictionCoverage = $predictionCoverage; IoU = $iou })
                if ($coverage -ge 0.1 -and $predictionCoverage -ge 0.1) {
                    if (-not $predictionGroundTruthCoverageCounts.ContainsKey($predictionIndex)) { $predictionGroundTruthCoverageCounts[$predictionIndex] = 0 }
                    $predictionGroundTruthCoverageCounts[$predictionIndex]++
                }
            }
            $predictionIndex++
        }
        $bestOverlap = if ($overlaps.Count -gt 0) { $overlaps | Sort-Object -Property Coverage -Descending | Select-Object -First 1 } else { $null }
        $maxCoverage = if ($overlaps.Count -gt 0) { ($overlaps | Measure-Object -Property Coverage -Maximum).Maximum } else { 0.0 }
        $sumCoverage = [Math]::Min(1.0, [double](($overlaps | Measure-Object -Property Coverage -Sum).Sum))
        $bestIoU = if ($overlaps.Count -gt 0) { ($overlaps | Measure-Object -Property IoU -Maximum).Maximum } else { 0.0 }
        $strongOverlapCount = @($overlaps | Where-Object { $_.Coverage -ge 0.05 }).Count
        $imageGt++
        if ($bestIoU -ge 0.5) { $imageMatched++ }
        $rows.Add([pscustomobject]@{
            ImageId = $imageId
            InstanceId = [string]$instance.instance_id
            TextLength = ([string]$instance.text).Length
            MaxSinglePredictionCoverage = [Math]::Round([double]$maxCoverage, 6)
            SumPredictionCoverage = [Math]::Round([double]$sumCoverage, 6)
            PredictionCoverageForBestGroundTruthOverlap = [Math]::Round([double]$(if ($null -eq $bestOverlap) { 0.0 } else { $bestOverlap.PredictionCoverage }), 6)
            BestPredictionIoU = [Math]::Round([double]$bestIoU, 6)
            AtLeastHalfCoverageOnBothPolygons = [bool]($null -ne $bestOverlap -and $bestOverlap.Coverage -ge 0.5 -and $bestOverlap.PredictionCoverage -ge 0.5)
            StrongOverlapCount = $strongOverlapCount
        })
    }
    $mergedPredictionCounts = @($predictionGroundTruthCoverageCounts.Values | Where-Object { $_ -ge 2 })
    $imageRows.Add([pscustomobject]@{
        ImageId = $imageId
        GroundTruthRegions = $imageGt
        Predictions = $predictions.Count
        IoUMatchedRegions = $imageMatched
        PredictionsCoveringAtLeastTwoGroundTruthRegions = $mergedPredictionCounts.Count
        MaximumGroundTruthRegionsCoveredByOnePrediction = if ($predictionGroundTruthCoverageCounts.Count -gt 0) { ($predictionGroundTruthCoverageCounts.Values | Measure-Object -Maximum).Maximum } else { 0 }
    })
}

function Count-At([object[]]$items, [string]$property, [double]$threshold) {
    return @($items | Where-Object { [double]$_.$property -ge $threshold }).Count
}

$output = [ordered]@{
    SchemaVersion = 1
    GeneratedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Manifest = $manifestFullPath
    Predictions = $predictionsFullPath
    GroundTruthRegions = $rows.Count
    PredictionImages = $imageRows.Count
    MaxSinglePredictionCoverageAtLeast50Percent = Count-At $rows MaxSinglePredictionCoverage 0.5
    SumPredictionCoverageAtLeast50Percent = Count-At $rows SumPredictionCoverage 0.5
    BestOverlapAtLeast50PercentOnBothPolygons = @($rows | Where-Object { $_.AtLeastHalfCoverageOnBothPolygons }).Count
    BestPredictionIoUAtLeast50Percent = Count-At $rows BestPredictionIoU 0.5
    CombinedCoverageWithoutSingleIoUMatch = @($rows | Where-Object { $_.SumPredictionCoverage -ge 0.5 -and $_.BestPredictionIoU -lt 0.5 }).Count
    StrongOverlapCountAtLeastTwo = @($rows | Where-Object { $_.StrongOverlapCount -ge 2 }).Count
    PredictionsCoveringAtLeastTwoGroundTruthRegions = [int](($imageRows | Measure-Object -Property PredictionsCoveringAtLeastTwoGroundTruthRegions -Sum).Sum)
    MaximumGroundTruthRegionsCoveredByOnePrediction = if ($imageRows.Count -gt 0) { ($imageRows | Measure-Object -Property MaximumGroundTruthRegionsCoveredByOnePrediction -Maximum).Maximum } else { 0 }
    Rows = $rows
    Images = $imageRows
}
$json = $output | ConvertTo-Json -Depth 8
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Output $json
} else {
    $directory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8
    Write-Output (Resolve-Path -LiteralPath $OutputPath).Path
}
