param(
    [string]$OutputPath = 'eng/models/paddle-document/verification/document-link-audit-20261002.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$documents = @(
    'docs/articles/visual-ocr.md',
    'docs/articles/visual-paddle-structure.md',
    'eng/models/paddle-ocr/README.md',
    'eng/models/paddle-document/README.md',
    'eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007.md',
    'eng/models/paddle-document/verification/formula-realformula-error-patterns-20261007.md',
    'docs/model-backend-verification-matrix.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-20261002.md'
)
$records = [System.Collections.Generic.List[object]]::new()
foreach ($relativeDocument in $documents) {
    $documentPath = Join-Path $repoRoot $relativeDocument
    if (!(Test-Path -LiteralPath $documentPath -PathType Leaf)) { throw "Missing document: $relativeDocument" }
    $documentDirectory = Split-Path -Parent $documentPath
    $text = Get-Content -LiteralPath $documentPath -Raw
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) { $target = $target.Substring(1, $target.Length - 2) }
        if ([string]::IsNullOrWhiteSpace($target) -or $target.StartsWith('#') -or $target -match '^(?i:https?|mailto):') { continue }
        $target = $target.Split('#')[0].Split('?')[0]
        if ([string]::IsNullOrWhiteSpace($target)) { continue }
        $target = [Uri]::UnescapeDataString($target)
        $resolved = [IO.Path]::GetFullPath((Join-Path $documentDirectory $target))
        $exists = Test-Path -LiteralPath $resolved -PathType Leaf
        if (!$exists -and (Test-Path -LiteralPath $resolved -PathType Container)) {
            $exists = (Test-Path -LiteralPath (Join-Path $resolved 'README.md') -PathType Leaf) -or
                (Test-Path -LiteralPath (Join-Path $resolved 'index.md') -PathType Leaf)
        }
        $records.Add([ordered]@{
            document = $relativeDocument
            target = $target
            resolved = $resolved.Substring($repoRoot.Length).TrimStart('\', '/')
            exists = $exists
        })
    }
}

$broken = @($records | Where-Object { !$_.exists })
$output = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-doc-link-audit-v2'
    generatedUtc = [DateTimeOffset]::UtcNow
    documents = $documents
    localMarkdownLinksChecked = $records.Count
    brokenLocalMarkdownLinks = $broken.Count
    broken = $broken
    scope = 'Relative local Markdown links only; external URLs and generated runtime paths are not network-validated.'
}
$outputPathFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
$outputDirectory = Split-Path -Parent $outputPathFull
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$output | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPathFull -Encoding utf8
if ($broken.Count -gt 0) {
    $broken | ForEach-Object { Write-Error ("Broken link: " + $_.document + " -> " + $_.target) }
    exit 1
}
Write-Output ("Checked {0} local Markdown links; broken={1}; report={2}" -f $records.Count, $broken.Count, $outputPathFull)
