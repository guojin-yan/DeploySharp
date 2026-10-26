[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string[]]$OfficialReportPaths,
    [Parameter(Mandatory)][string]$ManagedReportPath,
    [Parameter(Mandatory)][string]$OutputJsonPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-Json([string]$Path) {
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 50
}

function Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Equal($Expected, $Actual, [string]$Field) {
    if (($Expected | ConvertTo-Json -Depth 30 -Compress) -cne ($Actual | ConvertTo-Json -Depth 30 -Compress)) {
        throw "Incomparable PaddleOCR-VL official reference: $Field"
    }
}

$manifestFile = (Resolve-Path -LiteralPath $ManifestPath).Path
$managedFile = (Resolve-Path -LiteralPath $ManagedReportPath).Path
$officialFiles = @($OfficialReportPaths | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
$outputFile = [IO.Path]::GetFullPath($OutputJsonPath)
$inputFiles = @($manifestFile, $managedFile) + $officialFiles
if ($inputFiles.Count -ne @($inputFiles | Sort-Object -Unique).Count) {
    throw 'Manifest and reports must be distinct files.'
}
if ($inputFiles | Where-Object { $_ -ieq $outputFile }) {
    throw 'Output cannot overwrite an input file.'
}

$manifest = Read-Json $manifestFile
$managed = Read-Json $managedFile
$manifestHash = Sha256 $manifestFile
Assert-Equal 'c5630abae1d940eafe0697512a0325494b02ab42' $manifest.checkpointRevision 'checkpoint revision'
Assert-Equal 'deploysharp-paddleocr-vl-realformula-quality-v1' $managed.schemaVersion 'managed schema'
Assert-Equal 'completed' $managed.status 'managed completion'
Assert-Equal 'paddleocr-vl/1.6' $managed.model.id 'managed model ID'
Assert-Equal $manifest.checkpointRevision $managed.model.checkpointRevision 'managed checkpoint'
Assert-Equal 'onnxruntime-cpu' $managed.environment.backend 'managed backend'
Assert-Equal 'FormulaRecognition' $managed.protocol.task 'managed task'
Assert-Equal 512 $managed.protocol.maximumNewTokens 'managed token budget'
Assert-Equal $managed.dataset.annotatedCount $managed.summary.completedCount 'managed corpus completion'
Assert-Equal $managed.summary.completedCount @($managed.results).Count 'managed row count'
Assert-Equal $true $managed.protocol.recordTokenIds 'managed token recording'

$expectedCases = @($manifest.cases)
if ($expectedCases.Count -eq 0) { throw 'The official sample manifest is empty.' }
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$rows = [System.Collections.Generic.List[object]]::new()
$reportRecords = [System.Collections.Generic.List[object]]::new()
$implementation = $null

foreach ($officialFile in $officialFiles) {
    $official = Read-Json $officialFile
    Assert-Equal 'reference-generation-complete' $official.status 'official completion'
    Assert-Equal $manifestHash $official.manifestSha256 'official manifest SHA'
    Assert-Equal $manifest.checkpointRevision $official.checkpoint.revision 'official checkpoint revision'
    Assert-Equal $true $official.checkpoint.weightVerified 'official weight verification'
    Assert-Equal '85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db' `
        $official.checkpoint.weightSha256 'official weight SHA'
    Assert-Equal 'cpu' $official.environment.device 'official device'
    Assert-Equal 'float32' $official.environment.dtype 'official precision'
    Assert-Equal 'pil' $official.protocol.processorBackend 'official processor'
    Assert-Equal 'sdpa' $official.protocol.attention 'official attention'
    Assert-Equal $true $official.protocol.requestedUseCache 'official KV cache'
    Assert-Equal $false $official.protocol.doSample 'official greedy generation'
    Assert-Equal 512 $official.protocol.maximumNewTokens 'official token budget'
    if ($null -eq $implementation) {
        $implementation = $official.implementation
    } else {
        foreach ($field in @('processorModuleSha256', 'imageProcessorModuleSha256', 'modelModuleSha256', 'transformersVersion')) {
            Assert-Equal $implementation.$field $official.implementation.$field "official implementation/$field"
        }
    }
    $reportRecords.Add([ordered]@{ sha256 = Sha256 $officialFile; completedCount = @($official.cases).Count })

    foreach ($case in @($official.cases)) {
        $expected = @($expectedCases | Where-Object id -CEQ $case.id)
        Assert-Equal 1 $expected.Count "manifest case/$($case.id)"
        if (-not $seen.Add([string]$case.id)) { throw "Duplicate official case: $($case.id)" }
        Assert-Equal 'formula' $case.task "task/$($case.id)"
        Assert-Equal $expected[0].task $case.task "manifest task/$($case.id)"
        Assert-Equal $expected[0].image $case.inputFile "manifest image/$($case.id)"
        Assert-Equal $expected[0].sha256 $case.inputSha256 "manifest source SHA/$($case.id)"
        Assert-Equal 'generated' $case.status "generation/$($case.id)"
        Assert-Equal 'eos' $case.stopReason "EOS/$($case.id)"
        $imageName = [IO.Path]::GetFileName([string]$case.inputFile)
        $matched = @($managed.results | Where-Object image -CEQ $imageName)
        Assert-Equal 1 $matched.Count "managed case/$imageName"
        $row = $matched[0]
        Assert-Equal $case.inputSha256 $row.imageSha256 "source SHA/$imageName"
        Assert-Equal 'EndOfSequence' $row.finishReason "managed EOS/$imageName"
        $officialTokens = @($case.generatedTokenIds)
        $managedTokens = @($row.generatedTokenIds)
        Assert-Equal $officialTokens.Count $row.generatedTokens "managed token count/$imageName"
        $firstDifference = $null
        for ($index = 0; $index -lt [Math]::Max($officialTokens.Count, $managedTokens.Count); $index++) {
            if ($index -ge $officialTokens.Count -or $index -ge $managedTokens.Count -or
                $officialTokens[$index] -ne $managedTokens[$index]) {
                $firstDifference = $index
                break
            }
        }
        $rows.Add([ordered]@{
            image = $imageName
            imageSha256 = $case.inputSha256
            officialPixelSha256 = $case.tensors.pixel_values.sha256
            generatedTokenCount = $officialTokens.Count
            tokenIdsExact = $null -eq $firstDifference
            firstTokenDifferenceIndex = $firstDifference
            textExact = $case.decoded -ceq $row.prediction
            annotationWhitespaceExact = [bool]$row.exactMatch
            annotationCharacterEdits = $row.characterEdits
        })
    }
}

Assert-Equal $expectedCases.Count $seen.Count 'complete official sample selection'
$rowArray = @($rows.ToArray())
$tokenMatches = @($rowArray | Where-Object tokenIdsExact).Count
$textMatches = @($rowArray | Where-Object textExact).Count
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-official-realformula-comparison-v1'
    status = $(if ($tokenMatches -eq $rowArray.Count -and $textMatches -eq $rowArray.Count) { 'exact-generation-parity' } else { 'observed-differences' })
    manifestSha256 = $manifestHash
    datasetManifestSha256 = $managed.dataset.manifestSha256
    annotationCsvSha256 = $managed.dataset.csvSha256
    checkpointRevision = $manifest.checkpointRevision
    officialWeightSha256 = '85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db'
    officialImplementation = [ordered]@{
        transformersVersion = $implementation.transformersVersion
        processorModuleSha256 = $implementation.processorModuleSha256
        imageProcessorModuleSha256 = $implementation.imageProcessorModuleSha256
        modelModuleSha256 = $implementation.modelModuleSha256
    }
    officialReports = @($reportRecords.ToArray())
    managedReportSha256 = Sha256 $managedFile
    managedSourceCommit = $managed.environment.sourceCommit
    managedVisualAssemblySha256 = $managed.environment.visualAssemblySha256
    managedOpenCvAssemblySha256 = $managed.environment.openCvAssemblySha256
    managedTestAssemblySha256 = $managed.environment.testAssemblySha256
    comparedCount = $rowArray.Count
    tokenIdsExactCount = $tokenMatches
    textExactCount = $textMatches
    annotationWhitespaceExactCount = @($rowArray | Where-Object annotationWhitespaceExact).Count
    rows = $rowArray
    boundary = 'This checks five selected images and exact generated tokens/text against the pinned official Python weights. It does not compare intermediate tensors, prove formula semantic accuracy, validate the other 116 corpus images against official weights, or establish controlled latency.'
}

$null = New-Item -ItemType Directory -Force -Path (Split-Path $outputFile)
$summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $outputFile -Encoding utf8
Write-Output "PADDLEOCR_VL_OFFICIAL_COMPARISON_STATUS=$($summary.status)"
Write-Output "PADDLEOCR_VL_OFFICIAL_COMPARISON_JSON=$outputFile"
