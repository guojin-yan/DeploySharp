[CmdletBinding()]
param([string]$WideLineReportPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\OcrTextAccuracy.ps1')

# These cases expose the previous character-distance/word-count bug, ordinal
# comparison, Unicode scalar counting, insertions/deletions and empty inputs.
$cases = @(
    @{ reference = 'hello world'; hypothesis = 'hullo world'; characters = 11; characterEdits = 1; words = 2; wordEdits = 1 },
    @{ reference = 'hello world'; hypothesis = 'hello there'; characters = 11; characterEdits = 5; words = 2; wordEdits = 1 },
    @{ reference = 'hello'; hypothesis = 'HELLO'; characters = 5; characterEdits = 5; words = 1; wordEdits = 1 },
    @{ reference = '😀'; hypothesis = '😁'; characters = 1; characterEdits = 1; words = 1; wordEdits = 1 },
    @{ reference = "a`t b"; hypothesis = 'a b'; characters = 4; characterEdits = 1; words = 2; wordEdits = 0 },
    @{ reference = 'a b'; hypothesis = 'a b c d e'; characters = 3; characterEdits = 6; words = 2; wordEdits = 3 },
    @{ reference = 'a b c'; hypothesis = 'a c'; characters = 5; characterEdits = 2; words = 3; wordEdits = 1 },
    @{ reference = '编号001'; hypothesis = '编号001'; characters = 5; characterEdits = 0; words = 1; wordEdits = 0 },
    @{ reference = ''; hypothesis = ''; characters = 0; characterEdits = 0; words = 0; wordEdits = 0 },
    @{ reference = ''; hypothesis = 'a b'; characters = 0; characterEdits = 3; words = 0; wordEdits = 2 },
    @{ reference = 'a b'; hypothesis = ''; characters = 3; characterEdits = 3; words = 2; wordEdits = 2 }
)
foreach ($case in $cases) {
    $actual = Get-OcrTextAccuracy -Reference $case.reference -Hypothesis $case.hypothesis
    foreach ($pair in @(@('referenceCharacters', 'characters'), @('characterEditDistance', 'characterEdits'), @('referenceWords', 'words'), @('wordEditDistance', 'wordEdits'))) {
        if ($actual.($pair[0]) -ne $case[$pair[1]]) { throw "Incorrect $($pair[0]) for reference '$($case.reference)': $($actual | ConvertTo-Json -Compress)" }
    }
    $expectedCer = if ($case.characters) { $case.characterEdits / [double]$case.characters } elseif ($case.hypothesis.Length) { 1.0 } else { 0.0 }
    $expectedWer = if ($case.words) { $case.wordEdits / [double]$case.words } elseif ($case.hypothesis.Length) { 1.0 } else { 0.0 }
    if ([Math]::Abs($actual.characterErrorRate - $expectedCer) -gt 1e-12 -or [Math]::Abs($actual.wordErrorRate - $expectedWer) -gt 1e-12) { throw 'Incorrect CER/WER ratio.' }
}
Write-Output "OCR_TEXT_ACCURACY_CONTRACT=pass ($($cases.Count) cases)"

if ($WideLineReportPath) {
    $reportFull = (Resolve-Path -LiteralPath $WideLineReportPath).Path
    $evidence = Get-Content -LiteralPath $reportFull -Raw | ConvertFrom-Json -Depth 40
    if (-not $evidence.localTraceEvidence.fileName) { throw 'The evidence must be generated with DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1.' }
    $tracePath = Join-Path (Split-Path $reportFull) $evidence.localTraceEvidence.fileName
    $traceSha = (Get-FileHash -LiteralPath $tracePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($traceSha -cne $evidence.localTraceEvidence.sha256) { throw 'Local trace SHA-256 mismatch.' }
    $rows = @{}
    foreach ($row in $evidence.rows) {
        $key = "$($row.Backend)/$($row.Model)/$($row.Mode)/$($row.Key)"
        if ($rows.ContainsKey($key)) { throw "Duplicate metric key: $key" }
        $rows.Add($key, $row)
    }
    $traces = @(Get-Content -LiteralPath $tracePath -Raw | ConvertFrom-Json -Depth 40)
    if ($traces.Count -ne 104 -or $rows.Count -ne 104) { throw 'Expected 104 full C# recognition/trace rows.' }
    $seen = @{}
    $fields = @('referenceCharacters', 'hypothesisCharacters', 'characterEditDistance', 'characterErrorRate', 'referenceWords', 'hypothesisWords', 'wordEditDistance', 'wordErrorRate')
    foreach ($trace in $traces) {
        $key = "$($trace.backend)/$($trace.model)/$($trace.mode)/$($trace.Key)"
        if (-not $rows.ContainsKey($key) -or $seen.ContainsKey($key)) { throw "Missing or duplicate trace key: $key" }
        $seen.Add($key, $true)
        $actual = Get-OcrTextAccuracy -Reference $trace.expected -Hypothesis $trace.recognized
        foreach ($field in $fields) {
            if ([Math]::Abs([double]$actual.$field - [double]$rows[$key].Raw.$field) -gt 1e-12) { throw "PowerShell/C# $field mismatch: $key" }
        }
    }
    Write-Output "OCR_TEXT_ACCURACY_CSHARP_PARITY=pass ($($seen.Count) real pairs, $($seen.Count * $fields.Count) fields)"
}
