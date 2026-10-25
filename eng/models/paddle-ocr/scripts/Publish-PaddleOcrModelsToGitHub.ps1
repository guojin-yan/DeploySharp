[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\paddleocr',
    [string]$PaddleDocumentRoot = 'E:\Model\PaddleDocument',
    [string]$Chart2TableTextGraphRoot,
    [string]$Chart2TableTokenizerRoot,
    [string]$PaddleOcrVlBundleRoot,
    [string]$OutputRoot = 'E:\Model\deploysharp-paddleocr-release-stage',
    [string]$Tag = 'models-paddleocr',
    [string]$Repository = 'guojin-yan/DeploySharp',
    [switch]$Check,
    [switch]$IncludeLegacyBundles,
    [switch]$PruneLegacyArchives,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$catalogPath = Join-Path $PSScriptRoot '..\paddle-ocr-models.json'
$documentCatalogPath = Join-Path $repositoryRoot 'eng\models\paddle-document\paddle-document-models.json'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$documentCatalog = Get-Content -Raw -LiteralPath $documentCatalogPath | ConvertFrom-Json
$summaryPath = Join-Path $ModelRoot 'paddle-ocr-acquisition-summary.json'
$documentSummaryPath = Join-Path $PaddleDocumentRoot 'acquisition-summary.json'
if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) { throw "Run Acquire-PaddleOcrModels.ps1 first: $summaryPath" }
if (-not (Test-Path -LiteralPath $documentSummaryPath -PathType Leaf)) { throw "Run Acquire-PaddleDocumentModels.ps1 first: $documentSummaryPath" }
$documentSummary = Get-Content -Raw -LiteralPath $documentSummaryPath | ConvertFrom-Json

