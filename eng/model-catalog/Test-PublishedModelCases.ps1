[CmdletBinding()]
param(
    [string]$CatalogPath,
    [string]$CasesPath,
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$OutputPath,
    [string]$CachePath,
    [string]$ModelId,
    [switch]$UpdateReadmes,
    [switch]$DownloadAssets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($CatalogPath)) { $CatalogPath = Join-Path $repoRoot 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json' }
if ([string]::IsNullOrWhiteSpace($CasesPath)) { $CasesPath = Join-Path $repoRoot 'samples/06-models/cases' }
if ([string]::IsNullOrWhiteSpace($CachePath)) { $CachePath = Join-Path $repoRoot 'artifacts/model-case-verification' }
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $CachePath 'verification-results.json' }

if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) { throw "Catalog file not found: $CatalogPath" }
if (-not (Test-Path -LiteralPath $CasesPath -PathType Container)) { throw "Cases directory not found: $CasesPath" }
New-Item -ItemType Directory -Force -Path $CachePath | Out-Null

function Get-AssetName {
    param([object]$Asset)
    return [IO.Path]::GetFileName(([Uri]$Asset.downloadUrl).AbsolutePath)
}

function Get-CaseSlug {
    param([string]$Value)
    return $Value.Replace('/', '--')
}

function Normalize-Hash {
    param([string]$Value)
    return $Value.Replace('sha256:', '').ToLowerInvariant()
}

function Get-ExtensionValue {
    param([object]$Extensions, [string]$Name)
    $property = $Extensions.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return '' }
    return [string]$property.Value
}

function Assert-Equal {
    param([string]$Description, [object]$Expected, [object]$Actual)
    if ([string]$Expected -ne [string]$Actual) { throw "$Description mismatch. Expected '$Expected', got '$Actual'." }
}

function Get-Release {
    param([string]$Tag, [hashtable]$Headers)
    $uri = 'https://api.github.com/repos/' + $Repository.Trim('/') + '/releases/tags/' + [Uri]::EscapeDataString($Tag)
    $release = Invoke-RestMethod -Headers $Headers -Uri $uri -TimeoutSec 60
    if ([string]$release.tag_name -ne $Tag) { throw "GitHub returned an unexpected release tag for '$Tag'." }
    if ([bool]$release.draft -or -not [bool]$release.prerelease) { throw "Release '$Tag' is not an unpublished prerelease." }
    return $release
}

function Read-Manifest {
    param([object]$Asset, [string]$ManifestPath)
    Invoke-WebRequest -UseBasicParsing -Uri ([string]$Asset.downloadUrl) -OutFile $ManifestPath -TimeoutSec 120
    $file = Get-Item -LiteralPath $ManifestPath
    Assert-Equal "manifest size for $($Asset.assetId)" $Asset.size $file.Length
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath).Hash.ToLowerInvariant()
    Assert-Equal "manifest SHA256 for $($Asset.assetId)" (Normalize-Hash ([string]$Asset.sha256)) $hash
    return Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
}

function Assert-ManifestContract {
    param([object]$Entry, [object]$CatalogArtifact, [object]$Manifest)
    Assert-Equal 'ModelPack modelId' $Entry.modelId $Manifest.modelId
    $manifestArtifact = @($Manifest.artifacts | Where-Object { $_.artifactId -eq $CatalogArtifact.artifactId })
    if ($manifestArtifact.Count -ne 1) { throw "ModelPack artifact '$($CatalogArtifact.artifactId)' is missing or duplicated for '$($Entry.modelId)'." }
    $manifestFiles = @($manifestArtifact[0].files)
    foreach ($asset in @($CatalogArtifact.assets | Where-Object { $_.kind -ne 'manifest' })) {
        # Release catalog paths may be bundle-relative while ModelPack paths are entrypoint-relative.
        # Size and SHA256 are the stable identity across those two representations.
        $file = @($manifestFiles | Where-Object { $_.size -eq $asset.size -and (Normalize-Hash ([string]$_.sha256)) -eq (Normalize-Hash ([string]$asset.sha256)) })
        if ($file.Count -ne 1) { throw "ModelPack file '$($asset.relativePath)' is missing or duplicated for '$($Entry.modelId)'." }
        Assert-Equal "ModelPack size for $($asset.assetId)" $asset.size $file[0].size
        Assert-Equal "ModelPack SHA256 for $($asset.assetId)" (Normalize-Hash ([string]$asset.sha256)) (Normalize-Hash ([string]$file[0].sha256))
    }
    return $manifestArtifact[0]
}

