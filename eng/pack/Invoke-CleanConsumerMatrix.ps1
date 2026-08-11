[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,
    [Parameter(Mandatory = $true)]
    [string]$CacheDirectory,
    [string]$EvidenceDirectory,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packages = (Resolve-Path -LiteralPath $PackageDirectory).Path
$work = [IO.Path]::GetFullPath($WorkingDirectory)
$cache = [IO.Path]::GetFullPath($CacheDirectory)
$releaseEvidence = $null
$releasePackageEvidence = @{}
$managedDependencyEvidence = @{}
$nativeRuntimeEvidence = @{}
if (-not [string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $evidenceRoot = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
    $evidencePath = Join-Path $evidenceRoot 'package-provenance-sbom.json'
    $releaseEvidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json -AsHashtable
    if ($releaseEvidence.schemaVersion -ne '1.0' -or $releaseEvidence.format.standard -ne 'custom') { throw 'Release evidence schema/format is invalid.' }
    foreach ($component in @($releaseEvidence.releasePackages)) { $releasePackageEvidence[[string]$component.id] = $component }
    foreach ($component in @($releaseEvidence.managedDependencies)) { $managedDependencyEvidence["$($component.id)/$($component.version)"] = $component }
    foreach ($component in @($releaseEvidence.consumerOwnedNativeRuntimes)) { $nativeRuntimeEvidence["$($component.id)/$($component.version)"] = $component }
}
if (Test-Path -LiteralPath $work) { throw "Consumer working directory already exists: $work" }
if (Test-Path -LiteralPath $cache) { throw "Consumer package cache already exists: $cache" }
New-Item -ItemType Directory -Path $work, $cache | Out-Null
$stagedConsumerRoot = Join-Path $work 'tests\clean-consumer'
$stagedAssets = Join-Path $work 'tests\assets'
New-Item -ItemType Directory -Path $stagedConsumerRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'tests\assets') -Destination $stagedAssets -Recurse

$settings = [Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$configPath = Join-Path $work 'NuGet.Config'
$writer = [Xml.XmlWriter]::Create($configPath, $settings)
try {
    $writer.WriteStartDocument()
    $writer.WriteStartElement('configuration')
    $writer.WriteStartElement('packageSources')
    $writer.WriteStartElement('clear'); $writer.WriteEndElement()
    $writer.WriteStartElement('add'); $writer.WriteAttributeString('key', 'stage35-local'); $writer.WriteAttributeString('value', $packages); $writer.WriteEndElement()
    $writer.WriteStartElement('add'); $writer.WriteAttributeString('key', 'nuget.org'); $writer.WriteAttributeString('value', 'https://api.nuget.org/v3/index.json'); $writer.WriteAttributeString('protocolVersion', '3'); $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndElement()
    $writer.WriteEndDocument()
}
finally { $writer.Dispose() }

function Test-NativeRuntimePackage {
    param([string]$Id)
    return $Id -like 'LLamaSharp.Backend.*' -or
        $Id -eq 'Microsoft.ML.OnnxRuntime' -or
        $Id -like 'OpenVINO.runtime*' -or
        $Id -like 'JYPPX.OpenCV.runtime*'
}

$environmentBackup = @{}
foreach ($item in Get-ChildItem Env: | Where-Object { $_.Name -like 'DEPLOYSHARP_*' }) {
    $environmentBackup[$item.Name] = $item.Value
    [Environment]::SetEnvironmentVariable($item.Name, $null, 'Process')
}

$passed = 0
$skipped = 0
$blocked = 0
$projects = @(Get-ChildItem -LiteralPath (Join-Path $repository 'tests\clean-consumer') -Recurse -Filter '*.csproj' | Sort-Object FullName)
$expectedExternalBlocks = @('visual-portable-detectors', 'visual-yolo-detection', 'visual-yolo-multitask')
try {
    foreach ($sourceProject in $projects) {
        $name = $sourceProject.Directory.Name
        $projectWork = Join-Path $stagedConsumerRoot $name
        New-Item -ItemType Directory -Path $projectWork | Out-Null
        foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceProject.Directory.FullName -File) {
            Copy-Item -LiteralPath $sourceFile.FullName -Destination $projectWork
        }
        $stagedProjects = @(Get-ChildItem -LiteralPath $projectWork -Filter '*.csproj')
        if ($stagedProjects.Count -ne 1) { throw "Consumer $name must stage exactly one project." }
        $project = $stagedProjects[0]
        $obj = Join-Path $project.Directory.FullName 'obj'
        $properties = @()

        $restoreOutput = & dotnet restore $project.FullName --configfile $configPath --packages $cache --force-evaluate @properties 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Consumer restore failed for $name.`n$restoreOutput" }
        $assetsPath = Join-Path $obj 'project.assets.json'
        if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) { throw "Consumer assets missing for $name." }
        $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
        if ($null -ne $releaseEvidence) {
            foreach ($libraryProperty in @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' })) {
                $identity = [string]$libraryProperty.Name
                $parts = $identity -split '/', 2
                if ($parts[0] -like 'JYPPX.DeploySharp.*') {
                    if (-not $releasePackageEvidence.ContainsKey($parts[0]) -or [string]$releasePackageEvidence[$parts[0]].version -ne $parts[1]) { throw "Consumer $name restored DeploySharp component outside release evidence: $identity." }
                }
                elseif (Test-NativeRuntimePackage $parts[0]) {
                    if (-not $nativeRuntimeEvidence.ContainsKey($identity) -or $nativeRuntimeEvidence[$identity].ownership -ne 'consumer-owned-native-runtime') { throw "Consumer $name restored native component outside consumer-owned evidence: $identity." }
                }
                elseif (-not $managedDependencyEvidence.ContainsKey($identity)) {
                    throw "Consumer $name restored managed component missing from release evidence: $identity."
                }
            }
        }
        $deploySharpLibraries = @($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'JYPPX.DeploySharp.*/*' })
        if ($deploySharpLibraries.Count -eq 0) { throw "Consumer $name restored no DeploySharp package." }
        foreach ($library in $deploySharpLibraries) {
            $parts = $library.Split('/')
            if ($parts[1] -ne '2.0.0-alpha.1') { throw "Consumer $name restored unexpected DeploySharp version: $library" }
            $metadataPath = Join-Path $cache "$($parts[0].ToLowerInvariant())\$($parts[1])\.nupkg.metadata"
            if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw "NuGet source metadata missing for $library." }
            $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
            if (-not [string]::Equals([IO.Path]::GetFullPath([string]$metadata.source).TrimEnd('\'), $packages.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
                throw "Consumer $name did not restore $library from the Stage 35 package directory."
            }
        }

        [xml]$projectXml = Get-Content -LiteralPath $project.FullName -Raw
        $directNative = @($projectXml.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { [string]$_.Include } | Where-Object { Test-NativeRuntimePackage $_ })
        $assetNative = @($assets.libraries.PSObject.Properties.Name | ForEach-Object { ($_ -split '/')[0] } | Where-Object { Test-NativeRuntimePackage $_ } | Sort-Object -Unique)
        if ($name -like '*missing-native*' -and $assetNative.Count -ne 0) { throw "Missing-native consumer $name restored native packages: $($assetNative -join ',')." }
        if ($name -notlike '*missing-native*' -and $directNative.Count -gt 0 -and $assetNative.Count -eq 0) { throw "Consumer-owned native package disappeared from $name assets." }

        $buildOutput = & dotnet build $project.FullName -c Release --no-restore @properties 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Consumer build failed for $name.`n$buildOutput" }
        if ($null -ne $releaseEvidence) {
            $consumerFrameworks = @($assets.project.frameworks.PSObject.Properties.Name)
            if ($consumerFrameworks.Count -ne 1) { throw "Consumer $name must have one restored target framework for output evidence." }
            $outputRoot = Join-Path $project.Directory.FullName "bin\Release\$($consumerFrameworks[0])"
            foreach ($identity in $deploySharpLibraries) {
                $packageId = ($identity -split '/', 2)[0]
                $outputAssembly = Join-Path $outputRoot "$packageId.dll"
                if (-not (Test-Path -LiteralPath $outputAssembly -PathType Leaf)) { throw "Consumer $name output is missing release-evidence assembly: $packageId.dll." }
                $outputSha256 = (Get-FileHash -LiteralPath $outputAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
                if (@($releasePackageEvidence[$packageId].frameworks | Where-Object { $_.assemblySha256 -eq $outputSha256 }).Count -eq 0) {
                    throw "Consumer $name output assembly differs from every packaged TFM in release evidence: $packageId.dll."
                }
            }
        }
        $runOutput = & dotnet run --project $project.FullName -c Release --no-build --no-restore @properties 2>&1 | Out-String
        $runExit = $LASTEXITCODE
        $status = $null
        if ($runExit -eq 0 -and $runOutput -match 'SKIP') {
            $status = 'skipped-external'
            $skipped++
        }
        elseif ($runExit -eq 0) {
            $status = 'passed'
            $passed++
        }
        elseif ($expectedExternalBlocks -contains $name) {
            $status = 'blocked-external'
            $blocked++
        }
        else {
            throw "Consumer run failed unexpectedly for $name with exit $runExit.`n$runOutput"
        }
        $marker = @($runOutput -split "`r?`n" | Where-Object { $_ -match 'DEPLOYSHARP_|package-only|passed' } | Select-Object -Last 1)
        $markerText = if ($marker.Count -eq 0) { 'none' } else { ($marker[0] -replace '\s+', '-') }
        $evidenceStatus = if ($null -eq $releaseEvidence) { 'not-requested' } else { 'validated' }
        Write-Output "DEPLOYSHARP_CLEAN_CONSUMER_MATRIX_ITEM name=$name status=$status packages=$($deploySharpLibraries.Count) direct-native=$($directNative.Count) asset-native=$($assetNative.Count) evidence=$evidenceStatus marker=$markerText"
    }
}
finally {
    foreach ($item in Get-ChildItem Env: | Where-Object { $_.Name -like 'DEPLOYSHARP_*' }) { [Environment]::SetEnvironmentVariable($item.Name, $null, 'Process') }
    foreach ($name in $environmentBackup.Keys) { [Environment]::SetEnvironmentVariable($name, $environmentBackup[$name], 'Process') }
}

$evidenceStatus = if ($null -eq $releaseEvidence) { 'not-requested' } else { 'validated' }
Write-Output "DEPLOYSHARP_CLEAN_CONSUMER_MATRIX_OK projects=$($projects.Count) passed=$passed skipped-external=$skipped blocked-external=$blocked package-source=stage35-local evidence=$evidenceStatus"
