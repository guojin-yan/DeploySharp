[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BundleRoot,
    [Parameter(Mandatory)][string]$CurrentCatalogPath,
    [Parameter(Mandatory)][string]$CurrentReadmePath,
    [Parameter(Mandatory)][string]$CurrentChecksumsPath,
    [Parameter(Mandatory)][string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path -LiteralPath $BundleRoot).Path
$catalogPath = (Resolve-Path -LiteralPath $CurrentCatalogPath).Path
$readmePath = (Resolve-Path -LiteralPath $CurrentReadmePath).Path
$checksumsPath = (Resolve-Path -LiteralPath $CurrentChecksumsPath).Path
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite an existing release stage: $output" }

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$manifestPath = Join-Path $bundle 'paddleocr-vl-bundle.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 'deploysharp-paddleocr-vl-bundle-v2' -or
    $manifest.bundleId -ne 'paddleocr-vl/1.6' -or
    $manifest.modelId -ne 'paddleocr-vl/1.6' -or
    $manifest.checkpointRevision -ne 'c5630abae1d940eafe0697512a0325494b02ab42') {
    throw 'Only the pinned PaddleOCR-VL-1.6 five-graph Bundle can be staged.'
}

$descriptors = @(
    @{ role = 'vision'; localName = 'vision.onnx'; format = 'onnx'; modelId = 'paddleocr-vl/1.6/vision' },
    @{ role = 'embedding'; localName = 'embedding.onnx'; format = 'onnx'; modelId = 'paddleocr-vl/1.6/embedding' },
    @{ role = 'decoder'; localName = 'decoder.onnx'; format = 'onnx'; modelId = 'paddleocr-vl/1.6/decoder' },
    @{ role = 'head'; localName = 'head.onnx'; format = 'onnx'; modelId = 'paddleocr-vl/1.6/head' },
    @{ role = 'tokenizer-model'; localName = 'tokenizer.model'; format = 'sentencepiece'; modelId = 'paddleocr-vl/1.6' },
    @{ role = 'tokenizer-json'; localName = 'tokenizer.json'; format = 'json'; modelId = 'paddleocr-vl/1.6' },
    @{ role = 'tokenizer-config'; localName = 'tokenizer_config.json'; format = 'json'; modelId = 'paddleocr-vl/1.6' },
    @{ role = 'chat-template'; localName = 'chat_template.jinja'; format = 'jinja'; modelId = 'paddleocr-vl/1.6' },
    @{ role = 'prefill'; localName = 'prefill.onnx'; format = 'onnx'; modelId = 'paddleocr-vl/1.6/prefill' },
    @{ role = 'manifest'; localName = 'paddleocr-vl-bundle.json'; format = 'json'; modelId = 'paddleocr-vl/1.6' }
)
if (@($manifest.artifacts).Count -ne $descriptors.Count - 1) {
    throw 'The Bundle manifest has an unexpected artifact count.'
}

