[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,
    [Parameter(Mandatory = $true)]
    [string]$CacheDirectory,
    [string]$SeedPackageCache,
    [string]$EvidenceDirectory,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packages = (Resolve-Path -LiteralPath $PackageDirectory).Path
$work = [IO.Path]::GetFullPath($WorkingDirectory)
$cache = [IO.Path]::GetFullPath($CacheDirectory)
$seedCache = if ([string]::IsNullOrWhiteSpace($SeedPackageCache)) { $null } else { (Resolve-Path -LiteralPath $SeedPackageCache).Path }
$releaseEvidence = $null
$releasePackageEvidence = @{}
$releasePackageAssemblyHashes = @{}
$managedDependencyEvidence = @{}
$nativeRuntimeEvidence = @{}
if (-not [string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $evidenceRoot = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
    $evidencePath = Join-Path $evidenceRoot 'package-provenance-sbom.json'
    $releaseEvidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json -AsHashtable
    if ($releaseEvidence.schemaVersion -ne '1.0' -or $releaseEvidence.format.standard -ne 'custom') { throw 'Release evidence schema/format is invalid.' }
    foreach ($component in @($releaseEvidence.releasePackages)) {
        $packageId = [string]$component.id
        $releasePackageEvidence[$packageId] = $component
        $packagePath = Join-Path $packages "$packageId.$($component.version).nupkg"
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "Release-evidence package is missing from the consumer source: $packageId." }
        $hashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            foreach ($entry in @($archive.Entries | Where-Object { $_.FullName -match "^lib/[^/]+/$([regex]::Escape($packageId))\.dll$" })) {
                $stream = $entry.Open()
                $algorithm = [Security.Cryptography.SHA256]::Create()
                try { [void]$hashes.Add(([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()) }
                finally { $algorithm.Dispose(); $stream.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        if ($hashes.Count -eq 0) { throw "Release-evidence package has no managed assembly payload: $packageId." }
        $releasePackageAssemblyHashes[$packageId] = $hashes
    }
    foreach ($component in @($releaseEvidence.managedDependencies)) { $managedDependencyEvidence["$($component.id)/$($component.version)"] = $component }
    foreach ($component in @($releaseEvidence.consumerOwnedNativeRuntimes)) { $nativeRuntimeEvidence["$($component.id)/$($component.version)"] = $component }
}
if (Test-Path -LiteralPath $work) { throw "Consumer working directory already exists: $work" }
if (Test-Path -LiteralPath $cache) { throw "Consumer package cache already exists: $cache" }
New-Item -ItemType Directory -Path $work, $cache | Out-Null
$offlineSource = $null
if ($null -ne $seedCache) {
    $offlineSource = Join-Path $work 'offline-packages'
    New-Item -ItemType Directory -Path $offlineSource | Out-Null
    $seededIdentities = @{}
    foreach ($lockFile in Get-ChildItem -LiteralPath (Join-Path $repository 'tests\clean-consumer') -Recurse -Filter 'packages.lock.json') {
        $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json -AsHashtable
        foreach ($framework in $lock.dependencies.Values) {
            foreach ($dependency in $framework.GetEnumerator()) {
                $id = [string]$dependency.Key
                $version = [string]$dependency.Value.resolved
                if ($id -like 'JYPPX.DeploySharp.*' -or [string]::IsNullOrWhiteSpace($version)) { continue }
                $seededIdentities["$($id.ToLowerInvariant())/$($version.ToLowerInvariant())"] = @($id.ToLowerInvariant(), $version.ToLowerInvariant())
            }
        }
    }
    foreach ($seedPackage in Get-ChildItem -LiteralPath $seedCache -Recurse -Filter '*.nupkg' -File) {
        $version = $seedPackage.Directory.Name.ToLowerInvariant()
        $id = $seedPackage.Directory.Parent.Name.ToLowerInvariant()
        if ($id -like 'jyppx.deploysharp.*') { continue }
        $seededIdentities["$id/$version"] = @($id, $version)
    }
    foreach ($identity in $seededIdentities.Values) {
        $sourceVersionRoot = Join-Path (Join-Path $seedCache $identity[0]) $identity[1]
        if (-not (Test-Path -LiteralPath $sourceVersionRoot -PathType Container)) { throw "Locked package is missing from the offline seed cache: $($identity[0])/$($identity[1])" }
        $destinationIdRoot = Join-Path $cache $identity[0]
        New-Item -ItemType Directory -Path $destinationIdRoot -Force | Out-Null
        Copy-Item -LiteralPath $sourceVersionRoot -Destination $destinationIdRoot -Recurse
        # NuGet's global cache can retain a case-variant duplicate of the
        # package file. Prefer the canonical id.version filename so a valid
        # cache is not rejected merely because that duplicate is present.
        $canonicalName = "$($identity[0]).$($identity[1]).nupkg"
        $sourcePackages = @(Get-ChildItem -LiteralPath $sourceVersionRoot -Filter '*.nupkg' -File |
            Where-Object { $_.Name -ieq $canonicalName })
        if ($sourcePackages.Count -ne 1) {
            throw "Offline seed package is missing its canonical nupkg: $($identity[0])/$($identity[1])"
        }
        Copy-Item -LiteralPath $sourcePackages[0].FullName -Destination $offlineSource
    }
}
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
    if ($null -ne $offlineSource) {
        $writer.WriteStartElement('add'); $writer.WriteAttributeString('key', 'offline-seed'); $writer.WriteAttributeString('value', $offlineSource); $writer.WriteEndElement()
    } else {
        $writer.WriteStartElement('add'); $writer.WriteAttributeString('key', 'nuget.org'); $writer.WriteAttributeString('value', 'https://api.nuget.org/v3/index.json'); $writer.WriteAttributeString('protocolVersion', '3'); $writer.WriteEndElement()
    }
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
                if (-not $releasePackageAssemblyHashes[$packageId].Contains($outputSha256)) {
                    throw "Consumer $name output assembly differs from every managed DLL in the validated package: $packageId.dll."
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
$dependencySource = if ($null -eq $seedCache) { 'nuget.org' } else { 'offline-seeded-cache' }
Write-Output "DEPLOYSHARP_CLEAN_CONSUMER_MATRIX_OK projects=$($projects.Count) passed=$passed skipped-external=$skipped blocked-external=$blocked package-source=stage35-local dependency-source=$dependencySource evidence=$evidenceStatus"
