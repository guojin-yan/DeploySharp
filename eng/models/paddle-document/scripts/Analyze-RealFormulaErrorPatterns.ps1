param(
    [string]$DatasetRoot = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public',
    [string]$VerificationRoot = 'eng/models/paddle-document/verification',
    [string]$OutputJsonPath = 'eng/models/paddle-document/verification/formula-realformula-error-patterns-20261007.json',
    [string]$OutputMarkdownPath = 'eng/models/paddle-document/verification/formula-realformula-error-patterns-20261007.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$verificationRootFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $VerificationRoot))
$datasetRootFull = [IO.Path]::GetFullPath($DatasetRoot)
$manifestPath = Join-Path $datasetRootFull 'realFormula-manifest.jsonl'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Pinned realFormula manifest is missing: $manifestPath" }

$samplesByImage = @{}
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $sample = $line | ConvertFrom-Json
    $samplesByImage[$sample.Image] = $sample
}
if ($samplesByImage.Count -ne 121) { throw "Expected 121 manifest rows; found $($samplesByImage.Count)." }

$modelIds = @(
    'pp-formulanet-plus-s', 'pp-formulanet-plus-m', 'pp-formulanet-plus-l',
    'pp-formulanet-s', 'pp-formulanet-l', 'unimernet'
)
$tokenPattern = '\\[A-Za-z]+|\\[^A-Za-z]|[A-Za-z0-9]|[^\s]'
$models = [System.Collections.Generic.List[object]]::new()

function Remove-SelectedFontWrappers([string]$value) {
    $pattern = '\\(mathrm|mathbf|boldsymbol|mathbb|mathcal|mathfrak|mathscr)\s*\{([^{}]*)\}'
    do {
        $previous = $value
        $value = [regex]::Replace($value, $pattern, '$2')
    } while ($value -ne $previous)
    return [regex]::Replace($value, '\s+', '')
}

function Get-LevenshteinDistance([string]$expected, [string]$actual) {
    if ($expected.Length -lt $actual.Length) {
        $temporary = $expected
        $expected = $actual
        $actual = $temporary
    }
    $previous = [int[]]::new($actual.Length + 1)
    $current = [int[]]::new($actual.Length + 1)
    for ($column = 0; $column -le $actual.Length; $column++) { $previous[$column] = $column }
    for ($row = 1; $row -le $expected.Length; $row++) {
        $current[0] = $row
        for ($column = 1; $column -le $actual.Length; $column++) {
            $substitution = $previous[$column - 1] + [int]($expected[$row - 1] -cne $actual[$column - 1])
            $current[$column] = [Math]::Min([Math]::Min($previous[$column] + 1, $current[$column - 1] + 1), $substitution)
        }
        $temporary = $previous
        $previous = $current
        $current = $temporary
    }
    return $previous[$actual.Length]
}

