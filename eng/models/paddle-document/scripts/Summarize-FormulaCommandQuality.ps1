[CmdletBinding()]
param(
    [string]$InputRoot,
    [string]$DatasetRoot,
    [string]$OutputJson,
    [string]$OutputMarkdown
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($InputRoot)) { $InputRoot = Join-Path $scriptRoot '..\verification' }
if ([string]::IsNullOrWhiteSpace($DatasetRoot)) { $DatasetRoot = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public' }
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $scriptRoot '..\verification\formula-realformula-command-quality-20261011.json' }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $scriptRoot '..\verification\formula-realformula-command-quality-20261011.md' }

function Get-CommandBag([string]$Text) {
    $bag = @{}
    if ($null -eq $Text) { return ,$bag }
    foreach ($match in [regex]::Matches($Text, '\\(?:[A-Za-z]+|[^A-Za-z\s])')) {
        $token = [string]$match.Value
        if ($bag.ContainsKey($token)) { $bag[$token]++ } else { $bag[$token] = 1 }
    }
    return ,$bag
}

function Get-CommandTotal([hashtable]$Bag) {
    if ($Bag.Count -eq 0) { return 0 }
    return [int](($Bag.Values | Measure-Object -Sum).Sum)
}

function Test-BalancedEnvironments([string]$Text) {
    if ($null -eq $Text) { return $false }
    $rawCount = @([regex]::Matches($Text, '\\(?:begin|end)\b')).Count
    $matchedCount = 0
    $stack = [System.Collections.Generic.List[string]]::new()
    foreach ($match in [regex]::Matches($Text, '\\(begin|end)\s*\{([^{}]+)\}')) {
        $matchedCount++
        $kind = [string]$match.Groups[1].Value
        $environment = [string]$match.Groups[2].Value
        if ($kind -eq 'begin') {
            $stack.Add($environment)
            continue
        }
        if ($stack.Count -eq 0 -or $stack[$stack.Count - 1] -ne $environment) { return $false }
        $stack.RemoveAt($stack.Count - 1)
    }
    return $matchedCount -eq $rawCount -and $stack.Count -eq 0
}

function Get-CommandMetrics([string]$Reference, [string]$Prediction) {
    $referenceBag = Get-CommandBag $Reference
    $predictionBag = Get-CommandBag $Prediction
    $referenceCount = Get-CommandTotal $referenceBag
    $predictionCount = Get-CommandTotal $predictionBag
    $matched = 0
    foreach ($key in $referenceBag.Keys) {
        if ($predictionBag.ContainsKey($key)) { $matched += [math]::Min([int]$referenceBag[$key], [int]$predictionBag[$key]) }
    }
    $precision = if ($predictionCount -gt 0) { $matched / [double]$predictionCount } else { 0.0 }
    $recall = if ($referenceCount -gt 0) { $matched / [double]$referenceCount } else { 0.0 }
    $f1 = if (($precision + $recall) -gt 0) { 2.0 * $precision * $recall / ($precision + $recall) } else { 0.0 }
    return [ordered]@{
        referenceCommandCount = $referenceCount
        predictionCommandCount = $predictionCount
        matchedCommandCount = $matched
        precision = $precision
        recall = $recall
        f1 = $f1
    }
}

function New-Average([double]$Sum, [int]$Count) {
    if ($Count -eq 0) { return 0.0 }
    return $Sum / [double]$Count
}

$manifestPath = Join-Path $DatasetRoot 'realFormula-manifest.jsonl'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Missing MathNet manifest: $manifestPath" }
$manifestByImage = @{}
foreach ($line in [IO.File]::ReadAllLines($manifestPath)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $entry = $line | ConvertFrom-Json
    $image = [string]$entry.Image
    if ([string]::IsNullOrWhiteSpace($image) -or [string]::IsNullOrWhiteSpace([string]$entry.ReferenceLatex)) { throw "Manifest row has no image/reference: $line" }
    if ($manifestByImage.ContainsKey($image)) { throw "Duplicate manifest image: $image" }
    $manifestByImage[$image] = $entry
}
$manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()