function Download-AndVerifyAssets {
    param([object]$CatalogEntry, [object]$CatalogArtifact)
    $root = Join-Path (Join-Path $CachePath 'payload') (Get-CaseSlug $CatalogEntry.modelId)
    $root = Join-Path $root (Get-CaseSlug $CatalogArtifact.artifactId)
    foreach ($asset in @($CatalogArtifact.assets)) {
        $relative = ([string]$asset.relativePath).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $destination = Join-Path $root $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Invoke-WebRequest -UseBasicParsing -Uri ([string]$asset.downloadUrl) -OutFile $destination -TimeoutSec 1800
        $file = Get-Item -LiteralPath $destination
        Assert-Equal "downloaded size for $($asset.assetId)" $asset.size $file.Length
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant()
        Assert-Equal "downloaded SHA256 for $($asset.assetId)" (Normalize-Hash ([string]$asset.sha256)) $hash
    }
    return $root
}

function Write-VerificationSection {
    param([object]$Entry, [object[]]$ArtifactResults, [string]$AuditDate)
    $path = Join-Path $CasesPath ((Get-CaseSlug $Entry.modelId) + '\README.md')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "README is missing for '$($Entry.modelId)'." }
    $text = Get-Content -Raw -LiteralPath $path
    $section = @(
        '## Verification record',
        '',
        "Audit date: $AuditDate",
        "Catalog revision: $script:CatalogRevision",
        '',
        'Reproduce the release and ModelPack checks from the repository root:',
        '',
        '~~~powershell',
        ("pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -ModelId '$($Entry.modelId)' -UpdateReadmes" + $(if ($DownloadAssets) { ' -DownloadAssets' } else { '' })),
        '~~~',
        '',
        '| Check | Result | Details |',
        '| --- | --- | --- |',
        '| Official catalog selection | PASS | Exact model ID and artifact filters were selected from the immutable catalog. |',
        '| GitHub Release asset metadata | PASS | Every declared asset is uploaded and its size/SHA256 matches the catalog. |',
        '| ModelPack manifest download | PASS | Manifest HTTP download, byte size, SHA256, model ID, artifact ID, and declared file size/SHA256 identities passed. |'
    )
    if ($DownloadAssets) { $section += '| Full asset download and SHA256 | PASS | All manifest, model, license, and auxiliary assets were downloaded and verified. |' }
    else { $section += '| Full asset download and SHA256 | NOT RUN | Add -DownloadAssets for a local full-payload download; release metadata and the manifest were checked in this audit. |' }
    $section += ''
    $section += 'Published runtime evidence:'
    $section += ''
    foreach ($result in $ArtifactResults) {
        $detail = "$($result.ArtifactId): $($result.RuntimeEvidence)"
        if (-not [string]::IsNullOrWhiteSpace($result.Preprocessing)) { $detail += "; preprocessing=$($result.Preprocessing)" }
        if (-not [string]::IsNullOrWhiteSpace($result.Postprocessing)) { $detail += "; postprocessing=$($result.Postprocessing)" }
        if (-not [string]::IsNullOrWhiteSpace($result.GoldenSummary)) { $detail += "; golden=$($result.GoldenSummary)" }
        $section += "- $detail"
    }
    $section += ''
    $section += 'The runtime-evidence value is copied from the published ModelPack extension. It records the backend evidence attached to this release and is separate from the release-asset integrity checks above.'
    $sectionText = ($section -join [Environment]::NewLine).TrimEnd()
    $marker = [Environment]::NewLine + '## Verification record'
    $markerIndex = $text.IndexOf($marker, [StringComparison]::Ordinal)
    if ($markerIndex -ge 0) {
        $nextIndex = $text.IndexOf([Environment]::NewLine + '## ', $markerIndex + $marker.Length, [StringComparison]::Ordinal)
        if ($nextIndex -lt 0) { $nextIndex = $text.Length }
        $text = $text.Substring(0, $markerIndex) + [Environment]::NewLine + $sectionText + $text.Substring($nextIndex)
    } else {
        $text = $text.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $sectionText + [Environment]::NewLine
    }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

$headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'DeploySharp-model-case-audit' }
$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
$script:CatalogRevision = [string]$catalog.catalogRevision
$entries = @($catalog.entries | Where-Object { [string]::IsNullOrWhiteSpace($ModelId) -or $_.modelId -eq $ModelId })
if ($entries.Count -eq 0) { throw "No catalog entry matched '$ModelId'." }

$releaseCache = @{}
$results = New-Object System.Collections.Generic.List[object]
$auditDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
foreach ($entry in $entries | Sort-Object modelId) {
    $tag = [string]$entry.release.tag
    if (-not $releaseCache.ContainsKey($tag)) { $releaseCache[$tag] = Get-Release $tag $headers }
    $release = $releaseCache[$tag]
    $remoteByName = @{}
    foreach ($remote in @($release.assets | Where-Object { $_.state -eq 'uploaded' })) { $remoteByName[[string]$remote.name] = $remote }
    $artifactResults = New-Object System.Collections.Generic.List[object]
    foreach ($artifact in $entry.artifacts | Sort-Object artifactId) {
        $manifestAsset = @($artifact.assets | Where-Object { $_.kind -eq 'manifest' })
        if ($manifestAsset.Count -ne 1) { throw "Expected one manifest asset for '$($entry.modelId)/$($artifact.artifactId)'." }
        $manifestAsset = $manifestAsset[0]
        foreach ($asset in @($artifact.assets)) {
            $name = Get-AssetName $asset
            if (-not $remoteByName.ContainsKey($name)) { throw "Release '$tag' is missing '$name' for '$($entry.modelId)'." }
            $remote = $remoteByName[$name]
            Assert-Equal "release size for $name" $asset.size $remote.size
            Assert-Equal "release SHA256 for $name" (Normalize-Hash ([string]$asset.sha256)) (Normalize-Hash ([string]$remote.digest))
        }
        $manifestPath = Join-Path (Join-Path $CachePath 'manifests') ((Get-CaseSlug $entry.modelId) + '--' + (Get-CaseSlug $artifact.artifactId) + '.json')
        New-Item -ItemType Directory -Force -Path (Split-Path $manifestPath -Parent) | Out-Null
        $manifest = Read-Manifest $manifestAsset $manifestPath
        $manifestArtifact = Assert-ManifestContract $entry $artifact $manifest
        $payloadRoot = if ($DownloadAssets) { Download-AndVerifyAssets $entry $artifact } else { '' }
        $artifactResults.Add([pscustomobject]@{
            ModelId = [string]$entry.modelId
            ArtifactId = [string]$artifact.artifactId
            ReleaseTag = $tag
            RuntimeEvidence = Get-ExtensionValue $manifestArtifact.extensions 'deploysharp.validation-status'
            Preprocessing = Get-ExtensionValue $manifestArtifact.extensions 'deploysharp.preprocessing-version'
            Postprocessing = Get-ExtensionValue $manifestArtifact.extensions 'deploysharp.postprocessing-version'
            GoldenSummary = Get-ExtensionValue $manifestArtifact.extensions 'deploysharp.golden-summary'
            PayloadRoot = $payloadRoot
            ReleaseMetadata = 'PASS'
            Manifest = 'PASS'
            FullPayload = if ($DownloadAssets) { 'PASS' } else { 'NOT_RUN' }
        })
    }
    foreach ($artifactResult in $artifactResults) { $results.Add($artifactResult) }
    if ($UpdateReadmes) { Write-VerificationSection -Entry $entry -ArtifactResults $artifactResults.ToArray() -AuditDate $auditDate }
    Write-Output "DEPLOYSHARP_MODEL_CASE_VERIFIED model=$($entry.modelId) artifacts=$($artifactResults.Count) release=$tag manifest=PASS payload=$(if($DownloadAssets){'PASS'}else{'NOT_RUN'})"
}

$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$modelCount = @($results | Select-Object -ExpandProperty ModelId -Unique).Count
Write-Output "DEPLOYSHARP_MODEL_CASE_AUDIT_OK models=$modelCount artifacts=$($results.Count) catalog=$CatalogRevision output=$OutputPath"
