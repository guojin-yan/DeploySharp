[CmdletBinding()]
param(
    [string]$VerificationRoot,
    [string]$OutputJson,
    [string]$OutputMarkdown,
    [int]$ExpectedSampleCount = 121,
    [double]$MinimumEosRate = 0.99,
    [double]$MinimumPredictionRenderRate = 0.90,
    [double]$MinimumComparableRenderRate = 0.90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($VerificationRoot)) {
    $VerificationRoot = Join-Path $scriptRoot '..\verification'
}
if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $OutputJson = Join-Path $VerificationRoot 'formula-quality-gate-20261017.json'
}
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $VerificationRoot 'formula-quality-gate-20261017.md'
}
if ($ExpectedSampleCount -lt 1) { throw 'ExpectedSampleCount must be positive.' }
if ($MinimumEosRate -lt 0 -or $MinimumEosRate -gt 1) { throw 'MinimumEosRate must be between 0 and 1.' }
if ($MinimumPredictionRenderRate -lt 0 -or $MinimumPredictionRenderRate -gt 1) { throw 'MinimumPredictionRenderRate must be between 0 and 1.' }
if ($MinimumComparableRenderRate -lt 0 -or $MinimumComparableRenderRate -gt 1) { throw 'MinimumComparableRenderRate must be between 0 and 1.' }

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing evidence file: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-Rate([int]$Numerator, [int]$Denominator) {
    if ($Denominator -eq 0) { return 0.0 }
    return $Numerator / [double]$Denominator
}

$summaryPath = Join-Path $VerificationRoot 'formula-realformula-six-models-ort-20261007-summary.json'
$parseabilityPath = Join-Path $VerificationRoot 'formula-realformula-latex-parseability-20261012.json'
$renderPath = Join-Path $VerificationRoot 'formula-realformula-render-quality-20261013.json'
$syntaxPath = Join-Path $VerificationRoot 'formula-realformula-syntax-quality-20261012.json'
$summary = Read-Json $summaryPath
$parseability = Read-Json $parseabilityPath
$render = Read-Json $renderPath
$syntax = Read-Json $syntaxPath

$parseByModel = @{}
foreach ($row in @($parseability.models)) { $parseByModel[[string]$row.model] = $row }
$renderByModel = @{}
foreach ($row in @($render.modelResults)) { $renderByModel[[string]$row.model] = $row }
$syntaxByModel = @{}
foreach ($row in @($syntax.models)) { $syntaxByModel[[string]$row.model] = $row }

$models = [System.Collections.Generic.List[object]]::new()
$allIntegrityPassed = $true
$allDiagnosticEvidencePassed = $true
foreach ($row in @($summary.models)) {
    $model = [string]$row.model
    if (-not $parseByModel.ContainsKey($model)) { throw "Parseability evidence missing model: $model" }
    if (-not $renderByModel.ContainsKey($model)) { throw "Render evidence missing model: $model" }
    if (-not $syntaxByModel.ContainsKey($model)) { throw "Syntax evidence missing model: $model" }

    $parseRow = $parseByModel[$model]
    $renderRow = $renderByModel[$model]
    $syntaxRow = $syntaxByModel[$model]
    $sampleCount = [int]$row.sampleCount
    $eosRate = Get-Rate ([int]$row.reachedEndOfSequenceCount) $sampleCount
    $renderRate = Get-Rate ([int]$renderRow.predictionRenderSucceeded) $sampleCount
    $comparableRate = Get-Rate ([int]$renderRow.comparableRenderPairs) $sampleCount
    $integrityPassed = $sampleCount -eq $ExpectedSampleCount -and [int]$row.emptyPredictionCount -eq 0 -and $eosRate -ge $MinimumEosRate
    $diagnosticEvidencePassed = $renderRate -ge $MinimumPredictionRenderRate -and $comparableRate -ge $MinimumComparableRenderRate
    if (-not $integrityPassed) { $allIntegrityPassed = $false }
    if (-not $diagnosticEvidencePassed) { $allDiagnosticEvidencePassed = $false }

    $reasons = [System.Collections.Generic.List[string]]::new()
    if ($sampleCount -ne $ExpectedSampleCount) { $reasons.Add("sample count $sampleCount != expected $ExpectedSampleCount") }
    if ([int]$row.emptyPredictionCount -ne 0) { $reasons.Add("empty predictions $($row.emptyPredictionCount)") }
    if ($eosRate -lt $MinimumEosRate) { $reasons.Add(('EOS rate {0:P2} < {1:P2}' -f $eosRate, $MinimumEosRate)) }
    if ($renderRate -lt $MinimumPredictionRenderRate) { $reasons.Add(('prediction render rate {0:P2} < {1:P2}' -f $renderRate, $MinimumPredictionRenderRate)) }
    if ($comparableRate -lt $MinimumComparableRenderRate) { $reasons.Add(('comparable render rate {0:P2} < {1:P2}' -f $comparableRate, $MinimumComparableRenderRate)) }
    $reasons.Add('mathematical semantic equivalence has no independent evaluator/evidence')

    $models.Add([ordered]@{
        model = $model
        modelSha256 = [string]$row.modelSha256
        sampleCount = $sampleCount
        exactMatchRate = [double]$row.exactMatchRate
        corpusNormalizedCharErrorRate = [double]$row.corpusNormalizedCharErrorRate
        eosRate = $eosRate
        emptyPredictionCount = [int]$row.emptyPredictionCount
        predictionRenderRate = $renderRate
        comparableRenderRate = $comparableRate
        parseAcceptanceRate = [double]$parseRow.parseAcceptanceRate
        syntaxValidRate = [double]$syntaxRow.predictionSyntaxValidRate
        structureFingerprintMatchRate = [double]$syntaxRow.pairedStructureFingerprintMatchRate
        integrityPassed = $integrityPassed
        diagnosticEvidencePassed = $diagnosticEvidencePassed
        semanticEquivalence = 'not-assessed'
        releaseGate = 'blocked'
        blockingReasons = @($reasons)
    })
}