$fileNames = @(
    'formula-realformula-pp-formulanet-plus-s-ort-20261007.json',
    'formula-realformula-pp-formulanet-plus-m-ort-20261007.json',
    'formula-realformula-pp-formulanet-plus-l-ort-20261007.json',
    'formula-realformula-pp-formulanet-s-ort-20261007.json',
    'formula-realformula-pp-formulanet-l-ort-20261007.json',
    'formula-realformula-unimernet-ort-20261007.json'
)
$modelResults = [System.Collections.Generic.List[object]]::new()
foreach ($fileName in $fileNames) {
    $path = Join-Path $InputRoot $fileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing formula report: $path" }
    $source = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $model = $source.modelResults[0]
    $rows = @($model.results)
    if ($rows.Count -eq 0) { throw "No prediction rows in $fileName" }
    $sampleResults = [System.Collections.Generic.List[object]]::new()
    $referenceTotal = 0
    $predictionTotal = 0
    $matchedTotal = 0
    $precisionSum = 0.0
    $recallSum = 0.0
    $f1Sum = 0.0
    $expectedBalancedEnvironmentCount = 0
    $actualBalancedEnvironmentCount = 0
    foreach ($row in $rows) {
        $image = [string]$row.image
        if (-not $manifestByImage.ContainsKey($image)) { throw "Prediction image is missing from manifest: $image" }
        $entry = $manifestByImage[$image]
        if ([string]$entry.ImageSha256 -ne [string]$row.imageSha256) { throw "Image SHA mismatch for $image in $fileName" }
        $metrics = Get-CommandMetrics ([string]$entry.ReferenceLatex) ([string]$row.prediction)
        $expectedBalancedEnvironments = Test-BalancedEnvironments ([string]$entry.ReferenceLatex)
        $actualBalancedEnvironments = Test-BalancedEnvironments ([string]$row.prediction)
        if ($expectedBalancedEnvironments) { $expectedBalancedEnvironmentCount++ }
        if ($actualBalancedEnvironments) { $actualBalancedEnvironmentCount++ }
        $referenceTotal += [int]$metrics.referenceCommandCount
        $predictionTotal += [int]$metrics.predictionCommandCount
        $matchedTotal += [int]$metrics.matchedCommandCount
        $precisionSum += [double]$metrics.precision
        $recallSum += [double]$metrics.recall
        $f1Sum += [double]$metrics.f1
        $sampleResults.Add([ordered]@{
            image = $image
            imageSha256 = [string]$row.imageSha256
            referenceSha256 = [string]$row.referenceSha256
            referenceLength = [int]$row.referenceLength
            normalizedExactMatch = [bool]$row.normalizedExactMatch
            reachedEndOfSequence = [bool]$row.reachedEndOfSequence
            expectedBalancedEnvironments = $expectedBalancedEnvironments
            actualBalancedEnvironments = $actualBalancedEnvironments
            commandMetrics = $metrics
        })
    }
    $microPrecision = if ($predictionTotal -gt 0) { $matchedTotal / [double]$predictionTotal } else { 0.0 }
    $microRecall = if ($referenceTotal -gt 0) { $matchedTotal / [double]$referenceTotal } else { 0.0 }
    $microF1 = if (($microPrecision + $microRecall) -gt 0) { 2.0 * $microPrecision * $microRecall / ($microPrecision + $microRecall) } else { 0.0 }
    $modelResults.Add([ordered]@{
        model = [string]$model.model
        backend = [string]$model.backend
        sourceReport = $fileName
        sampleCount = $rows.Count
        referenceCommandTotal = $referenceTotal
        predictionCommandTotal = $predictionTotal
        matchedCommandTotal = $matchedTotal
        microPrecision = $microPrecision
        microRecall = $microRecall
        microF1 = $microF1
        macroPrecision = New-Average $precisionSum $rows.Count
        macroRecall = New-Average $recallSum $rows.Count
        macroF1 = New-Average $f1Sum $rows.Count
        expectedBalancedEnvironmentCount = $expectedBalancedEnvironmentCount
        expectedBalancedEnvironmentRate = $expectedBalancedEnvironmentCount / [double]$rows.Count
        actualBalancedEnvironmentCount = $actualBalancedEnvironmentCount
        actualBalancedEnvironmentRate = $actualBalancedEnvironmentCount / [double]$rows.Count
        samples = @($sampleResults)
    })
}

