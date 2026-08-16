[CmdletBinding()]
param(
    [string]$PackageRoot,
    [string]$SourceRepository = $env:DEPLOYSHARP_TENSORRT_SOURCE_REPOSITORY,
    [string]$EvidencePath = (Join-Path $PSScriptRoot 'evidence\tensorrt-4.0.0-admission.blocked.json'),
    [switch]$RequireAdmitted
)

$ErrorActionPreference = 'Stop'

$packageId = 'JYPPX.TensorRT.CSharp.API'
$packageVersion = '4.0.0'
$repositoryUrl = 'https://github.com/guojin-yan/TensorRT-CSharp-API'
$repositoryCommit = '673e120807d789d90a13a9f28a043282e95bb5e6'
$expectedTfms = @('net10.0', 'net46', 'net461', 'net462', 'net47', 'net471', 'net472', 'net48', 'net481', 'net5.0', 'net6.0', 'net7.0', 'net8.0', 'net9.0', 'netcoreapp3.1')
$expectedAssemblies = @('JYPPX.Shared', 'JYPPX.CudaSharp', 'JYPPX.TensorRtSharp')
$requiredMembers = @(
    'M:JYPPX.TensorRtSharp.TensorRtBuilder.BuildSerializedNetwork(JYPPX.TensorRtSharp.TensorRtNetworkDefinition,JYPPX.TensorRtSharp.TensorRtBuilderConfig)',
    'M:JYPPX.TensorRtSharp.TensorRtRuntime.Deserialize(System.Byte[])',
    'M:JYPPX.TensorRtSharp.TensorRtEngine.CreateExecutionContext',
    'M:JYPPX.TensorRtSharp.TensorRtExecutionContext.EnqueueAsync(JYPPX.CudaSharp.CudaStream)',
    'M:JYPPX.TensorRtSharp.TensorRtExecutionContext.SetInputShape(System.String,JYPPX.TensorRtSharp.TensorRtDims)',
    'M:JYPPX.TensorRtSharp.TensorRtOptimizationProfile.SetShape(System.String,JYPPX.TensorRtSharp.TensorRtOptimizationProfileSelector,JYPPX.TensorRtSharp.TensorRtDims)'
)

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $nugetRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { Join-Path $env:USERPROFILE '.nuget\packages' } else { $env:NUGET_PACKAGES }
    $PackageRoot = Join-Path $nugetRoot 'jyppx.tensorrt.csharp.api\4.0.0'
}

if ([string]::IsNullOrWhiteSpace($SourceRepository)) {
    $defaultSource = 'E:\GitSpace\TensorRT-CSharp-API-4.0\TensorRtSharp4.0'
    if (Test-Path -LiteralPath $defaultSource -PathType Container) { $SourceRepository = $defaultSource }
}

$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$EvidencePath = [IO.Path]::GetFullPath($EvidencePath)

function Add-Blocker {
    param([System.Collections.Generic.List[string]]$List, [string]$Code)
    if (-not $List.Contains($Code)) { $List.Add($Code) }
}