$stageRoot = [IO.Path]::GetFullPath($OutputRoot)
$assetRoot = Join-Path $stageRoot 'assets'
$bundleRoot = Join-Path $stageRoot 'bundles'
if (-not $Check) {
    if (Test-Path -LiteralPath $stageRoot -PathType Container) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $assetRoot, $bundleRoot | Out-Null
}

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Copy-Required([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing release source: $Source" }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}
function Get-RecordPath([string]$Root, [string]$Id) {
    Join-Path $Root ('records\' + (($Id -replace '/', '-') + '.json'))
}
function Get-AssetName([string]$Id, [string]$FileName) {
    (($Id -replace '/', '-') + [IO.Path]::GetExtension($FileName)).ToLowerInvariant()
}

$assetRows = [System.Collections.Generic.List[object]]::new()
$coreRows = [System.Collections.Generic.List[object]]::new()
$documentRows = [System.Collections.Generic.List[object]]::new()
$dictionaryAssets = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$chart2TableBundleRows = [System.Collections.Generic.List[object]]::new()
$paddleOcrVlBundleRows = [System.Collections.Generic.List[object]]::new()

foreach ($entry in @($catalog.models)) {
    $recordPath = Get-RecordPath $ModelRoot ([string]$entry.id)
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "Missing acquisition record: $recordPath" }
    $record = Get-Content -Raw -LiteralPath $recordPath | ConvertFrom-Json
    if ([string]$record.status -notin @('onnx-existing','onnx-converted')) { throw "Model is not release-ready: $($entry.id) status=$($record.status)" }
    $source = Join-Path $ModelRoot ([string]$entry.directory + '\' + [string]$entry.onnx)
    $assetName = Get-AssetName ([string]$entry.id) ([string]$entry.onnx)
    $target = Join-Path $assetRoot $assetName
    if (-not $Check) { Copy-Required $source $target }
    $dictionaryAssetName = $null
    if ($entry.dictionary) {
        $dictionarySource = Join-Path $ModelRoot ([string]$entry.dictionary)
        $dictionaryAssetName = [IO.Path]::GetFileName([string]$entry.dictionary)
        $dictionaryTarget = Join-Path $assetRoot $dictionaryAssetName
        if (-not $Check -and -not (Test-Path -LiteralPath $dictionaryTarget -PathType Leaf)) { Copy-Required $dictionarySource $dictionaryTarget }
        [void]$dictionaryAssets.Add($dictionaryAssetName)
    }
    $row = [ordered]@{
        assetName = $assetName; modelId = [string]$entry.id; family = 'pp-ocr'; version = [string]$entry.version
        task = [string]$entry.family; name = [string]$entry.name; input = [string]$entry.input; output = [string]$entry.output; opset = [int]$entry.opset
        profileFactory = [string]$entry.codeProfile; dictionaryAssetName = $dictionaryAssetName
        sourceArchive = [string]$record.sourceUrl; status = [string]$record.status
        size = [long]$record.onnx.size; sha256 = [string]$record.onnx.sha256
    }
    $coreRows.Add($row)
    $assetRows.Add([ordered]@{ path = $target; name = $assetName; kind = 'model'; modelId = [string]$entry.id; module = 'pp-ocr' })
}

foreach ($entry in @($documentCatalog.models)) {
    $recordPath = Get-RecordPath $PaddleDocumentRoot ([string]$entry.id)
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "Missing document acquisition record: $recordPath" }
    $record = Get-Content -Raw -LiteralPath $recordPath | ConvertFrom-Json
    if ([string]$record.status -notin @('onnx-existing','onnx-converted')) { continue }
    $source = Join-Path $PaddleDocumentRoot ('onnx\' + ([IO.Path]::GetFileName([string]$record.onnx.path)))
    $assetName = ([string]$entry.id -replace '/', '-') + '.onnx'
    $target = Join-Path $assetRoot $assetName
    if (-not $Check) { Copy-Required $source $target }
    $row = [ordered]@{
        assetName = $assetName; modelId = [string]$entry.id; family = 'pp-structure'; version = 'paddle3.0.0'
        module = [string]$entry.module; sourceArchive = [string]$record.sourceUrl; status = [string]$record.status
        size = [long]$record.onnx.size; sha256 = [string]$record.onnx.sha256; opset = if ($record.onnx.opset) { [int]$record.onnx.opset } else { $null }; profileFactory = $null
    }
    $documentRows.Add($row)
    $assetRows.Add([ordered]@{ path = $target; name = $assetName; kind = 'model'; modelId = [string]$entry.id; module = 'pp-structure' })
}

foreach ($dictionaryAssetName in $dictionaryAssets) {
    $source = Get-ChildItem -LiteralPath $ModelRoot -Recurse -File -Filter $dictionaryAssetName | Select-Object -First 1
    if (-not $source) { throw "Missing dictionary source: $dictionaryAssetName" }
    $target = Join-Path $assetRoot $dictionaryAssetName
    if (-not $Check) { Copy-Required $source.FullName $target }
    $assetRows.Add([ordered]@{ path = $target; name = $dictionaryAssetName; kind = 'dictionary'; modelId = $null; module = 'pp-ocr' })
}

if (-not [string]::IsNullOrWhiteSpace($Chart2TableTextGraphRoot) -or -not [string]::IsNullOrWhiteSpace($Chart2TableTokenizerRoot)) {
    if ([string]::IsNullOrWhiteSpace($Chart2TableTextGraphRoot) -or [string]::IsNullOrWhiteSpace($Chart2TableTokenizerRoot)) {
        throw 'Chart2Table release packaging requires both -Chart2TableTextGraphRoot and -Chart2TableTokenizerRoot.'
    }
    $visionSource = Join-Path $PaddleDocumentRoot 'chart-export\chart-vision.onnx'
    $textRoot = [IO.Path]::GetFullPath($Chart2TableTextGraphRoot)
    $tokenizerRoot = [IO.Path]::GetFullPath($Chart2TableTokenizerRoot)
    $bundleArtifacts = @(
        [pscustomobject]@{ Role = 'vision-projector'; ModelId = 'paddle-chart/pp-chart2table/vision-projector'; AssetName = 'pp-chart2table-vision-projector.onnx'; Format = 'onnx'; Source = $visionSource },
        [pscustomobject]@{ Role = 'token-embedding'; ModelId = 'paddle-chart/pp-chart2table/token-embedding'; AssetName = 'pp-chart2table-token-embedding.onnx'; Format = 'onnx'; Source = (Join-Path $textRoot 'chart-token-embedding-dynamic.onnx') },
        [pscustomobject]@{ Role = 'text-prefill'; ModelId = 'paddle-chart/pp-chart2table/text-prefill'; AssetName = 'pp-chart2table-text-prefill.onnx'; Format = 'onnx'; Source = $null },
        [pscustomobject]@{ Role = 'text-decode-with-past'; ModelId = 'paddle-chart/pp-chart2table/text-decode-with-past'; AssetName = 'pp-chart2table-text-decode-with-past.onnx'; Format = 'onnx'; Source = $null },
        [pscustomobject]@{ Role = 'qwen-tokenizer'; ModelId = 'paddle-chart/pp-chart2table'; AssetName = 'qwen.tiktoken'; Format = 'tiktoken'; Source = (Join-Path $tokenizerRoot 'qwen.tiktoken') },
        [pscustomobject]@{ Role = 'tokenizer-config'; ModelId = 'paddle-chart/pp-chart2table'; AssetName = 'tokenizer_config.json'; Format = 'json'; Source = (Join-Path $tokenizerRoot 'tokenizer_config.json') },
        [pscustomobject]@{ Role = 'added-tokens'; ModelId = 'paddle-chart/pp-chart2table'; AssetName = 'added_tokens.json'; Format = 'json'; Source = (Join-Path $tokenizerRoot 'added_tokens.json') }
    )
    foreach ($graph in @(
        [pscustomobject]@{ Role = 'text-prefill'; Stem = 'chart-text-prefill-full' },
        [pscustomobject]@{ Role = 'text-decode-with-past'; Stem = 'chart-text-decoder-dynamic-past-full' }
    )) {
        $descriptor = $bundleArtifacts | Where-Object Role -eq $graph.Role | Select-Object -First 1
        foreach ($candidate in @((Join-Path $textRoot ($graph.Stem + '-epsilon.onnx')), (Join-Path $textRoot ($graph.Stem + '.onnx')))) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $descriptor.Source = $candidate; break }
        }
        if ([string]::IsNullOrWhiteSpace($descriptor.Source)) { throw "Missing Chart2Table exported graph: $($graph.Role) under $textRoot" }
    }

    $releaseArtifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($descriptor in $bundleArtifacts) {
        if (-not (Test-Path -LiteralPath $descriptor.Source -PathType Leaf)) { throw "Missing Chart2Table release source: $($descriptor.Source)" }
        $target = Join-Path $assetRoot $descriptor.AssetName
        if (-not $Check) { Copy-Required $descriptor.Source $target }
        $integrityPath = if ($Check) { $descriptor.Source } else { $target }
        $size = (Get-Item -LiteralPath $integrityPath).Length
        $sha256 = Get-Sha256 $integrityPath
        $releaseArtifacts.Add([ordered]@{
            role = $descriptor.Role; modelId = $descriptor.ModelId; assetName = $descriptor.AssetName
            format = $descriptor.Format; size = [long]$size; sha256 = $sha256
        })
        $assetRows.Add([ordered]@{ path = $target; name = $descriptor.AssetName; kind = 'generation-bundle'; modelId = $descriptor.ModelId; module = 'chart-parsing' })
    }
    $chart2TableBundleRows.Add([ordered]@{
        bundleId = 'paddle-chart/pp-chart2table'; modelId = 'paddle-chart/pp-chart2table'; family = 'pp-structure-generative'
        artifacts = @($releaseArtifacts)
    })
}

if (-not [string]::IsNullOrWhiteSpace($PaddleOcrVlBundleRoot)) {
    $vlRoot = [IO.Path]::GetFullPath($PaddleOcrVlBundleRoot)
    $vlDescriptors = @(
        [pscustomobject]@{ Role = 'vision'; ModelId = 'paddleocr-vl/1.6/vision'; AssetName = 'paddleocr-vl-1.6-vision.onnx'; LocalName = 'vision.onnx'; Format = 'onnx' },
        [pscustomobject]@{ Role = 'embedding'; ModelId = 'paddleocr-vl/1.6/embedding'; AssetName = 'paddleocr-vl-1.6-embedding.onnx'; LocalName = 'embedding.onnx'; Format = 'onnx' },
        [pscustomobject]@{ Role = 'decoder'; ModelId = 'paddleocr-vl/1.6/decoder'; AssetName = 'paddleocr-vl-1.6-decoder.onnx'; LocalName = 'decoder.onnx'; Format = 'onnx' },
        [pscustomobject]@{ Role = 'head'; ModelId = 'paddleocr-vl/1.6/head'; AssetName = 'paddleocr-vl-1.6-head.onnx'; LocalName = 'head.onnx'; Format = 'onnx' },
        [pscustomobject]@{ Role = 'tokenizer-model'; ModelId = 'paddleocr-vl/1.6'; AssetName = 'paddleocr-vl-1.6-tokenizer.model'; LocalName = 'tokenizer.model'; Format = 'sentencepiece' },
        [pscustomobject]@{ Role = 'tokenizer-json'; ModelId = 'paddleocr-vl/1.6'; AssetName = 'paddleocr-vl-1.6-tokenizer.json'; LocalName = 'tokenizer.json'; Format = 'json' },
        [pscustomobject]@{ Role = 'tokenizer-config'; ModelId = 'paddleocr-vl/1.6'; AssetName = 'paddleocr-vl-1.6-tokenizer-config.json'; LocalName = 'tokenizer_config.json'; Format = 'json' },
        [pscustomobject]@{ Role = 'chat-template'; ModelId = 'paddleocr-vl/1.6'; AssetName = 'paddleocr-vl-1.6-chat-template.jinja'; LocalName = 'chat_template.jinja'; Format = 'jinja' },
        [pscustomobject]@{ Role = 'manifest'; ModelId = 'paddleocr-vl/1.6'; AssetName = 'paddleocr-vl-1.6-bundle.json'; LocalName = 'paddleocr-vl-bundle.json'; Format = 'json' }
    )
    $releaseArtifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($descriptor in $vlDescriptors) {
        $source = Join-Path $vlRoot $descriptor.LocalName
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing PaddleOCR-VL release source: $source" }
        $target = Join-Path $assetRoot $descriptor.AssetName
        if (-not $Check) { Copy-Required $source $target }
        $integrityPath = if ($Check) { $source } else { $target }
        $size = (Get-Item -LiteralPath $integrityPath).Length
        $sha256 = Get-Sha256 $integrityPath
        $releaseArtifacts.Add([ordered]@{
            role = $descriptor.Role; modelId = $descriptor.ModelId; assetName = $descriptor.AssetName; localName = $descriptor.LocalName
            format = $descriptor.Format; size = [long]$size; sha256 = $sha256
        })
        $assetRows.Add([ordered]@{ path = $target; name = $descriptor.AssetName; kind = 'generation-bundle'; modelId = $descriptor.ModelId; module = 'paddleocr-vl' })
    }
    $paddleOcrVlBundleRows.Add([ordered]@{
        bundleId = 'paddleocr-vl/1.6'; modelId = 'paddleocr-vl/1.6'; family = 'paddleocr-vl-generative'
        artifacts = @($releaseArtifacts)
    })
}

$releaseCatalog = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-collection-release-v3'
    collection = 'paddleocr-and-pp-structure'; tag = $Tag; repository = $Repository
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    corePaddleOcr = @($coreRows)
    ppStructure = @($documentRows)
    bundles = @($chart2TableBundleRows + $paddleOcrVlBundleRows)
    skipped = @($documentSummary.records | Where-Object { $_.status -notin @('onnx-existing','onnx-converted') } | ForEach-Object {
        [ordered]@{ modelId = [string]$_.id; module = [string]$_.module; status = [string]$_.status; error = [string]$_.error }
    })
    notes = @(
        'Each ONNX model and dictionary is a separate release asset; consumers can download only the required files.',
        'PP-Structure single-graph assets are limited to standard ONNX inference graphs; declared multi-file generative bundles list every graph and tokenizer asset with independent size/SHA-256 verification.',
        'PaddleOCR-VL 1.6 is an experimental four-graph Bundle; its catalog entry lists global Release asset names and Bundle-local filenames separately.',
        'The release tag is stable. New models are appended to this release and recorded in the catalog and release notes.'
    )
}

