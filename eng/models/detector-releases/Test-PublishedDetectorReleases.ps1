[CmdletBinding()]
param(
    [string]$CatalogPath = 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json',
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$Token
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$catalogFile = Join-Path $repositoryRoot $CatalogPath
$catalog = Get-Content -Raw -LiteralPath $catalogFile | ConvertFrom-Json
$headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'DeploySharp-detector-release-audit' }
if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers.Authorization = 'Bearer ' + $Token }

function Get-ReleaseAssetName {
    param([object]$Asset)
    return [IO.Path]::GetFileName(([Uri]$Asset.downloadUrl).AbsolutePath)
}

function Add-ExpectedAsset {
    param([hashtable]$Expected, [object]$Asset)
    $name = Get-ReleaseAssetName $Asset
    $record = [ordered]@{
        name = $name
        size = [long]$Asset.size
        digest = 'sha256:' + ([string]$Asset.sha256).ToLowerInvariant()
    }
    if ($Expected.ContainsKey($name)) {
        $prior = $Expected[$name]
        if ($prior.size -ne $record.size -or $prior.digest -ne $record.digest) { throw "Catalog maps release asset '$name' to conflicting integrity metadata." }
        return
    }
    $Expected[$name] = $record
}

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
    # Emit each asset as an individual pipeline item so callers can enumerate
    # releases with more than GitHub's default first page of assets.
    return $items
}

foreach ($tag in @('models-visual.1')) {
    $entries = @($catalog.entries | Where-Object { $_.release.tag -eq $tag })
    if ($entries.Count -eq 0) { throw "The official catalog contains no entries for release '$tag'." }

    $releaseUri = 'https://api.github.com/repos/' + $Repository.Trim('/') + '/releases/tags/' + [Uri]::EscapeDataString($tag)
    $release = Invoke-RestMethod -Headers $headers -Uri $releaseUri
    if ($release.tag_name -ne $tag -or $release.draft -or -not $release.prerelease) { throw "Release state is invalid for '$tag'." }

    $expected = @{}
    foreach ($entry in $entries) {
        foreach ($artifact in @($entry.artifacts)) {
            foreach ($asset in @($artifact.assets)) {
                if ($asset.releaseTag -ne $tag) { throw "Catalog asset '$($asset.assetId)' does not retain release tag '$tag'." }
                Add-ExpectedAsset $expected $asset
            }
        }
    }

    $remote = @{}
    foreach ($asset in @(Get-ReleaseAssets -ReleaseId ([long]$release.id))) {
        if ($remote.ContainsKey([string]$asset.name)) { throw "Release '$tag' has duplicate asset name '$($asset.name)'." }
        $remote[[string]$asset.name] = $asset
    }

    $checksumAsset = $remote['SHA256SUMS']
    if ($null -eq $checksumAsset -or $checksumAsset.state -ne 'uploaded' -or [string]::IsNullOrWhiteSpace([string]$checksumAsset.digest)) { throw "Release '$tag' is missing an uploaded SHA256SUMS asset with a digest." }
    if ($remote.Count -ne ($expected.Count + 1)) { throw "Release '$tag' asset count is $($remote.Count), expected $($expected.Count + 1) including SHA256SUMS." }

    foreach ($record in $expected.Values) {
        $actual = $remote[$record.name]
        if ($null -eq $actual) { throw "Release '$tag' is missing catalog asset '$($record.name)'." }
        if ($actual.state -ne 'uploaded' -or [long]$actual.size -ne $record.size -or [string]$actual.digest -ne $record.digest) {
            throw "Release '$tag' integrity metadata mismatch for '$($record.name)'."
        }
    }

    $sumResponse = Invoke-WebRequest -Headers $headers -Uri ([string]$checksumAsset.browser_download_url)
    $sumContent = if ($sumResponse.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($sumResponse.Content) } else { [string]$sumResponse.Content }
    $checksums = @{}
    foreach ($line in ($sumContent -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^(?<hash>[0-9a-f]{64})  (?<name>.+)$') { throw "Release '$tag' contains an invalid SHA256SUMS line: $line" }
        if ($checksums.ContainsKey($Matches.name)) { throw "Release '$tag' SHA256SUMS repeats '$($Matches.name)'." }
        $checksums[$Matches.name] = $Matches.hash
    }

    if ($checksums.Count -ne $expected.Count) { throw "Release '$tag' SHA256SUMS contains $($checksums.Count) records, expected $($expected.Count)." }
    foreach ($record in $expected.Values) {
        $hash = $checksums[$record.name]
        if ($null -eq $hash -or ('sha256:' + $hash) -ne $record.digest) { throw "Release '$tag' SHA256SUMS mismatch for '$($record.name)'." }
    }

    Write-Host ("{0}: {1} catalog models, {2} release assets, all uploaded and SHA-256 verified." -f $tag, $entries.Count, $remote.Count)
}
