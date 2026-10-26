param(
    [string]$ReportPath = 'eng/models/paddle-ocr/verification/paddleocr-vl-release-six-task-offline-linux-20261026.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$fullReportPath = if ([System.IO.Path]::IsPathRooted($ReportPath)) { $ReportPath } else { Join-Path $repoRoot $ReportPath }
$report = Get-Content -LiteralPath $fullReportPath -Raw | ConvertFrom-Json
$referencePath = Join-Path $repoRoot 'eng/models/paddle-ocr/verification/paddleocr-vl-reference-summary-linux-20261023.json'
$reference = Get-Content -LiteralPath $referencePath -Raw | ConvertFrom-Json

if ($report.schemaVersion -ne 'deploysharp-paddleocr-vl-release-six-task-offline-v1') { throw 'Unexpected report schema.' }
if ($report.bundle.modelId -ne 'paddleocr-vl/1.6' -or $report.bundle.checkpointRevision -ne $reference.checkpoint.revision) { throw 'Bundle identity mismatch.' }
if ($report.bundle.verifiedGraphs -ne 5 -or $report.bundle.verifiedArtifacts -ne 9) { throw 'Incomplete Bundle verification.' }
if ($report.identities.officialReferenceSha256 -ne (Get-FileHash -LiteralPath $referencePath -Algorithm SHA256).Hash) { throw 'Reference SHA-256 mismatch.' }
if ($report.cases.Count -ne 6 -or $reference.cases.Count -ne 6) { throw 'Expected exactly six tasks.' }

$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($expected in $reference.cases) {
    $rows = @($report.cases | Where-Object { $_.id -ceq $expected.id })
    if ($rows.Count -ne 1 -or -not $seen.Add($expected.id)) { throw "Missing or repeated task: $($expected.id)" }
    $actual = $rows[0]
    $expectedTextHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($expected.decoded))).ToLowerInvariant()
    if ($actual.task -cne $expected.task -or $actual.inputSha256 -ne $expected.inputSha256 -or
        $actual.tokenCount -ne $expected.generatedTokenCount -or
        $actual.tokenIdsCsvSha256 -ne $expected.generatedTokenCsvSha256 -or
        $actual.textUtf8Sha256 -ne $expectedTextHash -or
        $actual.finishReason -ne 'EndOfSequence' -or $actual.exitCode -ne 0) {
        throw "Reference mismatch: $($expected.id)"
    }
    if ($actual.generationMilliseconds -le 0 -or $actual.processWallSeconds -le 0 -or $actual.maximumRssKiB -le 0) {
        throw "Missing resource observation: $($expected.id)"
    }
}
Write-Output "validated=$($seen.Count); model=$($report.bundle.modelId); status=reference-match"