if (-not $Check) {
    $releaseCatalog | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $assetRoot 'paddleocr-release-catalog.json') -Encoding UTF8
    Copy-Required (Join-Path $ModelRoot 'paddle-ocr-acquisition-summary.json') (Join-Path $assetRoot 'paddleocr-acquisition-summary.json')
    Copy-Required (Join-Path $PaddleDocumentRoot 'acquisition-summary.json') (Join-Path $assetRoot 'paddle-document-acquisition-summary.json')
    $readme = @"
# DeploySharp PaddleOCR model collection

This stable release contains individual ONNX assets for the core PP-OCR v4/v5/v6 detector, recognizer and classifier models, plus the converted PP-Structure document models (layout, table, formula, unwarping, orientation and seal tasks).

Download only the assets needed by your pipeline. `paddleocr-release-catalog.json` lists every model, its module, SHA-256, source archive and DeploySharp profile status. `SHA256SUMS` covers all files in this release.

## Core PP-OCR

$(($coreRows | ForEach-Object { "- ``$($_.assetName)`` — $($_.version) / $($_.task) / $($_.name)" }) -join "`n")

## PP-Structure

$(($documentRows | ForEach-Object { "- ``$($_.assetName)`` — $($_.module) / $($_.modelId)" }) -join "`n")

