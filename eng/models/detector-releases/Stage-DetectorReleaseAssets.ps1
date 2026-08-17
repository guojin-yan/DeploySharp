[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model',
    [string]$OutputRoot = 'artifacts',
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$plan = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'detector-release-assets.json') | ConvertFrom-Json

function Get-ReleaseAssetName {
    param([object]$PlanEntry, [object]$File)
    if ($File.role -eq 'license') { return $PlanEntry.collection + '.' + $PlanEntry.licenseSlug + '.LICENSE.txt' }
    return $PlanEntry.modelId.Replace('/', '-') + '.' + [IO.Path]::GetFileName([string]$File.relativePath)
}

function Copy-CheckedFile {
    param([string]$SourcePath, [string]$DestinationPath, [object]$File)
    if (-not (Test-Path -LiteralPath $SourcePath)) { throw "Missing staged source file: $SourcePath" }
    $source = Get-Item -LiteralPath $SourcePath
    $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($source.Length -ne [long]$File.size -or $sourceHash -ne [string]$File.sha256) { throw "Staged source integrity mismatch: $SourcePath" }
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    $destination = Get-Item -LiteralPath $DestinationPath
    $destinationHash = (Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($destination.Length -ne [long]$File.size -or $destinationHash -ne [string]$File.sha256) { throw "Staged destination integrity mismatch: $DestinationPath" }
}

foreach ($collection in $plan.collections) {
    $entries = @($plan.models | Where-Object collection -eq $collection.id)
    $stageDirectory = Join-Path $repositoryRoot (Join-Path $OutputRoot ('model-release-' + $collection.id + '-20260817'))
    $assetPlanPath = Join-Path $stageDirectory 'release-assets.json'

    if ($Check) {
        if (-not (Test-Path -LiteralPath $assetPlanPath)) { throw "Missing staged asset plan: $assetPlanPath" }
        $assetPlan = Get-Content -Raw -LiteralPath $assetPlanPath | ConvertFrom-Json
        foreach ($asset in $assetPlan.assets) {
            $path = Join-Path $stageDirectory $asset.name
            if (-not (Test-Path -LiteralPath $path)) { throw "Missing staged release asset: $path" }
            $item = Get-Item -LiteralPath $path
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($item.Length -ne [long]$asset.size -or $hash -ne [string]$asset.sha256) { throw "Staged release asset integrity mismatch: $path" }
        }
        continue
    }

    [IO.Directory]::CreateDirectory($stageDirectory) | Out-Null
    $assetRecords = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $entries) {
        $manifestPath = Join-Path $repositoryRoot ('eng/models/' + $entry.manifestFile)
        $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        $manifestTarget = Join-Path $stageDirectory ([IO.Path]::GetFileName($manifestPath))
        Copy-Item -LiteralPath $manifestPath -Destination $manifestTarget -Force
        $manifestItem = Get-Item -LiteralPath $manifestTarget
        $assetRecords.Add([ordered]@{ name = $manifestItem.Name; size = [long]$manifestItem.Length; sha256 = (Get-FileHash -LiteralPath $manifestTarget -Algorithm SHA256).Hash.ToLowerInvariant() })

        foreach ($file in @($manifest.artifacts[0].files)) {
            $targetName = Get-ReleaseAssetName $entry $file
            $targetPath = Join-Path $stageDirectory $targetName
            if ($file.role -eq 'license') {
                $sourcePath = Join-Path $PSScriptRoot ('licenses\' + $entry.licenseSlug + '.txt')
            } else {
                $sourcePath = Join-Path $ModelRoot ([string]$entry.localPath)
                if ([IO.Path]::GetFileName([string]$file.relativePath) -eq 'model.bin' -and [IO.Path]::GetExtension($sourcePath) -eq '.xml') {
                    $sourcePath = [IO.Path]::ChangeExtension($sourcePath, '.bin')
                }
            }
            Copy-CheckedFile $sourcePath $targetPath $file
            if (-not $assetRecords.Exists([Predicate[object]]{ param($asset) $asset.name -eq $targetName })) {
                $targetItem = Get-Item -LiteralPath $targetPath
                $assetRecords.Add([ordered]@{ name = $targetItem.Name; size = [long]$targetItem.Length; sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant() })
            }
        }
    }

    $checksumPath = Join-Path $stageDirectory 'SHA256SUMS'
    $checksumLines = @($assetRecords | Sort-Object name | ForEach-Object { $_.sha256 + '  ' + $_.name })
    [IO.File]::WriteAllText($checksumPath, (($checksumLines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    $checksumItem = Get-Item -LiteralPath $checksumPath
    $assetRecords.Add([ordered]@{ name = $checksumItem.Name; size = [long]$checksumItem.Length; sha256 = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256).Hash.ToLowerInvariant() })

    $assetPlan = [ordered]@{ collection = $collection.id; tag = $collection.tag; assets = $assetRecords }
    [IO.File]::WriteAllText($assetPlanPath, (($assetPlan | ConvertTo-Json -Depth 10) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}
