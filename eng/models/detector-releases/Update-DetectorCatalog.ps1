[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ReleaseCommit,
    [string]$CatalogPath = 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json',
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$planPath = Join-Path $PSScriptRoot 'detector-release-assets.json'
$plan = Get-Content -Raw -LiteralPath $planPath | ConvertFrom-Json
$catalogFile = Join-Path $repositoryRoot $CatalogPath
$catalog = Get-Content -Raw -LiteralPath $catalogFile | ConvertFrom-Json

function Get-AssetName {
    param([object]$PlanEntry, [object]$File)
    if ($File.role -eq 'license') { return $PlanEntry.collection + '.' + $PlanEntry.licenseSlug + '.LICENSE.txt' }
    return $PlanEntry.modelId.Replace('/', '-') + '.' + [IO.Path]::GetFileName([string]$File.relativePath)
}

function Get-AssetId {
    param([object]$PlanEntry, [object]$File)
    $prefix = $PlanEntry.modelId.Replace('/', '-')
    if ($File.role -eq 'license') { return $prefix + '-license' }
    return $prefix + '-' + ([IO.Path]::GetFileName([string]$File.relativePath).Replace('.', '-'))
}

function Get-AssetKind {
    param([object]$File)
    if ($File.role -eq 'license') { return 'license' }
    if ($File.role -in @('model', 'weights')) { return 'model' }
    return 'other'
}

$knownIds = @{}
foreach ($entry in $plan.models) { $knownIds[[string]$entry.modelId] = $true }
$retained = @($catalog.entries | Where-Object { -not $knownIds.ContainsKey([string]$_.modelId) })
$generated = [System.Collections.Generic.List[object]]::new()

foreach ($planEntry in $plan.models) {
    $manifestPath = Join-Path $repositoryRoot ('eng/models/' + $planEntry.manifestFile)
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $artifact = @($manifest.artifacts)[0]
    $manifestFile = Get-Item -LiteralPath $manifestPath
    $prefix = ([string]$planEntry.modelId).Replace('/', '-')
    $tag = [string]$planEntry.tag
    $releaseBase = 'https://github.com/guojin-yan/DeploySharp/releases/download/' + $tag + '/'
    $assets = [System.Collections.Generic.List[object]]::new()
    $assets.Add([ordered]@{
        assetId = $prefix + '-modelpack'
        kind = 'manifest'
        releaseTag = $tag
        downloadUrl = $releaseBase + $manifestFile.Name
        relativePath = $manifestFile.Name
        size = [long]$manifestFile.Length
        sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        mediaType = 'application/json'
        licenseExpression = [string]$planEntry.licenseExpression
    })
    foreach ($file in $artifact.files) {
        $assets.Add([ordered]@{
            assetId = Get-AssetId $planEntry $file
            kind = Get-AssetKind $file
            releaseTag = $tag
            downloadUrl = $releaseBase + (Get-AssetName $planEntry $file)
            relativePath = [string]$file.relativePath
            size = [long]$file.size
            sha256 = ([string]$file.sha256).ToLowerInvariant()
            mediaType = [string]$file.mediaType
            licenseExpression = [string]$planEntry.licenseExpression
        })
    }

    $documentationPath = if ($planEntry.collection -eq 'yolo') { 'articles/visual-yolo-detection.md' } else { 'articles/visual-portable-detectors.md' }
    $generated.Add([ordered]@{
        modelId = [string]$manifest.modelId
        name = [string]$manifest.name
        family = [string]$manifest.family
        task = [string]$manifest.task
        modelVersion = [string]$manifest.modelVersion
        status = 'preview'
        description = 'Public alpha-preview ' + $planEntry.collection.ToUpperInvariant() + ' bundle with source, license, SHA-256, and immutable GitHub Release assets.'
        source = $manifest.source
        release = [ordered]@{ owner = 'guojin-yan'; repository = 'DeploySharp'; tag = $tag; commit = $ReleaseCommit }
        artifacts = @([ordered]@{
            artifactId = [string]$artifact.artifactId
            format = [string]$artifact.format
            compatibleBackends = @($artifact.compatibleBackends)
            precision = [string]$artifact.precision
            quantization = [string]$artifact.quantization
            portable = [bool]$artifact.portable
            manifestAssetId = $prefix + '-modelpack'
            bundleRole = 'detector'
            bundleVersion = [string]$manifest.modelVersion
            capabilities = @([string]$manifest.task)
            assets = $assets
            conversion = [ordered]@{
                exporter = [string]$manifest.exporter.name
                exporterVersion = [string]$manifest.exporter.version
                sourceRevision = [string]$manifest.exporter.sourceRevision
                notes = 'Source and runtime validation details are recorded in the released ModelPack.'
            }
        })
        testInputs = @()
        documentationPath = $documentationPath
    })
}

$document = [ordered]@{
    schemaVersion = [string]$catalog.schemaVersion
    generatedAt = [string]$catalog.generatedAt
    catalogRevision = 'models-20260817.detectors.1'
    sourceRepository = [string]$catalog.sourceRepository
    entries = @($retained) + @($generated)
}
$content = ($document | ConvertTo-Json -Depth 40) + [Environment]::NewLine

if ($Check) {
    if ((Get-Content -Raw -LiteralPath $catalogFile) -ne $content) { throw "Generated detector catalog is stale: $catalogFile" }
} else {
    [IO.File]::WriteAllText($catalogFile, $content, [Text.UTF8Encoding]::new($false))
}