foreach ($modelId in $modelIds) {
    $reportPath = Join-Path $verificationRootFull "formula-realformula-$modelId-ort-20261007.json"
    if (!(Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "Missing per-model inference report: $reportPath" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.modelResults.Count -ne 1 -or $report.modelResults[0].model -ne "paddle-formula/$modelId") {
        throw "Unexpected model identity in $reportPath"
    }

    $enriched = [System.Collections.Generic.List[object]]::new()
    foreach ($prediction in $report.modelResults[0].results) {
        if (!$samplesByImage.ContainsKey($prediction.image)) { throw "Prediction has no pinned label row: $($prediction.image)" }
        $reference = [string]$samplesByImage[$prediction.image].ReferenceLatex
        $referenceLength = [int]$prediction.referenceLength
        $predictionLength = [int]$prediction.predictionLength
        $editDistance = [int]$prediction.normalizedCharEditDistance
        $maxLength = [Math]::Max(1, [Math]::Max($referenceLength, $predictionLength))
        $normalizedReference = Remove-SelectedFontWrappers $reference
        $normalizedPrediction = Remove-SelectedFontWrappers ([string]$prediction.prediction)
        $styleEditDistance = Get-LevenshteinDistance $normalizedReference $normalizedPrediction
        $styleMaxLength = [Math]::Max(1, [Math]::Max($normalizedReference.Length, $normalizedPrediction.Length))
        $latexTokens = [regex]::Matches($reference, $tokenPattern).Count
        $hasArray = [regex]::IsMatch($reference, '\\begin\s*\{\s*(array|matrix|pmatrix|bmatrix|Bmatrix|vmatrix|Vmatrix|cases|aligned|gathered)\s*\}')
        $hasMultiline = [regex]::IsMatch($reference, '\\\\|\\begin\s*\{\s*(aligned|align|array|gathered|cases|matrix|pmatrix|bmatrix)\s*\}')
        $hasMathFont = [regex]::IsMatch($reference, '\\(boldsymbol|mathbf|mathbb|mathcal|mathfrak|mathscr)\b')
        $lengthClass = if ($latexTokens -le 40) { 'short (≤40 LaTeX tokens)' } elseif ($latexTokens -le 120) { 'medium (41–120 LaTeX tokens)' } else { 'long (>120 LaTeX tokens)' }
        $enriched.Add([pscustomobject]@{
            image = $prediction.image
            referenceLength = $referenceLength
            predictionLength = $predictionLength
            editDistance = $editDistance
            fontWrapperStrippedEditDistance = $styleEditDistance
            fontWrapperStrippedExact = ($normalizedReference -ceq $normalizedPrediction)
            fontWrapperStrippedReferenceLength = $normalizedReference.Length
            fontWrapperStrippedPredictionLength = $normalizedPrediction.Length
            exact = [bool]$prediction.normalizedExactMatch
            reachedEos = [bool]$prediction.reachedEndOfSequence
            hasArray = $hasArray
            hasMultiline = $hasMultiline
            hasMathFont = $hasMathFont
            lengthClass = $lengthClass
            latexTokenCount = $latexTokens
            boundedCharacterEditRate = [double]$editDistance / $maxLength
            fontWrapperStrippedBoundedEditRate = [double]$styleEditDistance / $styleMaxLength
            cer = [double]$editDistance / [Math]::Max(1, $referenceLength)
        })
    }
    if ($enriched.Count -ne 121) { throw "Expected 121 predictions for $modelId; found $($enriched.Count)." }

    $groups = [System.Collections.Generic.List[object]]::new()
    $groupSpecs = @(
        @{ name = 'all'; select = { param($s) $true } },
        @{ name = 'single-line'; select = { param($s) !$s.hasMultiline } },
        @{ name = 'multi-line'; select = { param($s) $s.hasMultiline } },
        @{ name = 'contains-array-or-alignment'; select = { param($s) $s.hasArray } },
        @{ name = 'contains-math-font-command'; select = { param($s) $s.hasMathFont } },
        @{ name = 'short (≤40 LaTeX tokens)'; select = { param($s) $s.lengthClass -eq 'short (≤40 LaTeX tokens)' } },
        @{ name = 'medium (41–120 LaTeX tokens)'; select = { param($s) $s.lengthClass -eq 'medium (41–120 LaTeX tokens)' } },
        @{ name = 'long (>120 LaTeX tokens)'; select = { param($s) $s.lengthClass -eq 'long (>120 LaTeX tokens)' } }
    )
    foreach ($spec in $groupSpecs) {
        $members = @($enriched | Where-Object { & $spec.select $_ })
        if ($members.Count -eq 0) { continue }
        $distanceSum = [long](($members | Measure-Object -Property editDistance -Sum).Sum)
        $maxDenominatorSum = [long](($members | ForEach-Object { [Math]::Max(1, [Math]::Max($_.referenceLength, $_.predictionLength)) } | Measure-Object -Sum).Sum)
        $referenceLengthSum = [long](($members | Measure-Object -Property referenceLength -Sum).Sum)
        $fontWrapperDistanceSum = [long](($members | Measure-Object -Property fontWrapperStrippedEditDistance -Sum).Sum)
        $fontWrapperDenominatorSum = [long](($members | ForEach-Object { [Math]::Max(1, [Math]::Max($_.fontWrapperStrippedReferenceLength, $_.fontWrapperStrippedPredictionLength)) } | Measure-Object -Sum).Sum)
        $fontWrapperExactCount = @($members | Where-Object fontWrapperStrippedExact).Count
        $exactCount = @($members | Where-Object exact).Count
        $eosCount = @($members | Where-Object reachedEos).Count
        $groups.Add([ordered]@{
            category = $spec.name
            sampleCount = $members.Count
            exactMatches = $exactCount
            exactMatchRate = [double]$exactCount / $members.Count
            meanBoundedCharacterEditRate = [double](($members | Measure-Object -Property boundedCharacterEditRate -Average).Average)
            corpusBoundedCharacterEditRate = [double]$distanceSum / [Math]::Max(1, $maxDenominatorSum)
            corpusReferenceNormalizedCer = [double]$distanceSum / [Math]::Max(1, $referenceLengthSum)
            fontWrapperStrippedExactMatches = $fontWrapperExactCount
            fontWrapperStrippedExactMatchRate = [double]$fontWrapperExactCount / $members.Count
            meanFontWrapperStrippedBoundedEditRate = [double](($members | Measure-Object -Property fontWrapperStrippedBoundedEditRate -Average).Average)
            corpusFontWrapperStrippedBoundedEditRate = [double]$fontWrapperDistanceSum / [Math]::Max(1, $fontWrapperDenominatorSum)
            reachedEosCount = $eosCount
            nonEosCount = $members.Count - $eosCount
        })
    }
    $worst = @($enriched | Sort-Object -Property boundedCharacterEditRate -Descending | Select-Object -First 10 | ForEach-Object {
        [ordered]@{
            image = $_.image
            referenceLength = $_.referenceLength
            predictionLength = $_.predictionLength
            cer = $_.cer
            boundedCharacterEditRate = $_.boundedCharacterEditRate
            fontWrapperStrippedBoundedEditRate = $_.fontWrapperStrippedBoundedEditRate
            latexTokenCount = $_.latexTokenCount
            hasArray = $_.hasArray
            hasMultiline = $_.hasMultiline
            hasMathFont = $_.hasMathFont
            reachedEos = $_.reachedEos
        }
    })
    $nonEosSamples = @($enriched | Where-Object { !$_.reachedEos } | ForEach-Object {
        [ordered]@{
            image = $_.image
            referenceLength = $_.referenceLength
            predictionLength = $_.predictionLength
            editDistance = $_.editDistance
            boundedCharacterEditRate = $_.boundedCharacterEditRate
            latexTokenCount = $_.latexTokenCount
        }
    })
    $models.Add([ordered]@{
        model = "paddle-formula/$modelId"
        report = "formula-realformula-$modelId-ort-20261007.json"
        sampleCount = $enriched.Count
        datasetShapeAudit = [ordered]@{
            singleLineCount = @($enriched | Where-Object { !$_.hasMultiline }).Count
            multiLineCount = @($enriched | Where-Object hasMultiline).Count
            arrayOrAlignmentCount = @($enriched | Where-Object hasArray).Count
            mathFontCommandCount = @($enriched | Where-Object hasMathFont).Count
        }
        groups = @($groups)
        nonEosSamples = $nonEosSamples
        worstTen = $worst
    })
}

$manifestSha = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$result = [ordered]@{
    schemaVersion = 'deploysharp-realformula-error-patterns-v1'
    generatedAtUtc = [DateTimeOffset]::UtcNow
    dataset = [ordered]@{
        name = 'MathNet realFormula'
        version = 'Zenodo 11296815 v1'
        annotatedSamples = $samplesByImage.Count
        manifestSha256 = $manifestSha
        source = 'https://doi.org/10.5281/zenodo.11296815'
    }
    method = [ordered]@{
        categories = 'Reference-label lexical features: multi-line breaks/alignment environments, array/alignment environments, and selected mathematical font commands; length bins use LaTeX lexical token count.'
        latexTokenPattern = $tokenPattern
        fontWrapperSensitivity = 'Strips only selected flat wrapper forms with brace-free payloads; this is sensitivity analysis and can remove meaningful typography.'
        boundedCharacterEditRate = 'Whitespace-stripped character Levenshtein distance / max(reference length, prediction length); bounded and diagnostic, not MathNet paper token-level EditScore and not TeX visual/semantic equivalence.'
        scope = 'Post-hoc stratification of committed ORT CPU per-sample reports; no inference is rerun and no model/backend support claim is added.'
    }
    datasetShapeAudit = $models[0].datasetShapeAudit
    models = @($models)
    boundary = 'The dataset contains a small number of multi-line, array/alignment, and mathematical-font cases. Group scores have small and overlapping denominators; inspect per-sample predictions and render/parse them before treating string errors as visual or mathematical errors. Checkpoint training overlap is unknown.'
}

$jsonPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputJsonPath))
$markdownPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputMarkdownPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath) | Out-Null
$result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# MathNet realFormula — error-pattern diagnostics')
$lines.Add('')
$lines.Add('This post-hoc report stratifies the six already committed ORT CPU prediction sets against the pinned 121-row reference manifest. It does not rerun models. Source labels remain outside Git; only derived counts and worst-case image IDs are included here.')
$lines.Add('')
$lines.Add(('Dataset: MathNet realFormula v1, `{0}` annotated samples; manifest SHA-256 `{1}`. Source: [Zenodo DOI](https://doi.org/10.5281/zenodo.11296815).' -f $samplesByImage.Count, $manifestSha))
$lines.Add('')
$lines.Add(('Reference strata in the normalized annotation strings: {0} without explicit line-break/alignment markers, {1} with such markers, {2} array/alignment environments, {3} containing selected math-font commands. Categories overlap; this is a syntax scan, not an image-level typography annotation.' -f $result.datasetShapeAudit.singleLineCount, $result.datasetShapeAudit.multiLineCount, $result.datasetShapeAudit.arrayOrAlignmentCount, $result.datasetShapeAudit.mathFontCommandCount))
$lines.Add('')
$lines.Add('The character CER in the inference report is unchanged. This adds a bounded character edit rate `distance / max(reference length, prediction length)` and splits results by reference features. A second sensitivity column strips only selected flat font wrappers with brace-free payloads (`\mathrm`, `\mathbf`, `\boldsymbol`, `\mathbb`, `\mathcal`, `\mathfrak`, `\mathscr`) before whitespace removal. For example, the normalized label and Plus-L output for `211111875-19.png` become identical after stripping those selected flat wrappers from both strings. This is not a safe general LaTeX canonicalizer: it can erase meaningful typography. Neither score is the normalized token EditScore used in the MathNet study, a TeX parser result, a rendered-image comparison, or a mathematical-equivalence judgment.')
$lines.Add('')
foreach ($model in $models) {
    $lines.Add(('## `{0}`' -f $model.model))
    $lines.Add('')
    $lines.Add('| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |')
    $lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
    foreach ($group in $model.groups) {
        $lines.Add(('| {0} | {1} | {2}/{1} ({3:P1}) | {4:P2} | {5}/{1} ({6:P1}) | {7:P2} | {8}/{1} |' -f $group.category, $group.sampleCount, $group.exactMatches, $group.exactMatchRate, $group.meanBoundedCharacterEditRate, $group.fontWrapperStrippedExactMatches, $group.fontWrapperStrippedExactMatchRate, $group.meanFontWrapperStrippedBoundedEditRate, $group.reachedEosCount))
    }
    $lines.Add('')
    if ($model.nonEosSamples.Count -eq 0) {
        $lines.Add('Non-EOS samples: none.')
    }
    else {
        $lines.Add(('Non-EOS samples (decoder did not emit EOS): {0}' -f (($model.nonEosSamples | ForEach-Object { '{0} (prediction length {1}, reference length {2}, bounded edit {3:P0}, LaTeX tokens {4})' -f $_.image, $_.predictionLength, $_.referenceLength, $_.boundedCharacterEditRate, $_.latexTokenCount }) -join '; ')))
    }
    $lines.Add('')
    $lines.Add(('Worst cases by bounded character edit rate: {0}' -f (($model.worstTen | ForEach-Object { '{0} (CER {1:P0}, edit {2:P0}, tokens {3}, multi-line {4}, array/alignment {5}, math-font {6}, EOS {7})' -f $_.image, $_.cer, $_.boundedCharacterEditRate, $_.latexTokenCount, $_.hasMultiline, $_.hasArray, $_.hasMathFont, $_.reachedEos }) -join '; ')))
    $lines.Add('')
}
$lines.Add('## Interpretation boundary')
$lines.Add('')
$lines.Add('A high string error can include both genuine symbol/structure errors and canonicalization differences (for example style commands, optional braces, array layout, and multi-line formatting). This report identifies where to inspect; it cannot reclassify those cases as correct. FormulaNet-L EOS completion and very long repetitive outputs remain a separate decoder/model-quality issue. More authoritative follow-up requires official PaddleX prediction normalization parity, a reproducible TeX render/visual comparison or vetted semantic normalizer, and a held-out/domain-matched quality set.')
$lines.Add('')
$lines.Add('The original per-sample output and raw diagnostics are linked in the [six-model inference report](formula-realformula-six-models-ort-20261007.md).')
$lines -join [Environment]::NewLine | Set-Content -LiteralPath $markdownPath -Encoding utf8
Write-Output "Wrote $jsonPath"
Write-Output "Wrote $markdownPath"
