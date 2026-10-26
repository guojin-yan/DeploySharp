[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DatasetRoot,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path -LiteralPath $DatasetRoot).Path
$source = Join-Path $root 'realFormula-manifest.jsonl'
$output = [IO.Path]::GetFullPath($OutputPath)
$expectedSourceSha256 = '9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76'
if ($output -ieq $source -or (Test-Path -LiteralPath $output)) {
    throw 'Output must be a new file distinct from the pinned source manifest.'
}
$actualSourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSourceSha256 -cne $expectedSourceSha256) {
    throw "Unexpected realFormula source manifest SHA-256: $actualSourceSha256"
}

$cases = [System.Collections.Generic.List[object]]::new()
$ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in [IO.File]::ReadLines($source)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $row = $line | ConvertFrom-Json -Depth 10
    $relative = [string]$row.ImageRelPath
    if (-not $relative.StartsWith('img-corrected/', [StringComparison]::Ordinal) -or
        $relative.Contains('..') -or $relative.Contains('\')) {
        throw "Unexpected dataset-relative image path: $relative"
    }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($path) -cne [string]$row.Image) {
        throw "Image path escapes or mismatches the dataset root: $relative"
    }
    $id = [IO.Path]::GetFileNameWithoutExtension([string]$row.Image)
    if (-not $ids.Add($id)) { throw "Duplicate realFormula case ID: $id" }
    $imageSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($imageSha256 -cne [string]$row.ImageSha256) {
        throw "Pinned image SHA-256 mismatch: $relative"
    }
    $cases.Add([ordered]@{
        id = $id
        task = 'formula'
        image = $relative
        sha256 = $imageSha256
    })
}
if ($cases.Count -ne 121) { throw "Expected 121 realFormula images; found $($cases.Count)." }

$manifest = [ordered]@{
    checkpointRevision = 'c5630abae1d940eafe0697512a0325494b02ab42'
    datasetManifestSha256 = $actualSourceSha256
    cases = @($cases.ToArray())
}
$null = New-Item -ItemType Directory -Force -Path (Split-Path $output)
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output "PADDLEOCR_VL_OFFICIAL_MANIFEST=$output"
Write-Output "PADDLEOCR_VL_OFFICIAL_CASES=$($cases.Count)"
Write-Output "PADDLEOCR_VL_OFFICIAL_MANIFEST_SHA256=$((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant())"
