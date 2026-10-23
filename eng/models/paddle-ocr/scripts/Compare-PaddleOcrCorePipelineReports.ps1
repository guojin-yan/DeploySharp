[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OrtReportPath,
    [Parameter(Mandatory)]
    [string]$OpenVinoReportPath,
    [Parameter(Mandatory)]
    [string]$OutputJsonPath,
    [Parameter(Mandatory)]
    [string]$OutputMarkdownPath,
    [double]$CoordinateTolerancePixels = 0.01,
    [double]$ConfidenceTolerance = 0.001,
    [double]$DetectorScoreTolerance = 0.001,
    [string]$OrtReportReference = [IO.Path]::GetFileName($OrtReportPath),
    [string]$OpenVinoReportReference = [IO.Path]::GetFileName($OpenVinoReportPath)
)

$ErrorActionPreference = 'Stop'
if ($CoordinateTolerancePixels -lt 0 -or $ConfidenceTolerance -lt 0 -or $DetectorScoreTolerance -lt 0) {
    throw 'Comparison tolerances cannot be negative.'
}

function Read-Report([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Get-Key($Run) {
    return '{0}|{1}' -f $Run.Variant, $Run.ImageFileName
}

function Assert-UniqueCompleteRuns($Report, [string]$Backend) {
    if (@($Report.Runs).Count -ne 21) {
        throw "$Backend report must contain exactly 21 model/image runs; found $(@($Report.Runs).Count)."
    }

    $keys = @($Report.Runs | ForEach-Object { Get-Key $_ })
    if (@($keys | Sort-Object -Unique).Count -ne 21) {
        throw "$Backend report contains duplicate model/image keys."
    }

    $expectedModels = @('v4-mobile', 'v4-server', 'v5-mobile', 'v5-server', 'v6-tiny', 'v6-small', 'v6-medium')
    $expectedImages = @('demo_1.jpg', 'demo_2.jpg', 'demo_3.jpg')
    foreach ($model in $expectedModels) {
        foreach ($image in $expectedImages) {
            $key = "$model|$image"
            if ($keys -notcontains $key) { throw "$Backend report is missing $key." }
        }
    }
}

$ort = Read-Report $OrtReportPath
$openVino = Read-Report $OpenVinoReportPath
Assert-UniqueCompleteRuns $ort 'ORT'
Assert-UniqueCompleteRuns $openVino 'OpenVINO'

$ortByKey = @{}
foreach ($run in $ort.Runs) { $ortByKey[(Get-Key $run)] = $run }
$pairs = [System.Collections.Generic.List[object]]::new()
$identityMismatches = [System.Collections.Generic.List[string]]::new()
$regionMismatches = [System.Collections.Generic.List[string]]::new()
$textMismatches = [System.Collections.Generic.List[string]]::new()
$geometryComparableCount = 0
$geometryMatchCount = 0
$recognitionComparableCount = 0
$recognitionMatchCount = 0
$tokenDecisionComparableCount = 0
$tokenDecisionMatchCount = 0

foreach ($ovRun in $openVino.Runs) {
    $key = Get-Key $ovRun
    $ortRun = $ortByKey[$key]
    $identityMatches = ($ovRun.ImageSha256 -eq $ortRun.ImageSha256) -and
        ($ovRun.DetectorSha256 -eq $ortRun.DetectorSha256) -and
        ($ovRun.DetectorModelId -eq $ortRun.DetectorModelId) -and
        ($ovRun.ClassifierSha256 -eq $ortRun.ClassifierSha256) -and
        ($ovRun.ClassifierModelId -eq $ortRun.ClassifierModelId) -and
        ($ovRun.RecognizerSha256 -eq $ortRun.RecognizerSha256) -and
        ($ovRun.RecognizerModelId -eq $ortRun.RecognizerModelId) -and
        ($ovRun.DictionarySha256 -eq $ortRun.DictionarySha256)
    $regionMatches = ($ovRun.RegionCount -eq $ortRun.RegionCount) -and
        ($ovRun.RecognizedRegionCount -eq $ortRun.RecognizedRegionCount)
    $textMatches = $ovRun.TextSha256 -eq $ortRun.TextSha256
    $characterSetMetadataMatches = $null -ne $ovRun.CharacterSetId -and
        $null -ne $ortRun.CharacterSetId -and
        $ovRun.CharacterSetId -eq $ortRun.CharacterSetId -and
        $ovRun.CharacterSetVersion -eq $ortRun.CharacterSetVersion -and
        $ovRun.CharacterSetSha256 -eq $ortRun.CharacterSetSha256
    $geometryComparable = $null -ne $ovRun.GeometrySha256 -and $null -ne $ortRun.GeometrySha256
    $geometryMatches = if ($geometryComparable) { $ovRun.GeometrySha256 -eq $ortRun.GeometrySha256 } else { $null }
    $recognitionComparable = $identityMatches -and $null -ne $ovRun.RecognitionSha256 -and $null -ne $ortRun.RecognitionSha256 -and $characterSetMetadataMatches
    $recognitionMatches = if ($recognitionComparable) { $ovRun.RecognitionSha256 -eq $ortRun.RecognitionSha256 } else { $null }

    $ovRegions = if ($null -ne $ovRun.PSObject.Properties['Regions']) { @($ovRun.Regions | Where-Object { $null -ne $_ }) } else { @() }
    $ortRegions = if ($null -ne $ortRun.PSObject.Properties['Regions']) { @($ortRun.Regions | Where-Object { $null -ne $_ }) } else { @() }
    $geometryDetailsComparable = $ovRegions.Count -gt 0 -and $ortRegions.Count -gt 0
    $geometryShapeCompatible = $geometryDetailsComparable -and $ovRegions.Count -eq $ortRegions.Count
    $recognitionDetailsComparable = $geometryDetailsComparable -and $characterSetMetadataMatches
    $recognitionStructureCompatible = $recognitionDetailsComparable -and $ovRegions.Count -eq $ortRegions.Count
    $maxCoordinateAbsDelta = 0.0
    $maxDetectorScoreAbsDelta = 0.0
    $maxRecognitionConfidenceAbsDelta = 0.0
    $maxTokenConfidenceAbsDelta = 0.0
    $orientationMismatchCount = 0
    $sourceIndexMismatchCount = 0
    $tokenIdentityMismatchCount = 0
    $tokenCountMismatchCount = 0
    $tokenDecisionMismatchCount = 0
    for ($regionIndex = 0; $regionIndex -lt [Math]::Min($ovRegions.Count, $ortRegions.Count); $regionIndex++) {
        $ovRegion = $ovRegions[$regionIndex]
        $ortRegion = $ortRegions[$regionIndex]
        if ($ovRegion.SourceIndex -ne $ortRegion.SourceIndex) { $geometryShapeCompatible = $false; $recognitionStructureCompatible = $false; $sourceIndexMismatchCount++ }
        if ($ovRegion.Orientation -ne $ortRegion.Orientation) { $orientationMismatchCount++ }
        if ($ovRegion.Vertices.Count -ne $ortRegion.Vertices.Count) {
            $geometryShapeCompatible = $false
        } else {
            for ($vertexIndex = 0; $vertexIndex -lt $ovRegion.Vertices.Count; $vertexIndex++) {
                $ovVertex = $ovRegion.Vertices[$vertexIndex]
                $ortVertex = $ortRegion.Vertices[$vertexIndex]
                $maxCoordinateAbsDelta = [Math]::Max($maxCoordinateAbsDelta, [Math]::Abs([double]$ovVertex.X - [double]$ortVertex.X))
                $maxCoordinateAbsDelta = [Math]::Max($maxCoordinateAbsDelta, [Math]::Abs([double]$ovVertex.Y - [double]$ortVertex.Y))
            }
        }
        $maxDetectorScoreAbsDelta = [Math]::Max($maxDetectorScoreAbsDelta, [Math]::Abs([double]$ovRegion.DetectorScore - [double]$ortRegion.DetectorScore))

        if ($ovRegion.Text -cne $ortRegion.Text) { $recognitionStructureCompatible = $false }
        $maxRecognitionConfidenceAbsDelta = [Math]::Max($maxRecognitionConfidenceAbsDelta, [Math]::Abs([double]$ovRegion.RecognitionConfidence - [double]$ortRegion.RecognitionConfidence))
        if ($ovRegion.Tokens.Count -ne $ortRegion.Tokens.Count) {
            $tokenCountMismatchCount++
            $recognitionStructureCompatible = $false
            continue
        }
        for ($tokenIndex = 0; $tokenIndex -lt $ovRegion.Tokens.Count; $tokenIndex++) {
            $ovToken = $ovRegion.Tokens[$tokenIndex]
            $ortToken = $ortRegion.Tokens[$tokenIndex]
            if ($ovToken.Timestep -ne $ortToken.Timestep -or $ovToken.ClassIndex -ne $ortToken.ClassIndex -or
                $ovToken.Text -cne $ortToken.Text -or $ovToken.IsBlank -ne $ortToken.IsBlank -or
                $ovToken.IsCollapsedRepeat -ne $ortToken.IsCollapsedRepeat -or $ovToken.IsUnknown -ne $ortToken.IsUnknown -or
                $ovToken.Emitted -ne $ortToken.Emitted) { $tokenIdentityMismatchCount++; $tokenDecisionMismatchCount++ }
            $maxTokenConfidenceAbsDelta = [Math]::Max($maxTokenConfidenceAbsDelta, [Math]::Abs([double]$ovToken.Confidence - [double]$ortToken.Confidence))
        }
    }
    $geometryWithinTolerance = if ($geometryShapeCompatible) {
        $orientationMismatchCount -eq 0 -and $maxCoordinateAbsDelta -le $CoordinateTolerancePixels -and $maxDetectorScoreAbsDelta -le $DetectorScoreTolerance
    } else { $null }
    $recognitionStructureCompatible = $recognitionStructureCompatible -and $tokenCountMismatchCount -eq 0 -and $tokenIdentityMismatchCount -eq 0
    $recognitionWithinTolerance = if ($recognitionStructureCompatible) {
        $maxRecognitionConfidenceAbsDelta -le $ConfidenceTolerance -and $maxTokenConfidenceAbsDelta -le $ConfidenceTolerance
    } else { $null }
    $tokenDecisionComparable = $identityMatches -and $characterSetMetadataMatches -and $ovRegions.Count -gt 0 -and $ovRegions.Count -eq $ortRegions.Count -and
        $tokenCountMismatchCount -eq 0 -and $sourceIndexMismatchCount -eq 0
    $tokenDecisionsMatch = $tokenDecisionComparable -and $tokenDecisionMismatchCount -eq 0
    $resultComparable = $identityMatches -and $characterSetMetadataMatches
    $resultMatches = if ($resultComparable) { $ovRun.ResultSha256 -eq $ortRun.ResultSha256 } else { $null }

    if ($geometryComparable) { $geometryComparableCount++; if ($geometryMatches) { $geometryMatchCount++ } }
    if ($recognitionComparable) { $recognitionComparableCount++; if ($recognitionMatches) { $recognitionMatchCount++ } }
    if ($tokenDecisionComparable) { $tokenDecisionComparableCount++; if ($tokenDecisionsMatch) { $tokenDecisionMatchCount++ } }

    if (-not $identityMatches) { $identityMismatches.Add($key) }
    if (-not $regionMatches) { $regionMismatches.Add($key) }
    if (-not $textMatches) { $textMismatches.Add($key) }

    $pairs.Add([pscustomobject]@{
        Variant = $ovRun.Variant
        ImageFileName = $ovRun.ImageFileName
        ImageSha256 = $ovRun.ImageSha256
        DetectorSha256 = $ovRun.DetectorSha256
        ClassifierSha256 = $ovRun.ClassifierSha256
        RecognizerSha256 = $ovRun.RecognizerSha256
        DictionarySha256 = $ovRun.DictionarySha256
        OpenVinoRegionCount = $ovRun.RegionCount
        OrtRegionCount = $ortRun.RegionCount
        OpenVinoRecognizedRegionCount = $ovRun.RecognizedRegionCount
        OrtRecognizedRegionCount = $ortRun.RecognizedRegionCount
        IdentityMatches = $identityMatches
        CharacterSetMetadataMatches = $characterSetMetadataMatches
        RegionCountsMatch = $regionMatches
        TextSha256Matches = $textMatches
        GeometrySha256Matches = $geometryMatches
        OpenVinoGeometrySha256 = $ovRun.GeometrySha256
        OrtGeometrySha256 = $ortRun.GeometrySha256
        RecognitionSha256Matches = $recognitionMatches
        OpenVinoRecognitionSha256 = $ovRun.RecognitionSha256
        OrtRecognitionSha256 = $ortRun.RecognitionSha256
        GeometryShapeCompatible = $geometryShapeCompatible
        GeometryDetailsComparable = $geometryDetailsComparable
        MaxCoordinateAbsoluteDelta = if ($geometryShapeCompatible) { $maxCoordinateAbsDelta } else { $null }
        MaxDetectorScoreAbsoluteDelta = if ($geometryShapeCompatible) { $maxDetectorScoreAbsDelta } else { $null }
        OrientationMismatchCount = $orientationMismatchCount
        SourceIndexMismatchCount = $sourceIndexMismatchCount
        GeometryWithinTolerance = $geometryWithinTolerance
        RecognitionStructureCompatible = $recognitionStructureCompatible
        RecognitionDetailsComparable = $recognitionDetailsComparable
        MaxRecognitionConfidenceAbsoluteDelta = if ($recognitionStructureCompatible) { $maxRecognitionConfidenceAbsDelta } else { $null }
        TokenIdentityMismatchCount = $tokenIdentityMismatchCount
        TokenDecisionMismatchCount = $tokenDecisionMismatchCount
        TokenCountMismatchCount = $tokenCountMismatchCount
        MaxTokenConfidenceAbsoluteDelta = if ($recognitionStructureCompatible) { $maxTokenConfidenceAbsDelta } else { $null }
        RecognitionWithinTolerance = $recognitionWithinTolerance
        TokenDecisionComparable = $tokenDecisionComparable
        TokenDecisionsMatch = $tokenDecisionsMatch
        OpenVinoTextSha256 = $ovRun.TextSha256
        OrtTextSha256 = $ortRun.TextSha256
        CanonicalResultComparable = $resultComparable
        CanonicalResultSha256Matches = $resultMatches
        OpenVinoResultSha256 = $ovRun.ResultSha256
        OrtResultSha256 = $ortRun.ResultSha256
    })
}

if ($identityMismatches.Count -gt 0) { throw "Image/model identity mismatch: $($identityMismatches -join ', ')." }
if ($regionMismatches.Count -gt 0) { throw "Region/recognized count mismatch: $($regionMismatches -join ', ')." }
if ($textMismatches.Count -gt 0) { throw "Ordered text fingerprint mismatch: $($textMismatches -join ', ')." }

$models = [System.Collections.Generic.List[object]]::new()
foreach ($group in $pairs | Group-Object Variant | Sort-Object Name) {
    $ordered = @($group.Group | Sort-Object ImageFileName)
    $models.Add([pscustomobject]@{
        Variant = $group.Name
        ImageCount = $ordered.Count
        RegionsByImage = @($ordered | ForEach-Object {
            [pscustomobject]@{ ImageFileName = $_.ImageFileName; RegionCount = $_.OpenVinoRegionCount }
        })
        TotalRegionsPerBackend = ($ordered | Measure-Object OpenVinoRegionCount -Sum).Sum
        TextParityCount = @($ordered | Where-Object TextSha256Matches).Count
        CanonicalResultComparableCount = @($ordered | Where-Object { $_.CanonicalResultComparable }).Count
        CanonicalResultParityCount = @($ordered | Where-Object { $_.CanonicalResultSha256Matches -eq $true }).Count
        GeometryParityCount = @($ordered | Where-Object { $_.GeometrySha256Matches -eq $true }).Count
        RecognitionParityCount = @($ordered | Where-Object { $_.RecognitionSha256Matches -eq $true }).Count
        TokenDecisionParityCount = @($ordered | Where-Object TokenDecisionsMatch).Count
        TokenDecisionComparableCount = @($ordered | Where-Object TokenDecisionComparable).Count
        GeometryWithinToleranceCount = @($ordered | Where-Object GeometryWithinTolerance).Count
        GeometryToleranceComparableCount = @($ordered | Where-Object { $null -ne $_.GeometryWithinTolerance }).Count
        RecognitionWithinToleranceCount = @($ordered | Where-Object RecognitionWithinTolerance).Count
        RecognitionToleranceComparableCount = @($ordered | Where-Object { $null -ne $_.RecognitionWithinTolerance }).Count
        MaxCoordinateAbsoluteDelta = ($ordered | Measure-Object MaxCoordinateAbsoluteDelta -Maximum).Maximum
        MaxDetectorScoreAbsoluteDelta = ($ordered | Measure-Object MaxDetectorScoreAbsoluteDelta -Maximum).Maximum
        MaxRecognitionConfidenceAbsoluteDelta = ($ordered | Measure-Object MaxRecognitionConfidenceAbsoluteDelta -Maximum).Maximum
        MaxTokenConfidenceAbsoluteDelta = ($ordered | Measure-Object MaxTokenConfidenceAbsoluteDelta -Maximum).Maximum
    })
}

$canonicalComparableCount = @($pairs | Where-Object CanonicalResultComparable).Count
$geometryToleranceComparableCount = @($pairs | Where-Object { $null -ne $_.GeometryWithinTolerance }).Count
$recognitionToleranceComparableCount = @($pairs | Where-Object { $null -ne $_.RecognitionWithinTolerance }).Count
$canonicalMatchCount = @($pairs | Where-Object { $_.CanonicalResultSha256Matches -eq $true }).Count
$canonicalDifferenceCount = @($pairs | Where-Object { $_.CanonicalResultComparable -and $_.CanonicalResultSha256Matches -eq $false }).Count
$allComponentToleranceChecksComparable = $geometryToleranceComparableCount -eq $pairs.Count -and $recognitionToleranceComparableCount -eq $pairs.Count -and $tokenDecisionComparableCount -eq $pairs.Count
$allComponentToleranceChecksPass = $allComponentToleranceChecksComparable -and
    @($pairs | Where-Object GeometryWithinTolerance).Count -eq $pairs.Count -and
    @($pairs | Where-Object RecognitionWithinTolerance).Count -eq $pairs.Count -and
    $tokenDecisionMatchCount -eq $pairs.Count
$canonicalStatus = if ($canonicalComparableCount -eq 0) { 'not-comparable-character-set-metadata' } elseif ($canonicalComparableCount -lt $pairs.Count) { 'partial-comparison' } elseif ($canonicalDifferenceCount -gt 0 -and $allComponentToleranceChecksPass) { 'bitwise-hash-differences-within-tolerance' } elseif ($canonicalDifferenceCount -gt 0) { 'comparable-result-differences' } else { 'full-canonical-result-parity' }
$summary = [pscustomobject]@{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow
    Status = $canonicalStatus
    Scope = 'Seven PP-OCR v4/v5/v6 complete DET-to-merge pipelines on three fixed images; ONNX Runtime CPU versus OpenVINO CPU.'
    PairCount = $pairs.Count
    AllComponentToleranceChecksComparable = $allComponentToleranceChecksComparable
    AllComponentToleranceChecksPass = $allComponentToleranceChecksPass
    ImageModelIdentityMatches = $pairs.Count - $identityMismatches.Count
    RegionAndRecognizedCountMatches = $pairs.Count - $regionMismatches.Count
    OrderedTextFingerprintMatches = $pairs.Count - $textMismatches.Count
    GeometryFingerprintComparablePairs = $geometryComparableCount
    GeometryFingerprintMatches = $geometryMatchCount
    RecognitionFingerprintComparablePairs = $recognitionComparableCount
    RecognitionFingerprintMatches = $recognitionMatchCount
    CoordinateTolerancePixels = $CoordinateTolerancePixels
    ConfidenceTolerance = $ConfidenceTolerance
    DetectorScoreTolerance = $DetectorScoreTolerance
    TokenDecisionComparablePairs = $tokenDecisionComparableCount
    TokenDecisionMatches = $tokenDecisionMatchCount
    GeometryToleranceComparablePairs = $geometryToleranceComparableCount
    GeometryWithinTolerancePairs = @($pairs | Where-Object GeometryWithinTolerance).Count
    RecognitionToleranceComparablePairs = $recognitionToleranceComparableCount
    RecognitionWithinTolerancePairs = @($pairs | Where-Object RecognitionWithinTolerance).Count
    MaximumCoordinateAbsoluteDelta = if ($geometryToleranceComparableCount -eq 0) { $null } else { ($pairs | Measure-Object MaxCoordinateAbsoluteDelta -Maximum).Maximum }
    MaximumDetectorScoreAbsoluteDelta = if ($geometryToleranceComparableCount -eq 0) { $null } else { ($pairs | Measure-Object MaxDetectorScoreAbsoluteDelta -Maximum).Maximum }
    MaximumRecognitionConfidenceAbsoluteDelta = if ($recognitionToleranceComparableCount -eq 0) { $null } else { ($pairs | Measure-Object MaxRecognitionConfidenceAbsoluteDelta -Maximum).Maximum }
    MaximumTokenConfidenceAbsoluteDelta = if ($recognitionToleranceComparableCount -eq 0) { $null } else { ($pairs | Measure-Object MaxTokenConfidenceAbsoluteDelta -Maximum).Maximum }
    CharacterSetMetadataComparablePairs = $canonicalComparableCount
    CanonicalResultFingerprintMatches = $canonicalMatchCount
    CanonicalResultFingerprintDifferences = $canonicalDifferenceCount
    CanonicalResultFingerprintNotComparable = $pairs.Count - $canonicalComparableCount
    Inputs = @($pairs | Group-Object ImageFileName | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{ ImageFileName = $_.Name; Sha256 = $_.Group[0].ImageSha256 }
    })
    Backends = @(
        [pscustomobject]@{
            Name = $ort.Backend
            OperatingSystem = $ort.OperatingSystem
            ProcessArchitecture = $ort.ProcessArchitecture
            CpuModel = $ort.CpuModel
            LogicalProcessorCount = $ort.LogicalProcessorCount
            DotNetRuntime = $ort.DotNetRuntime
            RuntimeVersion = $ort.OnnxRuntimeNativeVersion
            ProviderAssemblyVersion = $ort.OnnxRuntimeProviderAssemblyVersion
            SourceCommit = $ort.SourceCommit
            SourceTreeDirty = $ort.SourceTreeDirty
            TestSourceSha256 = $ort.TestSourceSha256
            WarmupRuns = $ort.WarmupRuns
            MeasuredRunsPerModelImage = $ort.MeasuredRunsPerModelImage
        },
        [pscustomobject]@{
            Name = $openVino.Backend
            OperatingSystem = $openVino.OperatingSystem
            ProcessArchitecture = $openVino.ProcessArchitecture
            CpuModel = $openVino.CpuModel
            LogicalProcessorCount = $openVino.LogicalProcessorCount
            DotNetRuntime = $openVino.DotNetRuntime
            RuntimeVersion = $openVino.OpenVinoRuntimeVersion
            ProviderAssemblyVersion = $openVino.OpenVinoProviderAssemblyVersion
            SourceCommit = $openVino.SourceCommit
            SourceTreeDirty = $openVino.SourceTreeDirty
            TestSourceSha256 = $openVino.TestSourceSha256
            WarmupRuns = $openVino.WarmupRuns
            MeasuredRunsPerModelImage = $openVino.MeasuredRunsPerModelImage
        }
    )
    Boundary = if ($canonicalComparableCount -eq 0) { 'Single-run functional integration evidence only. Inputs and Session creation are outside the recorded OcrPipeline.Run timing. Demo images have no transcription ground truth. Text and region-count parity do not establish accuracy. Canonical OcrResult hashes are not comparable because the reports do not contain matching character-set identity metadata; hash differences must not be interpreted as output drift. RTX 2060 is host hardware only: both tested backends ran on CPU. Not a steady-state benchmark, GPU test, or full support claim.' } else { 'Single-run functional integration evidence only. Inputs and Session creation are outside the recorded OcrPipeline.Run timing. Demo images have no transcription ground truth. Text and region-count parity do not establish accuracy. Canonical result hashes are compared only when source/model/dictionary and character-set identity/version/hash metadata match. RTX 2060 is host hardware only: both tested backends ran on CPU. Not a steady-state benchmark, GPU test, or full support claim.' }
    Models = $models
    Pairs = @($pairs | Sort-Object Variant, ImageFileName)
}
$geometryWithinToleranceSummary = if ($geometryToleranceComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f @($summary.GeometryWithinTolerancePairs, $geometryToleranceComparableCount) }
$recognitionWithinToleranceSummary = if ($recognitionToleranceComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f @($summary.RecognitionWithinTolerancePairs, $recognitionToleranceComparableCount) }
$tokenDecisionParitySummary = if ($tokenDecisionComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f $tokenDecisionMatchCount, $tokenDecisionComparableCount }
$geometryFingerprintSummary = if ($geometryComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f $geometryMatchCount, $geometryComparableCount }
$recognitionFingerprintSummary = if ($recognitionComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f $recognitionMatchCount, $recognitionComparableCount }
$canonicalFingerprintSummary = if ($canonicalComparableCount -eq 0) { 'n/a (0 comparable)' } else { '{0}/{1}' -f $canonicalMatchCount, $canonicalComparableCount }
$maximumCoordinateDelta = if ($geometryToleranceComparableCount -eq 0) { 'n/a' } else { $summary.MaximumCoordinateAbsoluteDelta }
$maximumDetectorScoreDelta = if ($geometryToleranceComparableCount -eq 0) { 'n/a' } else { $summary.MaximumDetectorScoreAbsoluteDelta }
$maximumRecognitionConfidenceDelta = if ($recognitionToleranceComparableCount -eq 0) { 'n/a' } else { $summary.MaximumRecognitionConfidenceAbsoluteDelta }
$maximumTokenConfidenceDelta = if ($recognitionToleranceComparableCount -eq 0) { 'n/a' } else { $summary.MaximumTokenConfidenceAbsoluteDelta }

$jsonPath = [IO.Path]::GetFullPath($OutputJsonPath)
$markdownPath = [IO.Path]::GetFullPath($OutputMarkdownPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownPath) | Out-Null
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$builder = [Text.StringBuilder]::new()
[void]$builder.AppendLine('# PP-OCR core seven-model three-image CPU backend check (2026-10-23)')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Result')
[void]$builder.AppendLine()
[void]$builder.AppendLine("All 21 model/image pairs completed the full ``det → crop → cls/orientation → rec → merge`` pipeline on both ONNX Runtime CPU and OpenVINO CPU. The image and model artifact identities match for ``$($pairs.Count - $identityMismatches.Count)/$($pairs.Count)``; detected and recognized region counts match for ``$($pairs.Count - $regionMismatches.Count)/$($pairs.Count)``; ordered text SHA-256 fingerprints match for ``$($pairs.Count - $textMismatches.Count)/$($pairs.Count)``.")
if ($canonicalComparableCount -eq 0) {
    [void]$builder.AppendLine("The canonical ``OcrResult`` SHA-256 comparison is **not valid for these reports**: matching character-set ID/version/hash metadata is absent. Raw hash values may differ because the hash includes that metadata; they are not counted as result differences, and canonical result parity remains unmeasured.")
} else {
    if ($canonicalStatus -eq 'bitwise-hash-differences-within-tolerance') {
        [void]$builder.AppendLine("Canonical result hashes are comparable for ``$canonicalComparableCount/$($pairs.Count)`` pairs; bit-exact hashes match for ``$canonicalMatchCount/$canonicalComparableCount``. All geometry/confidence tolerance checks and CTC token decisions pass, so the status is ``$canonicalStatus`` rather than a semantic output mismatch.")
    } else {
        [void]$builder.AppendLine("Canonical result hashes are comparable for ``$canonicalComparableCount/$($pairs.Count)`` pairs and match for ``$canonicalMatchCount``. Only comparable non-matching hashes are counted as result differences.")
    }
}
[void]$builder.AppendLine()
[void]$builder.AppendLine("Exact component fingerprints: geometry ``$geometryFingerprintSummary`` match; recognition (confidence, character-set provenance and CTC token trace) ``$recognitionFingerprintSummary`` match. Exact CTC token decisions: ``$tokenDecisionParitySummary``. Numeric tolerances are ``$CoordinateTolerancePixels`` source pixels for coordinates, ``$DetectorScoreTolerance`` for detector scores and ``$ConfidenceTolerance`` for recognition/token confidence; geometry is within tolerance for ``$geometryWithinToleranceSummary`` and recognition for ``$recognitionWithinToleranceSummary``. Maximum absolute deltas: coordinates ``$maximumCoordinateDelta``, detector score ``$maximumDetectorScoreDelta``, recognition confidence ``$maximumRecognitionConfidenceDelta``, token confidence ``$maximumTokenConfidenceDelta``.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Model | demo_1 regions | demo_2 regions | demo_3 regions | Total per backend | Text | Geometry tolerance | Token decisions | Recognition tolerance | Canonical SHA |')
[void]$builder.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($model in $models) {
    $counts = @($model.RegionsByImage | ForEach-Object RegionCount)
    $geometryTolerance = if ($model.GeometryToleranceComparableCount -eq 0) { 'n/a' } else { '{0}/{1}' -f $model.GeometryWithinToleranceCount, $model.GeometryToleranceComparableCount }
    $tokenParity = if ($model.TokenDecisionComparableCount -eq 0) { 'n/a' } else { '{0}/{1}' -f $model.TokenDecisionParityCount, $model.TokenDecisionComparableCount }
    $recognitionTolerance = if ($model.RecognitionToleranceComparableCount -eq 0) { 'n/a' } else { '{0}/{1}' -f $model.RecognitionWithinToleranceCount, $model.RecognitionToleranceComparableCount }
    $canonicalParity = if ($model.CanonicalResultComparableCount -eq 0) { 'n/a' } else { '{0}/{1}' -f $model.CanonicalResultParityCount, $model.CanonicalResultComparableCount }
    [void]$builder.AppendLine("| $($model.Variant) | $($counts[0]) | $($counts[1]) | $($counts[2]) | $($model.TotalRegionsPerBackend) | $($model.TextParityCount)/3 | $geometryTolerance | $tokenParity | $recognitionTolerance | $canonicalParity |")
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Device and runtime')
[void]$builder.AppendLine()
[void]$builder.AppendLine("Host OS: ``$($ort.OperatingSystem)``; CPU: ``$($ort.CpuModel)`` (``$($ort.LogicalProcessorCount)`` logical processors); process architecture: ``$($ort.ProcessArchitecture)``; .NET: ``$($ort.DotNetRuntime)``. Both runs used CPU execution only. ``$($ort.Backend)`` native runtime: ``$($ort.OnnxRuntimeNativeVersion)``; ``$($openVino.Backend)`` runtime: ``$($openVino.OpenVinoRuntimeVersion)``. No GPU execution, frequency tuning, or GPU performance was tested.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('Each model/image pair was run once with zero warmups. Timing starts at `OcrPipeline.Run` after image input preparation and backend Session creation; reported elapsed values are diagnostic only and must not be used as a latency ranking or steady-state benchmark.')
[void]$builder.AppendLine()
[void]$builder.AppendLine("Source commits: ORT ``$($ort.SourceCommit)``; OpenVINO ``$($openVino.SourceCommit)``. Source-tree dirty: ORT ``$($ort.SourceTreeDirty)``; OpenVINO ``$($openVino.SourceTreeDirty)``. Test-source SHA-256, input and model artifact SHA-256 values, and per-pair component summaries are in the machine-readable JSON evidence.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Parity boundary')
[void]$builder.AppendLine()
[void]$builder.AppendLine('The text fingerprint is SHA-256 over the ordered recognized strings joined with newline separators. Matching text and region counts do not prove polygon, confidence, orientation, or token equality. The historical Linux reports used backend-specific character-set IDs (`external.<model>` for ORT and `external.openvino.<model>` for OpenVINO), which participate in the canonical result SHA; those reports also lack matching character-set provenance fields and cannot be compared canonically. The new Windows reports record matching character-set ID/version/SHA metadata, so their canonical result hashes are comparable. The demo images have no manually verified transcription annotations, so this run does not calculate CER/WER, detection recall, or accuracy.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('A canonical result SHA is a bit-exact fingerprint, not a tolerance-based parity verdict: tiny backend floating-point differences change the hash. Read it together with the explicit geometry/confidence tolerances and token-decision comparison above. In particular, a hash mismatch alongside all tolerance checks passing means numerically close component parity, not identical binary outputs.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('The historical Linux RTX 2060 pair is a separate run: its three image SHA-256 values differ from this Windows set, and its per-region character-set identity/version/hash fields are absent. The comparator therefore marks those canonical hashes not comparable; do not interpret them as an ORT-versus-OpenVINO output difference or as a cross-device parity result.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Reproduction')
[void]$builder.AppendLine()
[void]$builder.AppendLine('On a machine with the listed models and images, set `DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL=1` and/or `DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1`, configure `DEPLOYSHARP_PADDLEOCR_ROOT` and the backend-specific `*_IMAGES` semicolon-delimited list, then run the corresponding `PaddleOcrAllCorePipeline*IntegrationTests` opt-in test. Configure `DEPLOYSHARP_PADDLEOCR_ORT_REPORT` or `DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT` to emit raw evidence JSON. Compare a new report pair with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrCorePipelineReports.ps1`.')
[void]$builder.AppendLine()
$pairReportName = [IO.Path]::GetFileName($jsonPath)
$pairReportReference = [Uri]::EscapeDataString($pairReportName)
$ortReportLink = [Uri]::EscapeDataString($OrtReportReference)
$openVinoReportLink = [Uri]::EscapeDataString($OpenVinoReportReference)
[void]$builder.AppendLine("Machine-readable pair report: [$pairReportName]($pairReportReference). Source reports: [ORT]($ortReportLink) and [OpenVINO]($openVinoReportLink).")
Set-Content -LiteralPath $markdownPath -Value $builder.ToString().TrimEnd() -Encoding utf8

Write-Host "PAIR_COUNT=$($pairs.Count)"
Write-Host "STATUS=$canonicalStatus"
Write-Host "TEXT_PARITY=$(@($pairs | Where-Object TextSha256Matches).Count)/$($pairs.Count)"
Write-Host "CANONICAL_RESULT_COMPARABLE=$canonicalComparableCount/$($pairs.Count)"
Write-Host "CANONICAL_RESULT_PARITY=$canonicalMatchCount/$canonicalComparableCount"
Write-Host "GEOMETRY_PARITY=$geometryMatchCount/$geometryComparableCount"
Write-Host "RECOGNITION_PARITY=$recognitionMatchCount/$recognitionComparableCount"
Write-Host "GEOMETRY_WITHIN_TOLERANCE=$geometryWithinToleranceSummary"
Write-Host "RECOGNITION_WITHIN_TOLERANCE=$recognitionWithinToleranceSummary"
Write-Host "TOKEN_DECISIONS_PARITY=$tokenDecisionParitySummary"
Write-Host "OUTPUT_JSON=$jsonPath"
Write-Host "OUTPUT_MARKDOWN=$markdownPath"