## Multi-file generation bundles

$(($chart2TableBundleRows | ForEach-Object { "- ``$($_.bundleId)`` — $($_.artifacts.Count) independently downloadable ONNX/tokenizer assets; see the bundle entry in ``paddleocr-release-catalog.json``." }) -join "`n")

$(($paddleOcrVlBundleRows | ForEach-Object { "- ``$($_.bundleId)`` — $($_.artifacts.Count) independently downloadable ONNX/tokenizer/manifest assets; see the bundle entry in ``paddleocr-release-catalog.json``." }) -join "`n")

The official PP-OCRv6 pipeline does not publish a separate versioned CLS archive; the PP-LCNet text-line orientation classifier assets are listed under the v5 classifier entries and can be shared by the pipeline.
"@
    $readme | Set-Content -LiteralPath (Join-Path $assetRoot 'paddleocr-README.md') -Encoding UTF8
}

if ($IncludeLegacyBundles -and -not $Check) {
    foreach ($version in @('v4','v5','v6')) {
        $versionRoot = Join-Path $bundleRoot $version
        New-Item -ItemType Directory -Force -Path $versionRoot | Out-Null
        foreach ($row in @($coreRows | Where-Object version -eq $version)) {
            Copy-Required (Join-Path $assetRoot $row.assetName) (Join-Path $versionRoot $row.assetName)
        }
        $zipPath = Join-Path $assetRoot ('deploysharp-paddleocr-' + $version + '.zip')
        Compress-Archive -LiteralPath $versionRoot -DestinationPath $zipPath -CompressionLevel Optimal -Force
    }
}

