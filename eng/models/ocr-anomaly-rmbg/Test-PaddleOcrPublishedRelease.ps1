[CmdletBinding()]
param(
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$Tag = 'models-visual.1',
    [string]$DestinationDirectory = '',
    [switch]$SkipDownload
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
    $DestinationDirectory = Join-Path $repoRoot ('artifacts/model-release-public-audit-' + $Tag)
}
New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null

$headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'DeploySharp-public-release-audit'; 'X-GitHub-Api-Version' = '2022-11-28' }
$apiUri = 'https://api.github.com/repos/' + $Repository + '/releases/tags/' + [Uri]::EscapeDataString($Tag)
$release = (Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $apiUri).Content | ConvertFrom-Json
if ([string]$release.tag_name -ne $Tag -or [bool]$release.draft -or -not [bool]$release.prerelease) { throw "Public release state is not the expected prerelease: $Tag" }
function Get-ReleaseAssets {
    param([long]$ReleaseId)
    $items = @()
    for ($page = 1; $true; $page++) {
        $pageResponse = Invoke-RestMethod -Headers $headers -Uri ('https://api.github.com/repos/' + $Repository.Trim('/') + '/releases/' + $ReleaseId + '/assets?per_page=100&page=' + $page)
        $pageItems = if ($pageResponse -is [Array]) { [object[]]$pageResponse } else { @($pageResponse) }
        if ($pageItems.Count -eq 0) { break }
        foreach ($pageItem in $pageItems) { $items += $pageItem }
        if ($pageItems.Count -lt 100) { break }
    }
    # Emit each asset as an individual pipeline item.  Returning the array as a
    # single object makes a subsequent Where-Object see the array itself and
    # silently filters every asset out when a release has more than one page.
    return $items
}
$allAssets = @(Get-ReleaseAssets -ReleaseId ([long]$release.id))
$allAssets = @($allAssets | Where-Object { $_.state -eq 'uploaded' })
$expectedNames = @(
    'mobile-cls.modelpack.json', 'mobile-det.modelpack.json', 'mobile-rec.modelpack.json',
    'server-cls.modelpack.json', 'server-det.modelpack.json', 'server-rec.modelpack.json',
    'ppocrv5-mobile-cls.model.onnx', 'ppocrv5-mobile-det.model.onnx', 'ppocrv5-mobile-rec.model.onnx',
    'ppocrv5-server-cls.model.onnx', 'ppocrv5-server-det.model.onnx', 'ppocrv5-server-rec.model.onnx',
    'ppocrv5_dict.txt', 'SHA256SUMS'
)
$assetsByName = @{}
foreach ($asset in $allAssets) { $assetsByName[[string]$asset.name] = $asset }
$missingNames = @($expectedNames | Where-Object { -not $assetsByName.ContainsKey($_) })
if ($missingNames.Count -gt 0) { throw "Public release is missing PaddleOCR assets: $($missingNames -join ', ')." }
$assets = @($expectedNames | ForEach-Object { $assetsByName[$_] })

$checksumAsset = $assetsByName['SHA256SUMS']
if ($null -eq $checksumAsset) { throw 'Public release is missing SHA256SUMS.' }
$checksumPath = Join-Path $DestinationDirectory 'SHA256SUMS'
if (-not $SkipDownload -or -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    Invoke-WebRequest -UseBasicParsing -Uri ([string]$checksumAsset.browser_download_url) -OutFile $checksumPath
}
$checksumActual = (Get-FileHash -Algorithm SHA256 -LiteralPath $checksumPath).Hash.ToLowerInvariant()
if ([string]$checksumAsset.digest -ne ('sha256:' + $checksumActual) -or (Get-Item -LiteralPath $checksumPath).Length -ne [long]$checksumAsset.size) { throw 'Downloaded SHA256SUMS does not match the public GitHub asset metadata.' }
$checksumText = Get-Content -Raw -LiteralPath $checksumPath
$checksums = @{}
foreach ($line in ($checksumText -split "`r?`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^([0-9a-fA-F]{64})\s+\*?(.+)$') { throw "Invalid SHA256SUMS line: $line" }
    $checksums[$matches[2].Trim()] = $matches[1].ToLowerInvariant()
}
foreach ($name in ($expectedNames | Where-Object { $_ -ne 'SHA256SUMS' })) {
    if (-not $checksums.ContainsKey($name)) { throw "SHA256SUMS does not contain $name." }
}

$downloaded = 0
foreach ($asset in $assets | Where-Object name -ne 'SHA256SUMS') {
    $name = [string]$asset.name
    if (-not $checksums.ContainsKey($name)) { throw "SHA256SUMS does not contain $name." }
    if ([string]$asset.digest -ne ('sha256:' + $checksums[$name])) { throw "GitHub digest mismatch for $name." }
    $path = Join-Path $DestinationDirectory $name
    if (-not $SkipDownload -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Invoke-WebRequest -UseBasicParsing -Uri ([string]$asset.browser_download_url) -OutFile $path
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($actual -ne $checksums[$name]) { throw "Downloaded SHA256 mismatch for $name." }
    if ((Get-Item -LiteralPath $path).Length -ne [long]$asset.size) { throw "Downloaded size mismatch for $name." }
    $downloaded++
}

foreach ($manifestName in @('mobile-det.modelpack.json','server-det.modelpack.json','mobile-rec.modelpack.json','server-rec.modelpack.json','mobile-cls.modelpack.json','server-cls.modelpack.json')) {
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $DestinationDirectory $manifestName) | ConvertFrom-Json
    if ([string]$manifest.extensions.'deploysharp.release-tag' -ne $Tag -or -not [bool]$manifest.source.redistributionAllowed) { throw "Published manifest admission mismatch: $manifestName" }
}

Write-Output "DEPLOYSHARP_PADDLE_OCR_PUBLISHED_RELEASE_OK tag=$Tag paddleOcrAssets=$($assets.Count) releaseAssets=$($allAssets.Count) downloaded=$downloaded directory=$DestinationDirectory"
