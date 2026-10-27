[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '../../../src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json'),
    [string]$ManifestDirectory = (Join-Path $PSScriptRoot 'releases'),
    [string]$ModelRoot,
    [switch]$RequireReleaseEligible
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
$entries = @($catalog.entries | Where-Object { $_.modelId -like 'paddleocr/ppocrv5/*' })
if ($entries.Count -ne 6) { throw "Expected six published PP-OCRv5 catalog entries, found $($entries.Count)." }
$verifiedFiles = 0
$missingFiles = [Collections.Generic.List[string]]::new()

foreach ($entry in $entries) {
    $suffix = ([string]$entry.modelId).Split('/')[-1]
    $manifestName = "$suffix.modelpack.json"
    $manifestPath = Join-Path $ManifestDirectory $manifestName
    $text = [IO.File]::ReadAllText($manifestPath)
    $manifest = $text | ConvertFrom-Json
    if ($entry.release.tag -ne 'models-visual.1' -or $entry.status -ne 'preview' -or $manifest.modelId -ne $entry.modelId) {
        throw "Published PP-OCRv5 identity mismatch: $($entry.modelId)."
    }
    $releaseBase = 'https://github.com/guojin-yan/DeploySharp/releases/download/' + $entry.release.tag + '/'
    $artifacts = @($entry.artifacts)
    if ($artifacts.Count -ne 1) { throw "Expected one ONNX artifact: $($entry.modelId)." }
    $artifact = $artifacts[0]
    $manifestArtifact = @($manifest.artifacts | Where-Object artifactId -eq $artifact.artifactId)
    if ($manifestArtifact.Count -ne 1) { throw "ModelPack artifact mismatch: $($entry.modelId)." }
    $manifestAsset = @($artifact.assets | Where-Object kind -eq 'manifest')
    # Reconstruct the immutable UTF-8/CRLF upload even after an LF checkout.
    $bytes = [Text.Encoding]::UTF8.GetBytes($text.Replace("`r`n", "`n").Replace("`n", "`r`n"))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if ($manifestAsset.Count -ne 1 -or $manifestAsset[0].size -ne $bytes.Length -or $manifestAsset[0].sha256 -ne $hash -or $manifestAsset[0].downloadUrl -ne ($releaseBase + $manifestName)) {
        throw "Catalog-to-ModelPack size, hash, or URL mismatch: $($entry.modelId)."
    }
    $files = @($manifestArtifact[0].files)
    $assets = @($artifact.assets | Where-Object kind -ne 'manifest')
    if ($files.Count -ne $assets.Count) { throw "Catalog-to-ModelPack file count mismatch: $($entry.modelId)." }
    foreach ($file in $files) {
        $asset = @($assets | Where-Object relativePath -eq $file.relativePath)
        $remoteName = switch ($file.role) {
            'model' { "ppocrv5-$suffix.model.onnx" }
            'labels' { 'ppocrv5_dict.txt' }
            'license' { 'paddleocr.LICENSE.txt' }
            default { throw "Unexpected file role: $($file.role)." }
        }
        if ($asset.Count -ne 1 -or $asset[0].size -ne $file.size -or $asset[0].sha256 -ne $file.sha256 -or $asset[0].downloadUrl -ne ($releaseBase + $remoteName)) {
            throw "Catalog-to-model file size, hash, or URL mismatch: $($entry.modelId)/$($file.relativePath)."
        }
        if (-not [string]::IsNullOrWhiteSpace($ModelRoot)) {
            $path = Join-Path $ModelRoot $remoteName
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $missingFiles.Add($remoteName); continue }
            if ((Get-Item -LiteralPath $path).Length -ne $file.size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) {
                throw "Local published asset integrity mismatch: $remoteName."
            }
            $verifiedFiles++
        }
    }
}
if ($RequireReleaseEligible -and ([string]::IsNullOrWhiteSpace($ModelRoot) -or $missingFiles.Count -ne 0)) {
    throw 'Complete local asset verification requires -ModelRoot containing all published ONNX, dictionary, and license assets.'
}
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_BINDING_OK models=$($entries.Count) verified-local-files=$verifiedFiles missing-local-files=$($missingFiles.Count) runtime-inference=not-run"