function Assert-ExactSet {
    param([object[]]$Expected, [object[]]$Actual, [string]$Label)
    $expectedSet = @($Expected | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $actualSet = @($Actual | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $expectedSet -DifferenceObject $actualSet)
    if ($expectedSet.Count -ne $actualSet.Count -or $difference.Count -ne 0) {
        throw "$Label drift: expected [$($expectedSet -join ',')], actual [$($actualSet -join ',')]."
    }
}

function Get-Sha512Base64 {
    param([string]$Path)
    $algorithm = [Security.Cryptography.SHA512]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [Convert]::ToBase64String($algorithm.ComputeHash($stream)) }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

function Get-StreamSha256 {
    param([IO.Stream]$Stream)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-ZipEntrySha256 {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $stream = $Entry.Open()
    try { return Get-StreamSha256 $stream }
    finally { $stream.Dispose() }
}

function Get-ZipEntryText {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $reader = [IO.StreamReader]::new($Entry.Open(), [Text.Encoding]::UTF8, $true)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Get-AssemblyMetadata {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $entryStream = $Entry.Open()
    $memory = [IO.MemoryStream]::new()
    try {
        $entryStream.CopyTo($memory)
        $memory.Position = 0
        $pe = [Reflection.PortableExecutable.PEReader]::new($memory)
        try {
            $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $definition = $reader.GetAssemblyDefinition()
            $references = @($reader.AssemblyReferences | ForEach-Object {
                $reference = $reader.GetAssemblyReference($_)
                $reader.GetString($reference.Name)
            } | Sort-Object -Unique)
            return [pscustomobject]@{
                name = $reader.GetString($definition.Name)
                version = [string]$definition.Version
                references = $references
            }
        }
        finally { $pe.Dispose() }
    }
    finally {
        $memory.Dispose()
        $entryStream.Dispose()
    }
}

function Get-GitText {
    param([string]$Repository, [string]$ObjectPath)
    $value = & git -C $Repository show $ObjectPath 2>$null | Out-String
    if ($LASTEXITCODE -ne 0) { return $null }
    return $value.TrimEnd("`r", "`n")
}

if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) { throw "TensorRT package cache directory is missing: $PackageRoot" }
if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) { throw "TensorRT admission evidence is missing: $EvidencePath" }

$evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -AsHashtable
if ([string]$evidence.schemaVersion -ne '1.0' -or [string]$evidence.format -ne 'DeploySharpTensorRtPackageAdmission') { throw 'Unsupported TensorRT admission evidence format.' }
if ([string]$evidence.package.id -ne $packageId -or [string]$evidence.package.version -ne $packageVersion) { throw 'TensorRT evidence package ID or version drifted.' }
if ([string]$evidence.source.repositoryUrl -ne $repositoryUrl -or [string]$evidence.source.commit -ne $repositoryCommit) { throw 'TensorRT evidence source identity drifted.' }
Assert-ExactSet $expectedTfms @($evidence.package.tfms) 'TensorRT evidence TFM set'
Assert-ExactSet $expectedAssemblies @($evidence.package.managedAssemblies | ForEach-Object { $_.name }) 'TensorRT evidence managed assembly set'

$nupkgPath = Join-Path $PackageRoot "$($packageId.ToLowerInvariant()).$packageVersion.nupkg"
$nuspecPath = Join-Path $PackageRoot "$($packageId.ToLowerInvariant()).nuspec"
$shaPath = "$nupkgPath.sha512"
$metadataPath = Join-Path $PackageRoot '.nupkg.metadata'
foreach ($path in @($nupkgPath, $nuspecPath, $shaPath, $metadataPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "TensorRT package cache file is missing: $path" }
}

$nupkgItem = Get-Item -LiteralPath $nupkgPath
$nupkgSha256 = (Get-FileHash -LiteralPath $nupkgPath -Algorithm SHA256).Hash.ToLowerInvariant()
$nupkgSha512 = Get-Sha512Base64 $nupkgPath
$recordedSha512 = (Get-Content -LiteralPath $shaPath -Raw).Trim()
$cacheMetadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($recordedSha512 -ne $nupkgSha512) { throw 'TensorRT nupkg SHA512 sidecar does not match the cached nupkg.' }
if ([string]$evidence.package.sha512Base64 -ne $nupkgSha512) { throw 'TensorRT retained SHA512 drifted from the audited package.' }
if ([string]$cacheMetadata.contentHash -ne [string]$evidence.package.contentHash) { throw 'TensorRT NuGet contentHash drifted from retained evidence.' }
if ([string]$evidence.package.sha256 -ne $nupkgSha256 -or [int64]$evidence.package.bytes -ne $nupkgItem.Length) { throw 'TensorRT retained package identity drifted from the audited cache.' }
if ([string]$evidence.package.cacheSource -ne [string]$cacheMetadata.source) { throw 'TensorRT retained cache source drifted from NuGet metadata.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata
$archive = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
$blockers = [System.Collections.Generic.List[string]]::new()
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) {
        if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate TensorRT NuGet entry: $($entry.FullName)" }
        $entries.Add($entry.FullName, $entry)
    }

    $nuspecName = "$packageId.nuspec"
    if (-not $entries.ContainsKey($nuspecName)) { throw 'TensorRT package root nuspec is missing.' }
    $embeddedNuspecText = Get-ZipEntryText $entries[$nuspecName]
    $extractedNuspecText = Get-Content -LiteralPath $nuspecPath -Raw
    if (-not [string]::Equals($embeddedNuspecText, $extractedNuspecText, [StringComparison]::Ordinal)) { throw 'TensorRT cached and embedded nuspec content differs.' }

    [xml]$embeddedNuspec = $embeddedNuspecText
    $metadata = $embeddedNuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($null -eq $metadata) { throw 'TensorRT package nuspec metadata is missing.' }
    $idNode = $metadata.SelectSingleNode("*[local-name()='id']")
    $versionNode = $metadata.SelectSingleNode("*[local-name()='version']")
    if ($null -eq $idNode -or $null -eq $versionNode -or [string]$idNode.InnerText -ne $packageId -or [string]$versionNode.InnerText -ne $packageVersion) { throw 'TensorRT package ID or version drifted.' }

    $declaredLicenseFile = $null
    $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
    $licenseUrlNode = $metadata.SelectSingleNode("*[local-name()='licenseUrl']")
    if ($null -eq $licenseNode -or [string]::IsNullOrWhiteSpace([string]$licenseNode.InnerText)) {
        if ($null -ne $licenseUrlNode -and -not [string]::IsNullOrWhiteSpace([string]$licenseUrlNode.InnerText)) { Add-Blocker $blockers 'package-license-url-not-admitted' }
        else { Add-Blocker $blockers 'package-license-metadata-missing' }
    }
    elseif ([string]$licenseNode.type -eq 'expression') {
        if ([string]$licenseNode.InnerText -notin @('Apache-2.0', 'BSD-3-Clause', 'MIT')) { Add-Blocker $blockers 'package-license-expression-not-admitted' }
    }
    elseif ([string]$licenseNode.type -eq 'file') {
        $declaredLicenseFile = ([string]$licenseNode.InnerText).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($declaredLicenseFile) -or [IO.Path]::IsPathRooted($declaredLicenseFile) -or $declaredLicenseFile -match '(^|/)\.\.(/|$)' -or -not $entries.ContainsKey($declaredLicenseFile)) {
            Add-Blocker $blockers 'package-license-file-invalid'
        }
        else { Add-Blocker $blockers 'package-license-file-manual-review-required' }
    }
    else { Add-Blocker $blockers 'package-license-type-not-admitted' }

    $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
    if ($null -eq $repositoryNode -or [string]$repositoryNode.type -ne 'git' -or [string]$repositoryNode.url -ne $repositoryUrl -or [string]$repositoryNode.commit -ne $repositoryCommit) { Add-Blocker $blockers 'package-repository-metadata-drift' }

    $dependencyGroups = @($metadata.SelectNodes("*[local-name()='dependencies']/*[local-name()='group']"))
    Assert-ExactSet @('net8.0') @($dependencyGroups | ForEach-Object { [string]$_.targetFramework }) 'TensorRT nuspec dependency group set'
    if (@($dependencyGroups | ForEach-Object { $_.SelectNodes("*[local-name()='dependency']") }).Count -ne 0) { throw 'TensorRT package has an unexpected NuGet dependency.' }
    Assert-ExactSet @($evidence.package.nuspec.dependencyGroups) @($dependencyGroups | ForEach-Object { [string]$_.targetFramework }) 'TensorRT evidence dependency group set'

    $allowedEntries = @($nuspecName, 'README.md', 'logo.jpg', '.signature.p7s', '_rels/.rels', '[Content_Types].xml')
    if (-not [string]::IsNullOrWhiteSpace($declaredLicenseFile) -and $entries.ContainsKey($declaredLicenseFile)) { $allowedEntries += $declaredLicenseFile }
    $coreProperties = @($archive.Entries | Where-Object { $_.FullName -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' })
    if ($coreProperties.Count -ne 1) { throw "TensorRT package core-properties entry count drifted: $($coreProperties.Count)." }
    $allowedEntries += $coreProperties[0].FullName
    foreach ($tfm in $expectedTfms) {
        foreach ($assembly in $expectedAssemblies) {
            $allowedEntries += "lib/$tfm/$assembly.dll"
            $allowedEntries += "lib/$tfm/$assembly.xml"
        }
    }
    Assert-ExactSet $allowedEntries @($entries.Keys) 'TensorRT strict NuGet payload'

    $tfms = @($archive.Entries | Where-Object { $_.FullName -match '^lib/([^/]+)/[^/]+\.dll$' } | ForEach-Object { [regex]::Match($_.FullName, '^lib/([^/]+)/').Groups[1].Value } | Sort-Object -Unique)
    Assert-ExactSet $expectedTfms $tfms 'TensorRT package TFM set'
    $managedEntries = @($archive.Entries | Where-Object { $_.FullName -match '^lib/[^/]+/JYPPX\.(Shared|CudaSharp|TensorRtSharp)\.dll$' })
    if ($managedEntries.Count -ne 45 -or [int]$evidence.package.payload.managedDlls -ne 45) { throw "TensorRT managed DLL count drifted: $($managedEntries.Count)." }
    $nativeEntries = @($archive.Entries | Where-Object { $_.FullName -match '(^|/)(runtimes|native)(/|$)|\.(dll\.a|so|dylib|lib|engine|plan|onnx|gguf)$' })
    if ($nativeEntries.Count -ne 0) { throw "TensorRT package contains native/engine/model payload: $($nativeEntries.FullName -join ',')." }
    $licenseEntries = @($archive.Entries | Where-Object { $_.FullName -match '(^|/)(license|copying|notice)(\.|$)' })
    if ($licenseEntries.Count -ne [int]$evidence.package.payload.licenseEntries) { throw "TensorRT package license payload count drifted: $($licenseEntries.Count)." }
    $signatureEntries = @($archive.Entries | Where-Object { $_.FullName -eq '.signature.p7s' -or $_.FullName -match '^package/services/digital-signature/' })
    if ([string]$evidence.package.signature -ne 'repository-signed / dotnet nuget verify --all passed' -or $signatureEntries.Count -ne 1) { throw 'TensorRT package signature state drifted from the retained repository-signed result.' }

    foreach ($tfm in $expectedTfms) {
        foreach ($assembly in $expectedAssemblies) {
            $entry = $entries["lib/$tfm/$assembly.dll"]
            $assemblyMetadata = Get-AssemblyMetadata $entry
            if ($assemblyMetadata.name -ne $assembly) { throw "TensorRT assembly identity drifted at $($entry.FullName): $($assemblyMetadata.name)." }
            $record = @($evidence.package.managedAssemblies | Where-Object { [string]$_.name -eq $assembly })
            if ($record.Count -ne 1 -or $assemblyMetadata.version -ne [string]$record[0].assemblyVersion) { throw "TensorRT assembly version drifted at $($entry.FullName): $($assemblyMetadata.version)." }
            $unexpectedReferences = @($assemblyMetadata.references | Where-Object {
                $_ -notin $expectedAssemblies -and $_ -ne 'mscorlib' -and $_ -ne 'netstandard' -and $_ -notmatch '^(System|Microsoft)(\.|$)'
            })
            if ($unexpectedReferences.Count -ne 0) { throw "TensorRT managed dependency closure contains unexpected references at $($entry.FullName): $($unexpectedReferences -join ',')." }
            if ($tfm -eq 'net8.0') {
                if ((Get-ZipEntrySha256 $entry) -ne [string]$record[0].sha256 -or [int64]$entry.Length -ne [int64]$record[0].net8Bytes) { throw "TensorRT net8 assembly identity drifted: $assembly." }
            }
        }

        $xmlText = Get-ZipEntryText $entries["lib/$tfm/JYPPX.TensorRtSharp.xml"]
        foreach ($member in $requiredMembers) {
            $needle = '<member name="' + $member + '"'
            if ($xmlText.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "TensorRT API member missing from $tfm XML contract: $member" }
        }
    }
}
finally { $archive.Dispose() }

$sourceCommit = [string]$evidence.source.commit
if ([string]::IsNullOrWhiteSpace($SourceRepository) -or -not (Test-Path -LiteralPath $SourceRepository -PathType Container)) {
    Add-Blocker $blockers 'source-repository-unavailable'
}
else {
    & git -C $SourceRepository cat-file -e "$sourceCommit^{commit}" 2>$null
    $commitAvailable = $LASTEXITCODE -eq 0
    if (-not $commitAvailable) { Add-Blocker $blockers 'source-commit-unavailable' }

    $originUrl = (& git -C $SourceRepository remote get-url origin 2>$null | Out-String).Trim().TrimEnd('/')
    if ($originUrl.EndsWith('.git', [StringComparison]::OrdinalIgnoreCase)) { $originUrl = $originUrl.Substring(0, $originUrl.Length - 4) }
    if ($originUrl -ne $repositoryUrl) { Add-Blocker $blockers 'source-repository-url-mismatch' }

    if ($commitAvailable) {
        $localTagCommit = (& git -C $SourceRepository rev-parse 'refs/tags/v4.0.0^{commit}' 2>$null | Out-String).Trim()
        $localTagAvailable = $LASTEXITCODE -eq 0
        if ($localTagAvailable -and $localTagCommit -ne $sourceCommit) { Add-Blocker $blockers 'formal-v4.0.0-tag-commit-mismatch' }
        $policyText = Get-GitText $SourceRepository "$sourceCommit`:pack/publication-license-policy.json"
        if ([string]::IsNullOrWhiteSpace($policyText)) {
            Add-Blocker $blockers 'source-license-policy-unavailable'
        }
        else {
            $policy = $policyText | ConvertFrom-Json
            if ([string]$policy.ownerDecisionState -ne 'approved' -or [string]::IsNullOrWhiteSpace([string]$policy.selectedPackageLicense.value) -or [string]::IsNullOrWhiteSpace([string]$policy.selectedSourceArchiveLicenseFileName)) { Add-Blocker $blockers 'source-license-owner-decision-required' }
        }
    }
}

$cacheSource = [string]$cacheMetadata.source
if ($cacheSource -ne 'https://api.nuget.org/v3/index.json') { Add-Blocker $blockers 'cache-source-not-official-nuget-org' }

$releaseBinding = $evidence.releaseBinding
$releaseIdentityMatches = (
    [string]$releaseBinding.tag -eq 'v4.0.0' -and
    [string]$releaseBinding.tagCommit -eq $sourceCommit -and
    [string]$releaseBinding.repositoryUrl -eq $repositoryUrl -and
    [bool]$releaseBinding.immutable -and
    [string]$releaseBinding.boundPackage.sha256 -eq $nupkgSha256 -and
    [string]$releaseBinding.boundPackage.sha512Base64 -eq $nupkgSha512 -and
    [string]$releaseBinding.boundPackage.contentHash -eq [string]$cacheMetadata.contentHash
)
if (-not $releaseIdentityMatches) { Add-Blocker $blockers 'formal-v4.0.0-release-package-binding-incomplete' }

$buildProvenance = $evidence.buildProvenance
$buildProvenanceMatches = (
    [string]$buildProvenance.status -eq 'verified' -and
    [string]$buildProvenance.sourceCommit -eq $sourceCommit -and
    [string]$buildProvenance.packageSha256 -eq $nupkgSha256 -and
    -not [string]::IsNullOrWhiteSpace([string]$buildProvenance.immutableSource.kind) -and
    -not [string]::IsNullOrWhiteSpace([string]$buildProvenance.immutableSource.sha256)
)
if (-not $buildProvenanceMatches) { Add-Blocker $blockers 'package-build-lock-assets-unavailable' }
$actualBlockers = @($blockers | Sort-Object -Unique)
$expectedBlockers = @($evidence.blockers | ForEach-Object { [string]$_.code } | Sort-Object -Unique)
Assert-ExactSet $expectedBlockers $actualBlockers 'TensorRT retained blocker set'

$verifyOutput = @(& dotnet nuget verify --all $nupkgPath 2>&1)
if ($LASTEXITCODE -ne 0) { throw "TensorRT package signature verification failed: $($verifyOutput -join ' ')" }

$status = if ($actualBlockers.Count -eq 0) { 'admitted' } else { 'blocked' }
if ([string]$evidence.conclusion.status -ne $status) { throw "TensorRT retained conclusion drifted: expected $($evidence.conclusion.status), actual $status." }
$marker = "DEPLOYSHARP_TENSORRT_ADMISSION_$($status.ToUpperInvariant()) package=$packageId version=$packageVersion tfms=$($expectedTfms.Count) managed-dlls=45 native-payload=0 sha512=match repository-commit=$sourceCommit blockers=$($actualBlockers -join ',')"
Write-Output $marker
if ($RequireAdmitted -and $status -ne 'admitted') { throw "TensorRT package admission is blocked: $($actualBlockers -join ',')." }