$report = [ordered]@{
    schemaVersion = 'deploysharp-formula-quality-gate-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    dataset = [ordered]@{
        name = 'MathNet realFormula'
        version = 'Zenodo 11296815 v1'
        sampleCount = $ExpectedSampleCount
        sourceSummary = (Split-Path -Leaf $summaryPath)
        sourceParseability = (Split-Path -Leaf $parseabilityPath)
        sourceRender = (Split-Path -Leaf $renderPath)
        sourceSyntax = (Split-Path -Leaf $syntaxPath)
    }
    policy = [ordered]@{
        expectedSampleCountPerModel = $ExpectedSampleCount
        minimumEosRate = $MinimumEosRate
        minimumPredictionRenderRate = $MinimumPredictionRenderRate
        minimumComparableRenderRate = $MinimumComparableRenderRate
        semanticRule = 'No release pass is possible without an independent, reproducible mathematical-semantic evaluator; lexical, parser and raster diagnostics are not substitutes.'
    }
    models = @($models)
    integrity = [ordered]@{
        allModelsPassedExecutionIntegrity = $allIntegrityPassed
        allModelsPassedDiagnosticEvidenceThresholds = $allDiagnosticEvidencePassed
        semanticEvidencePresent = $false
        releaseGate = 'blocked'
    }
    boundary = 'This is a fail-closed evidence gate. It records execution integrity and diagnostic thresholds but deliberately cannot claim formula accuracy or mathematical semantic equivalence. ORT CPU is the source quality set; ORT CUDA, OpenVINO, OpenCV DNN and TensorRT backend status remain separate.'
}

$fullJson = [IO.Path]::GetFullPath($OutputJson)
$fullMarkdown = [IO.Path]::GetFullPath($OutputMarkdown)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($fullJson)) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fullJson -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Formula realFormula release-quality gate (2026-10-17)')
$lines.Add('')
$lines.Add('This report is a fail-closed gate over the existing 121-sample MathNet realFormula evidence. It separates execution integrity from parser/render diagnostics and refuses to declare formula quality without an independent mathematical-semantic evaluator.')
$lines.Add('')
$lines.Add("- Overall gate: **BLOCKED**")
$lines.Add("- Execution integrity for all models: **$($allIntegrityPassed.ToString().ToUpperInvariant())**")
$lines.Add("- Diagnostic render thresholds for all models: **$($allDiagnosticEvidencePassed.ToString().ToUpperInvariant())**")
$lines.Add('- Mathematical semantic evidence: **NOT ASSESSED**')
$lines.Add('')
$lines.Add('| Model | EOS | Empty | CER | Render | Comparable | Parse | Syntax | Semantic | Gate |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|---:|---|---|')
foreach ($item in $models) {
    $lines.Add(('| ``' + [string]$item.model + '`` | ' + ('{0:P2}' -f [double]$item.eosRate) + ' | ' + [string]$item.emptyPredictionCount + ' | ' + ('{0:P2}' -f [double]$item.corpusNormalizedCharErrorRate) + ' | ' + ('{0:P2}' -f [double]$item.predictionRenderRate) + ' | ' + ('{0:P2}' -f [double]$item.comparableRenderRate) + ' | ' + ('{0:P2}' -f [double]$item.parseAcceptanceRate) + ' | ' + ('{0:P2}' -f [double]$item.syntaxValidRate) + ' | ``' + [string]$item.semanticEquivalence + '`` | ``' + [string]$item.releaseGate + '`` |'))
}
$lines.Add('')
$lines.Add('## Gate policy')
$lines.Add('')
$lines.Add("- Each model must have exactly $ExpectedSampleCount joined image rows, no empty predictions, and EOS rate at least $([math]::Round(100 * $MinimumEosRate, 2))% for execution integrity.")
$lines.Add("- Prediction render rate and comparable reference/prediction pairs are diagnostic thresholds ($([math]::Round(100 * $MinimumPredictionRenderRate, 2))% and $([math]::Round(100 * $MinimumComparableRenderRate, 2))%); they do not establish semantic correctness.")
$lines.Add('- A release-quality pass requires a separately versioned semantic evaluator and an independent, legally attributable validation set. Exact string/CER, SymPy/ANTLR acceptance, structure fingerprints and MathJax raster similarity remain diagnostics only.')
$lines.Add('- Backend quality is not inferred from this ORT CPU report. OpenVINO, OpenCV DNN and TensorRT retain their precise matrix states.')
$lines.Add('')
$lines.Add('## Reproduction')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add('.\eng\models\paddle-document\scripts\Evaluate-FormulaQualityGate.ps1')
$lines.Add('```')
$lines.Add('')
$lines.Add('The report intentionally remains blocked with the current evidence; this is an expected safety result, not a test failure.')
$lines | Set-Content -LiteralPath $fullMarkdown -Encoding UTF8
Write-Output "Wrote $fullJson and $fullMarkdown"
