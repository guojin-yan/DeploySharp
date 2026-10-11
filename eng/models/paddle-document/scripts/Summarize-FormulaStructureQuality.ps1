[CmdletBinding()]
param(
    [string]$InputRoot,
    [string]$OutputJson,
    [string]$OutputMarkdown
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($InputRoot)) { $InputRoot = Join-Path $scriptRoot '..\verification' }
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $scriptRoot '..\verification\formula-realformula-structure-quality-20261011.json' }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $scriptRoot '..\verification\formula-realformula-structure-quality-20261011.md' }

function Get-FormulaCommands([string]$Text) {
    if ($null -eq $Text) { return @() }
    return @([regex]::Matches($Text, '\\(?:[A-Za-z]+|[^A-Za-z\s])') | ForEach-Object Value)
}

function Test-BalancedDelimiters([string]$Text) {
    if ($null -eq $Text) { return $false }
    $pairs = @{ ')' = '('; ']' = '['; '}' = '{' }
    $open = [System.Collections.Generic.HashSet[char]]::new([char[]]'([{')
    $stack = [System.Collections.Generic.List[char]]::new()
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $ch = $Text[$i]
        if ($ch -in @('{','}','[',']','(',')') -and $i -gt 0 -and $Text[$i - 1] -eq '\') { continue }
        if ($open.Contains($ch)) { $stack.Add($ch); continue }
        if ($pairs.ContainsKey([string]$ch)) {
            if ($stack.Count -eq 0 -or $stack[$stack.Count - 1] -ne $pairs[[string]$ch]) { return $false }
            $stack.RemoveAt($stack.Count - 1)
        }
    }
    return $stack.Count -eq 0
}

