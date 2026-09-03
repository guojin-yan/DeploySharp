[CmdletBinding()]
param(
    [string]$CatalogPath,
    [string]$CasesPath
)

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $repoRoot 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json'
}
if ([string]::IsNullOrWhiteSpace($CasesPath)) {
    $CasesPath = Join-Path $repoRoot 'samples/06-models/cases'
}

if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) {
    throw "Catalog file not found: $CatalogPath"
}
if (-not (Test-Path -LiteralPath $CasesPath -PathType Container)) {
    throw "Model cases directory not found: $CasesPath"
}

$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
$expected = @($catalog.entries | ForEach-Object { $_.modelId -replace '/', '--' } | Sort-Object -Unique)
$actual = @(Get-ChildItem -LiteralPath $CasesPath -Directory | ForEach-Object Name | Sort-Object -Unique)

$missing = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual | Where-Object SideIndicator -eq '<=' | ForEach-Object InputObject)
$extra = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual | Where-Object SideIndicator -eq '=>' | ForEach-Object InputObject)
if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
    if ($missing.Count -gt 0) { Write-Error ('Missing model cases: ' + ($missing -join ', ')) }
    if ($extra.Count -gt 0) { Write-Error ('Unexpected model cases: ' + ($extra -join ', ')) }
    exit 1
}

$readmeMissing = @($expected | Where-Object { -not (Test-Path -LiteralPath (Join-Path $CasesPath $_ 'README.md') -PathType Leaf) })
if ($readmeMissing.Count -gt 0) {
    throw ('Model cases without README.md: ' + ($readmeMissing -join ', '))
}

$verificationMissing = @($expected | Where-Object {
    $readme = Get-Content -Raw -LiteralPath (Join-Path $CasesPath $_ 'README.md')
    $readme -notmatch '## Verification record' -or
    $readme -notmatch '\| GitHub Release asset metadata \| PASS \|' -or
    $readme -notmatch '\| ModelPack manifest download \| PASS \|'
})
if ($verificationMissing.Count -gt 0) {
    throw ('Model cases without a completed release/manifest verification record: ' + ($verificationMissing -join ', '))
}

$invalidCommands = @($expected | Where-Object {
    $readme = Get-Content -Raw -LiteralPath (Join-Path $CasesPath $_ 'README.md')
    $readme -notmatch 'dotnet run --project samples/06-models/(catalog-workflow/ModelFactoryCatalogInspection\.csproj|release-inference/ModelReleaseInference\.csproj) -c Release -- --model-id'
})
if ($invalidCommands.Count -gt 0) {
    throw ('Model cases without a runnable project-file command: ' + ($invalidCommands -join ', '))
}

Write-Output "DEPLOYSHARP_MODEL_SAMPLE_COVERAGE_OK entries=$($expected.Count) cases=$($actual.Count)"