$assetFiles = @()
if (-not $Check) {
    $assetFiles = @(Get-ChildItem -LiteralPath $assetRoot -File)
    $checksumLines = foreach ($file in ($assetFiles | Sort-Object Name)) { "$(Get-Sha256 $file.FullName)  $($file.Name)" }
    $checksumPath = Join-Path $assetRoot 'SHA256SUMS'
    ($checksumLines -join "`n") + "`n" | Set-Content -LiteralPath $checksumPath -Encoding ascii
    $assetFiles = @(Get-ChildItem -LiteralPath $assetRoot -File)
}

if ($Check) {
    Write-Output "DEPLOYSHARP_PADDLE_OCR_CHECK models=$($coreRows.Count + $documentRows.Count) core=$($coreRows.Count) ppStructure=$($documentRows.Count) stage=$stageRoot"
    return
}
Write-Output "DEPLOYSHARP_PADDLE_OCR_ASSETS_READY tag=$Tag core=$($coreRows.Count) ppStructure=$($documentRows.Count) assets=$($assetFiles.Count) stage=$stageRoot"
if (-not $Publish) { return }

$credential = ("protocol=https`nhost=github.com`n`n" | git credential fill)
$parts = @{}
foreach ($line in $credential) { $pair = $line -split '=',2; if ($pair.Count -eq 2) { $parts[$pair[0]] = $pair[1] } }
if ([string]::IsNullOrWhiteSpace($parts['password'])) { throw 'GitHub credentials are unavailable.' }
$headers = @{ Authorization = 'Bearer ' + $parts['password']; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'DeploySharp-paddleocr-release' }
$api = 'https://api.github.com/repos/' + $Repository
$uploadApi = 'https://uploads.github.com/repos/' + $Repository
$allReleases = Invoke-RestMethod -Headers $headers -Uri ($api + '/releases?per_page=100')
$release = $null
foreach ($candidate in $allReleases) {
    if ([string]$candidate.tag_name -eq $Tag) { $release = $candidate; break }
}
if (-not $release) {
    $target = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $request = [ordered]@{ tag_name = $Tag; target_commitish = $target; name = 'DeploySharp PaddleOCR and PP-Structure Models'; body = 'Individual PP-OCR v4/v5/v6 and PP-Structure ONNX assets. New model additions are appended to this stable release.'; draft = $false; prerelease = $true }
    $release = Invoke-RestMethod -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json) -Uri ($api + '/releases')
}
$releaseId = [long]$release.id
$remoteAssets = Invoke-RestMethod -Headers $headers -Uri ($api + '/releases/' + $releaseId + '/assets?per_page=100')
if ($PruneLegacyArchives) {
    $legacyNames = @('deploysharp-paddleocr-v4.zip','deploysharp-paddleocr-v5.zip','deploysharp-paddleocr-v6.zip')
    foreach ($legacy in @($remoteAssets | Where-Object { $legacyNames -contains $_.name })) {
        Invoke-RestMethod -Method Delete -Headers $headers -Uri ($api + '/releases/assets/' + [long]$legacy.id) | Out-Null
        Write-Output "removed-legacy-archive $($legacy.name)"
    }
    $remoteAssets = Invoke-RestMethod -Headers $headers -Uri ($api + '/releases/' + $releaseId + '/assets?per_page=100')
}
foreach ($file in ($assetFiles | Sort-Object Name)) {
    $existing = $remoteAssets | Where-Object { $_.name -eq $file.Name } | Select-Object -First 1
    $sha = Get-Sha256 $file.FullName
    if ($existing -and [long]$existing.size -eq [long]$file.Length -and [string]$existing.digest -eq ('sha256:' + $sha)) {
        Write-Output "verified-existing $($file.Name)"
        continue
    }
    if ($existing) { Invoke-RestMethod -Method Delete -Headers $headers -Uri ($api + '/releases/assets/' + [long]$existing.id) | Out-Null }
    $uploadUri = $uploadApi + '/releases/' + $releaseId + '/assets?name=' + [Uri]::EscapeDataString($file.Name)
    Write-Output "uploading name=$($file.Name) size=$($file.Length) uri=$uploadUri"
    & curl.exe --fail-with-body --silent --show-error --http1.1 --request POST --header ('Authorization: Bearer ' + $parts['password']) --header 'Accept: application/vnd.github+json' --header 'X-GitHub-Api-Version: 2022-11-28' --header 'Content-Type: application/octet-stream' --data-binary ('@' + $file.FullName) --output NUL $uploadUri
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $($file.Name)" }
    Write-Output "uploaded $($file.Name)"
}
$bodyLines = [System.Collections.Generic.List[string]]::new()
$bodyLines.Add("Individual PP-OCR v4/v5/v6 core assets and converted PP-Structure assets. Download only the required model, dictionary, or generation-bundle files. Generated $([DateTime]::UtcNow.ToString('yyyy-MM-dd')). See paddleocr-release-catalog.json and SHA256SUMS for the complete manifest. Skipped conversion-blocked models are listed in the catalog.")
if ($chart2TableBundleRows.Count -gt 0) {
    $chart2TableAssets = @($chart2TableBundleRows | ForEach-Object { $_.artifacts } | ForEach-Object { $_.assetName })
    $bodyLines.Add("Added $([DateTime]::UtcNow.ToString('yyyy-MM-dd')): PP-Chart2Table image-to-table generation bundle with four ONNX graphs and the official tokenizer assets. Download the required files listed under paddle-chart/pp-chart2table in the release catalog. Assets: " + ($chart2TableAssets -join ', ') + '.')
}
if ($paddleOcrVlBundleRows.Count -gt 0) {
    $vlAssets = @($paddleOcrVlBundleRows | ForEach-Object { $_.artifacts } | ForEach-Object { $_.assetName })
    $bodyLines.Add("Added $([DateTime]::UtcNow.ToString('yyyy-MM-dd')): experimental PaddleOCR-VL-1.6 four-graph ONNX generation bundle with the pinned tokenizer and manifest. Download the required files listed under paddleocr-vl/1.6 in the release catalog. Assets: " + ($vlAssets -join ', ') + '.')
}
$body = $bodyLines -join "`n`n"
$published = Invoke-RestMethod -Method Patch -Headers $headers -ContentType 'application/json' -Body (@{ name = 'DeploySharp PaddleOCR and PP-Structure Models'; draft = $false; prerelease = $true; body = $body } | ConvertTo-Json) -Uri ($api + '/releases/' + $releaseId)
$finalAssets = Invoke-RestMethod -Headers $headers -Uri ($api + '/releases/' + $releaseId + '/assets?per_page=100')
$finalAssetCount = @($finalAssets | ForEach-Object { $_ }).Count
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_PUBLISHED tag=$Tag url=$($published.html_url) remoteAssets=$finalAssetCount"