$assets = [System.Collections.Generic.List[object]]::new()
foreach ($descriptor in $descriptors) {
    $source = Join-Path $bundle $descriptor.localName
    $file = Get-Item -LiteralPath $source -ErrorAction Stop
    $sha = Get-Sha256 $file.FullName
    if ($descriptor.role -ne 'manifest') {
        $declared = @($manifest.artifacts | Where-Object role -eq $descriptor.role)
        if ($declared.Count -ne 1 -or $declared[0].fileName -cne $descriptor.localName -or
            $declared[0].format -cne $descriptor.format -or [long]$declared[0].size -ne $file.Length -or
            $declared[0].sha256 -cne $sha) {
            throw "Bundle integrity mismatch: $($descriptor.role)"
        }
    }
    if ($descriptor.role -eq 'prefill' -and $sha -ne '9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc') {
        throw 'The prefill graph differs from the pinned decoder derivation.'
    }
    $assetName = 'paddleocr-vl-1.6-' + ($descriptor.localName -replace '_', '-')
    if ($descriptor.role -eq 'manifest') { $assetName = 'paddleocr-vl-1.6-bundle.json' }
    $assets.Add([pscustomobject]@{
        role = $descriptor.role; modelId = $descriptor.modelId; assetName = $assetName
        localName = $descriptor.localName; format = $descriptor.format
        size = [long]$file.Length; sha256 = $sha; source = $file.FullName
    })
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -AsHashtable
if ($catalog.schemaVersion -ne 'deploysharp-paddleocr-collection-release-v3' -or
    $catalog.tag -ne 'models-paddleocr' -or $catalog.repository -ne 'guojin-yan/DeploySharp' -or
    @($catalog.bundles | Where-Object bundleId -eq 'paddleocr-vl/1.6').Count -ne 0) {
    throw 'The current Release catalog has an unexpected identity or already contains this Bundle.'
}
$catalog.bundles = @($catalog.bundles) + @([ordered]@{
    bundleId = 'paddleocr-vl/1.6'; modelId = 'paddleocr-vl/1.6'; family = 'paddleocr-vl-generative'
    artifacts = @($assets | ForEach-Object {
        [ordered]@{ role = $_.role; modelId = $_.modelId; assetName = $_.assetName
            localName = $_.localName; format = $_.format; size = $_.size; sha256 = $_.sha256 }
    })
})
$catalog.generatedAtUtc = [DateTime]::UtcNow.ToString('o')
$catalog.notes = @($catalog.notes) + 'PaddleOCR-VL 1.6 is an experimental five-graph Bundle; each graph and tokenizer file is a separate Release asset.'

$readme = Get-Content -LiteralPath $readmePath -Raw
$needle = 'The official PP-OCRv6 pipeline does not publish a separate versioned CLS archive;'
if (-not $readme.Contains($needle) -or $readme.Contains('`paddleocr-vl/1.6`')) {
    throw 'The current Release README cannot be updated without risking duplicate or misplaced text.'
}
$readme = $readme.Replace($needle, '- `paddleocr-vl/1.6` - 10 separately downloadable ONNX/tokenizer/manifest assets; see the Bundle entry in `paddleocr-release-catalog.json`.' + "`n`n" + $needle)

$checksums = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($line in (Get-Content -LiteralPath $checksumsPath | Where-Object { $_ })) {
    if ($line -cnotmatch '^([0-9a-f]{64})  (.+)$') { throw "Invalid SHA256SUMS line: $line" }
    if (-not $checksums.TryAdd($Matches[2], $Matches[1])) { throw "Duplicate SHA256SUMS asset: $($Matches[2])" }
}
if ($checksums['paddleocr-release-catalog.json'] -ne (Get-Sha256 $catalogPath) -or
    $checksums['paddleocr-README.md'] -ne (Get-Sha256 $readmePath)) {
    throw 'The downloaded catalog or README differs from the current Release checksum manifest.'
}
foreach ($asset in $assets) {
    if ($checksums.ContainsKey($asset.assetName)) { throw "Release checksum manifest already contains $($asset.assetName)" }
    $checksums.Add($asset.assetName, $asset.sha256)
}

New-Item -ItemType Directory -Path $output -ErrorAction Stop | Out-Null
foreach ($asset in $assets) {
    New-Item -ItemType HardLink -Path (Join-Path $output $asset.assetName) -Target $asset.source -ErrorAction Stop | Out-Null
}
$newCatalog = Join-Path $output 'paddleocr-release-catalog.json'
$newReadme = Join-Path $output 'paddleocr-README.md'
$newChecksums = Join-Path $output 'SHA256SUMS'
$catalog | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $newCatalog -Encoding utf8
$readme | Set-Content -LiteralPath $newReadme -Encoding utf8
$checksums['paddleocr-release-catalog.json'] = Get-Sha256 $newCatalog
$checksums['paddleocr-README.md'] = Get-Sha256 $newReadme
($checksums.Keys | Sort-Object | ForEach-Object { "$($checksums[$_])  $_" }) -join "`n" | Set-Content -LiteralPath $newChecksums -Encoding ascii
Write-Output "PADDLEOCR_VL_RELEASE_STAGE assets=$($assets.Count) catalogBundles=$(@($catalog.bundles).Count) checksumRows=$($checksums.Count) path=$output"
