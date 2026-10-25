[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReferenceReportPath,
    [Parameter(Mandatory)][string]$CandidateReportPath,
    [Parameter(Mandatory)][string]$OutputJsonPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Same($Expected, $Actual, [string]$Field) {
    if (($Expected | ConvertTo-Json -Depth 20 -Compress) -cne ($Actual | ConvertTo-Json -Depth 20 -Compress)) {
        throw "Incomparable PaddleOCR-VL reports: $Field"
    }
}

function Read-Report([string]$Path) {
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 30
}

$referencePath = (Resolve-Path -LiteralPath $ReferenceReportPath).Path
$candidatePath = (Resolve-Path -LiteralPath $CandidateReportPath).Path
$outputPath = [IO.Path]::GetFullPath($OutputJsonPath)
if ($referencePath -ieq $candidatePath) { throw 'Comparison needs two distinct reports.' }
if ($outputPath -ieq $referencePath -or $outputPath -ieq $candidatePath) { throw 'Output cannot overwrite an input report.' }

$reference = Read-Report $referencePath
$candidate = Read-Report $candidatePath
Assert-Same 'deploysharp-paddleocr-vl-realformula-quality-v1' $reference.schemaVersion 'reference schema'
Assert-Same $reference.schemaVersion $candidate.schemaVersion 'schema'
Assert-Same 'completed' $reference.status 'reference completion'
Assert-Same 'completed' $candidate.status 'candidate completion'
if ($reference.environment.backend -ceq $candidate.environment.backend) {
    throw 'Comparison needs two distinct backends.'
}

foreach ($field in @('name', 'version', 'csvSha256', 'manifestSha256', 'annotatedCount', 'selectedCount', 'limit')) {
    Assert-Same $reference.dataset.$field $candidate.dataset.$field "dataset/$field"
}
Assert-Same @($reference.dataset.selectedImages) @($candidate.dataset.selectedImages) 'selected images'
$referenceOffset = $reference.dataset.PSObject.Properties['offset']
$candidateOffset = $candidate.dataset.PSObject.Properties['offset']
if (($null -eq $referenceOffset) -ne ($null -eq $candidateOffset)) {
    throw 'Incomparable PaddleOCR-VL reports: dataset/offset availability'
}
if ($null -ne $referenceOffset) {
    Assert-Same $referenceOffset.Value $candidateOffset.Value 'dataset/offset'
}
foreach ($field in @('id', 'checkpointRevision', 'tokenizerSha256', 'graphs')) {
    Assert-Same $reference.model.$field $candidate.model.$field "model/$field"
}
foreach ($field in @('task', 'maximumNewTokens', 'imageExifOrientation', 'normalization')) {
    Assert-Same $reference.protocol.$field $candidate.protocol.$field "protocol/$field"
}
foreach ($field in @('sourceCommit', 'visualAssemblySha256', 'openCvAssemblySha256', 'testAssemblySha256')) {
    Assert-Same $reference.environment.$field $candidate.environment.$field "environment/$field"
}

$referenceRows = @($reference.results)
$candidateRows = @($candidate.results)
if ($referenceRows.Count -eq 0) { throw 'A backend comparison requires at least one completed image.' }
$referenceTokenRecording = $reference.protocol.PSObject.Properties['recordTokenIds']
$candidateTokenRecording = $candidate.protocol.PSObject.Properties['recordTokenIds']
Assert-Same $(if ($null -eq $referenceTokenRecording) { $null } else { $referenceTokenRecording.Value }) `
    $(if ($null -eq $candidateTokenRecording) { $null } else { $candidateTokenRecording.Value }) 'protocol/recordTokenIds'
Assert-Same $reference.dataset.selectedCount $referenceRows.Count 'reference row count'
Assert-Same $candidate.dataset.selectedCount $candidateRows.Count 'candidate row count'
Assert-Same $referenceRows.Count $reference.summary.completedCount 'reference completed count'
Assert-Same $candidateRows.Count $candidate.summary.completedCount 'candidate completed count'
Assert-Same @($referenceRows | ForEach-Object image | Sort-Object -Unique).Count $referenceRows.Count 'reference unique images'
Assert-Same @($candidateRows | ForEach-Object image | Sort-Object -Unique).Count $candidateRows.Count 'candidate unique images'
$rows = @(
    for ($index = 0; $index -lt $referenceRows.Count; $index++) {
        $left = $referenceRows[$index]
        $right = $candidateRows[$index]
        foreach ($field in @('image', 'imageSha256', 'referenceLatex', 'referenceLength')) {
            Assert-Same $left.$field $right.$field "row $index/$field"
        }
        $leftTokenIds = $left.PSObject.Properties['generatedTokenIds']
        $rightTokenIds = $right.PSObject.Properties['generatedTokenIds']
        if (($null -eq $leftTokenIds) -ne ($null -eq $rightTokenIds)) {
            throw "Incomparable PaddleOCR-VL reports: row $index/token ID availability"
        }
        $tokenIdsAvailable = $null -ne $leftTokenIds
        $tokenIdsExact = $null
        $firstTokenDifference = $null
        if ($tokenIdsAvailable) {
            $referenceTokens = @($leftTokenIds.Value)
            $candidateTokens = @($rightTokenIds.Value)
            Assert-Same $left.generatedTokens $referenceTokens.Count "row $index/reference token count"
            Assert-Same $right.generatedTokens $candidateTokens.Count "row $index/candidate token count"
            $tokenIdsExact = ($referenceTokens | ConvertTo-Json -Compress) -ceq ($candidateTokens | ConvertTo-Json -Compress)
            if (-not $tokenIdsExact) {
                for ($tokenIndex = 0; $tokenIndex -lt [Math]::Max($referenceTokens.Count, $candidateTokens.Count); $tokenIndex++) {
                    if ($tokenIndex -ge $referenceTokens.Count -or $tokenIndex -ge $candidateTokens.Count -or
                        $referenceTokens[$tokenIndex] -ne $candidateTokens[$tokenIndex]) {
                        $firstTokenDifference = $tokenIndex
                        break
                    }
                }
            }
        }
        [ordered]@{
            image = $left.image
            imageSha256 = $left.imageSha256
            predictionExact = $left.prediction -ceq $right.prediction
            generatedTokenCountExact = $left.generatedTokens -eq $right.generatedTokens
            finishReasonExact = $left.finishReason -ceq $right.finishReason
            characterEditsExact = $left.characterEdits -eq $right.characterEdits
            tokenIdsAvailable = $tokenIdsAvailable
            tokenIdsExact = $tokenIdsExact
            firstTokenDifferenceIndex = $firstTokenDifference
            referenceTokenId = $(if ($null -ne $firstTokenDifference -and $firstTokenDifference -lt $referenceTokens.Count) { $referenceTokens[$firstTokenDifference] } else { $null })
            candidateTokenId = $(if ($null -ne $firstTokenDifference -and $firstTokenDifference -lt $candidateTokens.Count) { $candidateTokens[$firstTokenDifference] } else { $null })
            referenceCharacterEdits = $left.characterEdits
            candidateCharacterEdits = $right.characterEdits
        }
    }
)

$allMatched = @($rows | Where-Object {
    -not ($_.predictionExact -and $_.generatedTokenCountExact -and $_.finishReasonExact -and $_.characterEditsExact -and
        (-not $_.tokenIdsAvailable -or $_.tokenIdsExact))
}).Count -eq 0
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-realformula-backend-comparison-v1'
    status = $(if ($allMatched) { 'decision-parity' } else { 'observed-differences' })
    reference = [ordered]@{
        backend = $reference.environment.backend
        reportSha256 = (Get-FileHash -LiteralPath $referencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        completedCount = $reference.summary.completedCount
        endOfSequenceCount = $reference.summary.endOfSequenceCount
        corpusCharacterErrorRate = $reference.summary.corpusCharacterErrorRate
    }
    candidate = [ordered]@{
        backend = $candidate.environment.backend
        reportSha256 = (Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash.ToLowerInvariant()
        completedCount = $candidate.summary.completedCount
        endOfSequenceCount = $candidate.summary.endOfSequenceCount
        corpusCharacterErrorRate = $candidate.summary.corpusCharacterErrorRate
    }
    datasetManifestSha256 = $reference.dataset.manifestSha256
    annotationCsvSha256 = $reference.dataset.csvSha256
    sourceCommit = $reference.environment.sourceCommit
    visualAssemblySha256 = $reference.environment.visualAssemblySha256
    openCvAssemblySha256 = $reference.environment.openCvAssemblySha256
    testAssemblySha256 = $reference.environment.testAssemblySha256
    model = $reference.model
    comparedCount = $rows.Count
    predictionExactCount = @($rows | Where-Object predictionExact).Count
    generatedTokenCountExactCount = @($rows | Where-Object generatedTokenCountExact).Count
    finishReasonExactCount = @($rows | Where-Object finishReasonExact).Count
    characterEditsExactCount = @($rows | Where-Object characterEditsExact).Count
    tokenIdsAvailableCount = @($rows | Where-Object tokenIdsAvailable).Count
    tokenIdsExactCount = @($rows | Where-Object tokenIdsExact).Count
    rows = $rows
    boundary = 'This compares text, token count, finish state and character edits on the selected labelled images under one model and build. Individual token IDs are compared only when both external reports explicitly record them; intermediate tensors and logit margins are not compared. A subset is not full-corpus quality, semantic equivalence, controlled latency or general backend support.'
}

$null = New-Item -ItemType Directory -Force -Path (Split-Path $outputPath)
$summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "PADDLEOCR_VL_COMPARISON_STATUS=$($summary.status)"
Write-Output "PADDLEOCR_VL_COMPARISON_JSON=$outputPath"