$report = [ordered]@{
    schemaVersion = 'deploysharp-realformula-command-quality-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    dataset = [ordered]@{
        name = 'MathNet realFormula'
        version = 'Zenodo 11296815 v1'
        license = 'CC-BY-4.0'
        manifestPath = $manifestPath
        manifestSha256 = $manifestSha256
        manifestSampleCount = $manifestByImage.Count
        sourceReports = $fileNames
    }
    metrics = [ordered]@{
        tokenDefinition = 'LaTeX command tokens matched by \\(?:[A-Za-z]+|single non-letter/non-whitespace character).'
        matching = 'Multiset overlap by exact command token; micro metrics aggregate token counts and macro metrics average per-sample precision/recall/F1.'
        environmentBalance = 'Shallow stack-based pairing of \\begin{environment}/\\end{environment}; raw reference text is used for the diagnostic but is not copied into the report.'
        referencePrivacy = 'Raw reference LaTeX is not copied into the report; image and reference hashes remain the join evidence.'
    }
    models = @($modelResults)
    boundary = 'Command-token overlap and environment pairing are lexical/structural diagnostics. They do not establish TeX parsing, rendered-image equivalence, mathematical semantic equivalence, held-out accuracy or quality on another backend.'
}
$fullJson = [IO.Path]::GetFullPath($OutputJson)
$fullMarkdown = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullJson)) | Out-Null
$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $fullJson -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Formula realFormula command-token quality diagnostics (2026-10-11)')
$lines.Add('')
$lines.Add('This report joins the checked-in ORT CPU predictions to the locally cached MathNet realFormula v1 manifest by image SHA, then compares LaTeX command-token multisets and shallow environment pairing. Raw reference LaTeX is not copied into the report.')
$lines.Add('')
$lines.Add("- Manifest entries: **$($manifestByImage.Count)**; manifest SHA-256: ``$manifestSha256``")
$lines.Add('- The six model reports each contain 121 prediction rows with image SHA validation.')
$lines.Add('')
$lines.Add('| Model | Samples | Ref commands | Pred commands | Matched | Micro P | Micro R | Micro F1 | Macro F1 | Ref env balanced | Pred env balanced |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($item in $modelResults) {
    $p = [math]::Round(100 * [double]$item.microPrecision, 2)
    $r = [math]::Round(100 * [double]$item.microRecall, 2)
    $f = [math]::Round(100 * [double]$item.microF1, 2)
    $mf = [math]::Round(100 * [double]$item.macroF1, 2)
    $lines.Add("| ``$($item.model)`` | $($item.sampleCount) | $($item.referenceCommandTotal) | $($item.predictionCommandTotal) | $($item.matchedCommandTotal) | $p% | $r% | $f% | $mf% | $($item.expectedBalancedEnvironmentCount)/$($item.sampleCount) | $($item.actualBalancedEnvironmentCount)/$($item.sampleCount) |")
}
$lines.Add('')
$lines.Add('## Interpretation boundary')
$lines.Add('')
$lines.Add('- The score is exact lexical command-token overlap, and environment pairing is a shallow structural check; neither is a TeX parser or renderer score. Equivalent expressions written with different but valid LaTeX forms can score lower, while syntactically invalid output can still share tokens.')
$lines.Add('- Image SHA and manifest joins were validated for every model row; raw reference formulas remain outside Git and are not redistributed by this report.')
$lines.Add('- The source predictions are ORT CPU only. This report does not add OpenVINO, OpenCV DNN or TensorRT quality evidence, and it does not close the formula quality gate.')
$lines | Set-Content -LiteralPath $fullMarkdown -Encoding UTF8
Write-Output "Wrote $fullJson and $fullMarkdown"
