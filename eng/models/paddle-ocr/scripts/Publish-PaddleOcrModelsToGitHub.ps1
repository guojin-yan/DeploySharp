[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\paddleocr',
    [string]$OutputRoot = 'artifacts',
    [string]$Tag = 'models-paddleocr',
    [string]$Repository = 'guojin-yan/DeploySharp',
    [switch]$Check,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$catalogPath = Join-Path $PSScriptRoot '..\paddle-ocr-models.json'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$summaryPath = Join-Path $ModelRoot 'paddle-ocr-acquisition-summary.json'
if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) { throw "Run Acquire-PaddleOcrModels.ps1 first: $summaryPath" }
$summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
$stageRoot = Join-Path $repositoryRoot (Join-Path $OutputRoot 'model-release-paddleocr')
$zipRoot = Join-Path $stageRoot 'assets'
$bundleRoot = Join-Path $stageRoot 'bundle'
if (-not $Check) {
    if (Test-Path -LiteralPath $stageRoot -PathType Container) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $zipRoot, $bundleRoot | Out-Null
}

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Copy-Required([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing model release source: $Source" }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

$rows = @()
foreach ($entry in @($catalog.models)) {
    $recordPath = Join-Path $ModelRoot ('records\' + (($entry.id -replace '/', '-') + '.json'))
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "Missing acquisition record: $recordPath" }
    $record = Get-Content -Raw -LiteralPath $recordPath | ConvertFrom-Json
    if ([string]$record.status -notin @('onnx-existing','onnx-converted')) { throw "Model is not release-ready: $($entry.id) status=$($record.status)" }
    $onnxSource = Join-Path $ModelRoot ([string]$entry.directory + '\' + [string]$entry.onnx)
    $versionFolder = Join-Path $bundleRoot ([string]$entry.version)
    $modelTarget = Join-Path $versionFolder ('models\' + [string]$entry.directory + '\' + [string]$entry.onnx)
    if (-not $Check) { Copy-Required $onnxSource $modelTarget }
    $dictionaryTarget = $null
    if ($entry.dictionary) {
        $dictionarySource = Join-Path $ModelRoot ([string]$entry.dictionary)
        $dictionaryTarget = Join-Path $versionFolder ('dictionaries\' + [IO.Path]::GetFileName([string]$entry.dictionary))
        if (-not $Check) { Copy-Required $dictionarySource $dictionaryTarget }
    }
    $rows += [ordered]@{
        id = [string]$entry.id; version = [string]$entry.version; family = [string]$entry.family; name = [string]$entry.name
        input = [string]$entry.input; output = [string]$entry.output; profileFactory = [string]$entry.codeProfile
        onnxRelativePath = 'models/' + ([string]$entry.directory).Replace('\','/') + '/' + [string]$entry.onnx
        dictionaryRelativePath = if ($dictionaryTarget) { 'dictionaries/' + [IO.Path]::GetFileName([string]$entry.dictionary) } else { $null }
        onnxSha256 = [string]$record.onnx.sha256; onnxSize = [long]$record.onnx.size; status = [string]$record.status
    }
}

$releaseCatalog = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-release-v1'; collection = 'paddleocr'; tag = $Tag; repository = $Repository
    generatedAtUtc = [DateTime]::UtcNow.ToString('o'); sourceCatalog = 'eng/models/paddle-ocr/paddle-ocr-models.json'; models = @($rows)
    notes = @('The catalog describes code-level Profile support and local ONNX assets.', 'PP-OCRv6 has no separate versioned CLS archive; use the catalogued PP-LCNet text-line orientation classifier.')
}
$catalogAsset = Join-Path $bundleRoot 'paddleocr-release-catalog.json'
if (-not $Check) { $releaseCatalog | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $catalogAsset -Encoding UTF8 }
$readme = @"
# DeploySharp PaddleOCR model collection

This release contains the core PP-OCR v4/v5/v6 ONNX assets used by DeploySharp. Every row has a matching Profile factory in `PaddleOcrModelCatalog` and a SHA-256 entry in `SHA256SUMS`.

| Version | Family | Model | Profile factory |
|---|---|---|---|
$(($rows | ForEach-Object { "| $($_.version) | $($_.family) | $($_.name) | ``$($_.profileFactory)`` |" }) -join "`n")

The archives are generated from the official PaddleOCR inference model URLs recorded in the repository catalog. The v6 pipeline does not publish a separate CLS model; the official PP-LCNet text-line orientation classifier is shared by the pipeline.
"@
if (-not $Check) { $readme | Set-Content -LiteralPath (Join-Path $bundleRoot 'README.md') -Encoding UTF8 }

foreach ($version in @('v4','v5','v6')) {
    $sourceDirectory = Join-Path $bundleRoot $version
    $zipPath = Join-Path $zipRoot ('deploysharp-paddleocr-' + $version + '.zip')
    if (-not $Check) {
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
        Compress-Archive -LiteralPath $sourceDirectory -DestinationPath $zipPath -CompressionLevel Optimal
    }
}
if (-not $Check) {
    Copy-Item -LiteralPath $catalogAsset -Destination (Join-Path $zipRoot 'paddleocr-release-catalog.json') -Force
    Copy-Item -LiteralPath (Join-Path $ModelRoot 'paddle-ocr-acquisition-summary.json') -Destination (Join-Path $zipRoot 'paddleocr-acquisition-summary.json') -Force
    Copy-Item -LiteralPath (Join-Path $bundleRoot 'README.md') -Destination (Join-Path $zipRoot 'paddleocr-README.md') -Force
}
$assetRecords = @()
foreach ($file in Get-ChildItem -LiteralPath $zipRoot -File) { $assetRecords += [ordered]@{ name = $file.Name; size = $file.Length; sha256 = Get-Sha256 $file.FullName } }
$checksumPath = Join-Path $zipRoot 'SHA256SUMS'
$checksumContent = (($assetRecords | Sort-Object name | ForEach-Object { $_.sha256 + '  ' + $_.name }) -join "`n") + "`n"
if (-not $Check) { $checksumContent | Set-Content -LiteralPath $checksumPath -Encoding ascii }
if ($Check) { $assetRecords = @(Get-ChildItem -LiteralPath $zipRoot -File | ForEach-Object { [ordered]@{ name = $_.Name; size = $_.Length; sha256 = Get-Sha256 $_.FullName } }) }
if (-not $Check) { $checksumItem = Get-Item -LiteralPath $checksumPath; $assetRecords += [ordered]@{ name = $checksumItem.Name; size = $checksumItem.Length; sha256 = Get-Sha256 $checksumItem.FullName } }
Write-Output "DEPLOYSHARP_PADDLE_OCR_ASSETS_READY tag=$Tag models=$($rows.Count) assets=$(@($assetRecords).Count) stage=$stageRoot"
if (-not $Publish) { return }

$credential = ("protocol=https`nhost=github.com`n`n" | git credential fill)
$parts = @{}
foreach ($line in $credential) { $pair = $line -split '=',2; if ($pair.Count -eq 2) { $parts[$pair[0]] = $pair[1] } }
if ([string]::IsNullOrWhiteSpace($parts['password'])) { throw 'GitHub credentials are unavailable.' }
$headers = @{ Authorization = 'Bearer ' + $parts['password']; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'DeploySharp-paddleocr-release' }
$api = 'https://api.github.com/repos/' + $Repository
$upload = 'https://uploads.github.com/repos/' + $Repository
$all = @(Invoke-RestMethod -Headers $headers -Uri ($api + '/releases?per_page=100'))
$release = $all | Where-Object { $_.tag_name -eq $Tag } | Select-Object -First 1
if ($release -and -not $release.draft) { throw "Release '$Tag' already exists and is published. Use a new immutable tag." }
if (-not $release) {
    $target = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $request = [ordered]@{ tag_name = $Tag; target_commitish = $target; name = 'DeploySharp PaddleOCR Models'; body = "Core PP-OCR v4/v5/v6 ONNX collection. Upload date: $([DateTime]::UtcNow.ToString('yyyy-MM-dd')). New model additions will be appended to this release note and catalog."; draft = $true; prerelease = $true }
    $release = Invoke-RestMethod -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json) -Uri ($api + '/releases')
}
$remoteAssets = @(Invoke-RestMethod -Headers $headers -Uri ($api + '/releases/' + $release.id + '/assets?per_page=100'))
foreach ($asset in $assetRecords) {
    $local = Join-Path $zipRoot $asset.name
    $existing = $remoteAssets | Where-Object name -eq $asset.name | Select-Object -First 1
    if ($existing -and $existing.size -eq $asset.size) { Write-Output "verified-existing $($asset.name)"; continue }
    if ($existing) { Invoke-RestMethod -Method Delete -Headers $headers -Uri ($api + '/releases/assets/' + $existing.id) | Out-Null }
    $uploadUri = $upload + '/releases/' + $release.id + '/assets?name=' + [Uri]::EscapeDataString($asset.name)
    & curl.exe --fail-with-body --silent --show-error --http1.1 --request POST --header ('Authorization: Bearer ' + $parts['password']) --header 'Accept: application/vnd.github+json' --header 'X-GitHub-Api-Version: 2022-11-28' --header 'Content-Type: application/octet-stream' --data-binary ('@' + $local) --output NUL $uploadUri
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $($asset.name)" }
    Write-Output "uploaded $($asset.name)"
}
$published = Invoke-RestMethod -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"prerelease":true}' -Uri ($api + '/releases/' + $release.id)
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_PUBLISHED tag=$Tag url=$($published.html_url)"