function Test-BalancedEnvironments([string]$Text) {
    if ($null -eq $Text) { return $false }
    $rawCount = @([regex]::Matches($Text, '\\(?:begin|end)\b')).Count
    $stack = [System.Collections.Generic.List[string]]::new()
    $matchedCount = 0
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

function Get-CommandCount([string]$Text) {
    return @(Get-FormulaCommands $Text).Count
}

function Get-LengthBucket([int]$Length) {
    if ($Length -lt 64) { return '<64' }
    if ($Length -lt 128) { return '64-127' }
    if ($Length -lt 256) { return '128-255' }
    return '>=256'
}

function New-Accumulator {
    return [ordered]@{ sampleCount = 0; exactMatches = 0; eosCount = 0; predictionBalanced = 0; predictionBalancedEnvironments = 0; cerSum = 0.0; commandCountSum = 0.0 }
}

$fileNames = @(
    'formula-realformula-pp-formulanet-plus-s-ort-20261007.json',
    'formula-realformula-pp-formulanet-plus-m-ort-20261007.json',
    'formula-realformula-pp-formulanet-plus-l-ort-20261007.json',
    'formula-realformula-pp-formulanet-s-ort-20261007.json',
    'formula-realformula-pp-formulanet-l-ort-20261007.json',
    'formula-realformula-unimernet-ort-20261007.json'
)
$modelResults = @()
foreach ($fileName in $fileNames) {
    $path = Join-Path $InputRoot $fileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing formula report: $path" }
    $source = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $model = $source.modelResults[0]
    $rows = @($model.results)
    $overall = New-Accumulator
    $buckets = [ordered]@{}
    foreach ($row in $rows) {
        $prediction = [string]$row.prediction
        $bucketName = Get-LengthBucket ([int]$row.referenceLength)
        if (-not $buckets.Contains($bucketName)) { $buckets[$bucketName] = New-Accumulator }
        $bucket = $buckets[$bucketName]
        $predBalanced = Test-BalancedDelimiters $prediction
        $predBalancedEnvironments = Test-BalancedEnvironments $prediction
        $commandCount = Get-CommandCount $prediction
        foreach ($acc in @($overall, $bucket)) {
            $acc.sampleCount++
            if ([bool]$row.normalizedExactMatch) { $acc.exactMatches++ }
            if ([bool]$row.reachedEndOfSequence) { $acc.eosCount++ }
            if ($predBalanced) { $acc.predictionBalanced++ }
            if ($predBalancedEnvironments) { $acc.predictionBalancedEnvironments++ }
            $acc.cerSum += [double]$row.normalizedCharErrorRate
            $acc.commandCountSum += $commandCount
        }
    }
    function Convert-Accumulator($acc) {
        [ordered]@{
            sampleCount = $acc.sampleCount
            exactMatches = $acc.exactMatches
            exactMatchRate = if ($acc.sampleCount) { $acc.exactMatches / [double]$acc.sampleCount } else { 0.0 }
            eosCount = $acc.eosCount
            eosRate = if ($acc.sampleCount) { $acc.eosCount / [double]$acc.sampleCount } else { 0.0 }
            predictionBalancedCount = $acc.predictionBalanced
            predictionBalancedRate = if ($acc.sampleCount) { $acc.predictionBalanced / [double]$acc.sampleCount } else { 0.0 }
            predictionBalancedEnvironmentCount = $acc.predictionBalancedEnvironments
            predictionBalancedEnvironmentRate = if ($acc.sampleCount) { $acc.predictionBalancedEnvironments / [double]$acc.sampleCount } else { 0.0 }
            meanCharacterCer = if ($acc.sampleCount) { $acc.cerSum / [double]$acc.sampleCount } else { 0.0 }
            meanPredictionCommandCount = if ($acc.sampleCount) { $acc.commandCountSum / [double]$acc.sampleCount } else { 0.0 }
        }
    }
    $bucketOutput = [ordered]@{}
    foreach ($bucketName in $buckets.Keys) { $bucketOutput[$bucketName] = Convert-Accumulator $buckets[$bucketName] }
    $modelResults += [ordered]@{
        model = [string]$model.model
        backend = [string]$model.backend
        sourceReport = $fileName
        datasetSampleCount = [int]$model.sampleCount
        overall = Convert-Accumulator $overall
        referenceLengthBuckets = $bucketOutput
    }
}

$report = [ordered]@{
    schemaVersion = 'deploysharp-realformula-structure-quality-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    dataset = [ordered]@{ name = 'MathNet realFormula'; version = 'Zenodo 11296815 v1'; license = 'CC-BY-4.0'; sampleCount = 121; sourceReports = $fileNames }
    metrics = [ordered]@{
        balancedDelimiters = 'Character-level balanced (), [] and {} diagnostic; escaped literal delimiters are ignored.'
        balancedEnvironments = 'Stack-based shallow \\begin{environment}/\\end{environment} pairing diagnostic; malformed environment tags are treated as unbalanced.'
        predictionCommandCount = 'Mean count of LaTeX command tokens in the generated string; reference LaTeX is intentionally not embedded in the checked-in per-sample reports, so command precision/recall is not fabricated.'
        lengthBuckets = '<64, 64-127, 128-255 and >=256 normalized reference characters.'
    }
    models = $modelResults
    boundary = 'Derived from the checked-in ORT CPU prediction reports. These structural diagnostics do not establish mathematical equivalence, rendered-image correctness, held-out accuracy, or any other backend quality.'
}
$fullOutput = [IO.Path]::GetFullPath($OutputJson)
$fullMarkdown = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullOutput)) | Out-Null
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullMarkdown)) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fullOutput -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Formula realFormula structural diagnostics (2026-10-11)')
$lines.Add('')
$lines.Add('This report is derived from the six checked-in MathNet realFormula v1 ORT CPU prediction files. It adds generated-output delimiter/environment balance, generated LaTeX command counts and reference-length strata to the existing exact/CER/EOS metrics. These are diagnostics, not mathematical semantic accuracy.')
$lines.Add('')
$lines.Add('| Model | Exact | Mean CER | EOS | Delimiters balanced | Environments balanced | Mean prediction command count |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|')
foreach ($item in $modelResults) {
    $o = $item.overall
    $lines.Add("| ``$($item.model)`` | $($o.exactMatches)/$($o.sampleCount) | $([math]::Round(100*$o.meanCharacterCer,2))% | $($o.eosCount)/$($o.sampleCount) | $($o.predictionBalancedCount)/$($o.sampleCount) | $($o.predictionBalancedEnvironmentCount)/$($o.sampleCount) | $([math]::Round($o.meanPredictionCommandCount,2)) |")
}
$lines.Add('')
$lines.Add('## Length strata')
$lines.Add('')
$lines.Add('| Model | Bucket | Samples | Exact | Mean CER | EOS | Delimiters balanced | Environments balanced | Prediction command count |')
$lines.Add('|---|---|---:|---:|---:|---:|---:|---:|---:|')
foreach ($item in $modelResults) {
    foreach ($bucketName in $item.referenceLengthBuckets.Keys) {
        $b = $item.referenceLengthBuckets[$bucketName]
        $lines.Add("| ``$($item.model)`` | $bucketName | $($b.sampleCount) | $($b.exactMatches) | $([math]::Round(100*$b.meanCharacterCer,2))% | $($b.eosCount) | $($b.predictionBalancedCount) | $($b.predictionBalancedEnvironmentCount) | $([math]::Round($b.meanPredictionCommandCount,2)) |")
    }
}
$lines.Add('')
$lines.Add('## Interpretation boundary')
$lines.Add('')
$lines.Add('- Balanced delimiters, balanced environments and generated-command counts are shallow string diagnostics; they do not parse TeX or prove rendered mathematical equivalence. Reference command precision/recall is intentionally not inferred because the checked-in per-sample records contain reference hashes, not raw labels.')
$lines.Add('- The input reports are ORT CPU only. No OpenVINO, OpenCV DNN or TensorRT quality claim is added by this derived report.')
$lines.Add('- The dataset is externally sourced and training overlap is unknown. Exact/CER values remain diagnostic rather than a release-quality gate.')
$lines | Set-Content -LiteralPath $fullMarkdown -Encoding UTF8
Write-Output "Wrote $fullOutput and $fullMarkdown"
