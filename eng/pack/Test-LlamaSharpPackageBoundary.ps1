[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$RepositoryRoot,
    [string]$AssetsPath,
    [string]$ComparisonPackagePath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..\..'
}

$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$centralPath = Join-Path $repository 'Directory.Packages.props'
$projectPath = Join-Path $repository 'src\DeploySharp.Backend.LlamaSharp\DeploySharp.Backend.LlamaSharp.csproj'
$lockPath = Join-Path $repository 'src\DeploySharp.Backend.LlamaSharp\packages.lock.json'
$testProjectPath = Join-Path $repository 'tests\DeploySharp.Backend.LlamaSharp.Tests\DeploySharp.Backend.LlamaSharp.Tests.csproj'
$consumerProjectPath = Join-Path $repository 'tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj'
if ([string]::IsNullOrWhiteSpace($AssetsPath)) {
    $AssetsPath = Join-Path $repository 'src\DeploySharp.Backend.LlamaSharp\obj\project.assets.json'
}
$assets = (Resolve-Path -LiteralPath $AssetsPath).Path

$expectedFrameworks = @('netstandard2.0', 'net8.0')
$expectedNuspecFrameworks = @('.NETStandard2.0', 'net8.0')
$backendAssemblyName = 'JYPPX.DeploySharp.Backend.LlamaSharp'

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata

function Get-SingleXmlNode {
    param(
        [System.Xml.XmlNode[]]$Nodes,
        [string]$Description
    )

    $items = @($Nodes)
    if ($items.Count -ne 1) { throw "Expected exactly one $Description; found $($items.Count)." }
    return $items[0]
}

function Get-EntryText {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)

    $stream = $Entry.Open()
    try {
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-EntrySha256 {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)

    $stream = $Entry.Open()
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Get-SemanticEntryMap {
    param([System.IO.Compression.ZipArchive]$Archive)

    $map = @{}
    foreach ($entry in $Archive.Entries) {
        if ($entry.FullName -eq '_rels/.rels' -or
            $entry.FullName -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' -or
            $entry.FullName -match '^package/services/digital-signature/[^/]+\.p7s$') {
            continue
        }
        if ($map.ContainsKey($entry.FullName)) { throw "Duplicate semantic NuGet entry: $($entry.FullName)" }
        $map.Add($entry.FullName, (Get-EntrySha256 $entry))
    }
    return $map
}

function Get-AssemblyReferenceNames {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)

    $entryStream = $Entry.Open()
    $memory = [IO.MemoryStream]::new()
    try {
        $entryStream.CopyTo($memory)
        $memory.Position = 0
        $pe = [Reflection.PortableExecutable.PEReader]::new($memory)
        try {
            $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            return @($reader.AssemblyReferences | ForEach-Object {
                $reference = $reader.GetAssemblyReference($_)
                $reader.GetString($reference.Name)
            })
        }
        finally {
            $pe.Dispose()
        }
    }
    finally {
        $memory.Dispose()
        $entryStream.Dispose()
    }
}

function Assert-ExactSet {
    param(
        [string[]]$Actual,
        [string[]]$Expected,
        [string]$Description
    )

    $actualValues = @($Actual | Sort-Object -Unique)
    $expectedValues = @($Expected | Sort-Object -Unique)
    if ($actualValues.Count -ne $expectedValues.Count -or (Compare-Object $expectedValues $actualValues)) {
        throw "$Description mismatch. Expected '$($expectedValues -join ',')'; found '$($actualValues -join ',')'."
    }
}

[xml]$central = Get-Content -LiteralPath $centralPath -Raw
$managedVersionNode = Get-SingleXmlNode @($central.SelectNodes("//*[local-name()='PackageVersion' and @Include='LLamaSharp']")) 'central LLamaSharp version'
$cpuVersionNode = Get-SingleXmlNode @($central.SelectNodes("//*[local-name()='PackageVersion' and @Include='LLamaSharp.Backend.Cpu']")) 'central LLamaSharp.Backend.Cpu version'
$managedVersion = [string]$managedVersionNode.Version
$cpuVersion = [string]$cpuVersionNode.Version
if ([string]::IsNullOrWhiteSpace($managedVersion) -or $managedVersion -ne $cpuVersion) {
    throw "Managed/native central versions differ: LLamaSharp=$managedVersion LLamaSharp.Backend.Cpu=$cpuVersion."
}

[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$targetFrameworksNode = Get-SingleXmlNode @($project.SelectNodes("//*[local-name()='TargetFrameworks']")) 'Backend.LlamaSharp TargetFrameworks'
$projectFrameworks = @($targetFrameworksNode.InnerText.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })
Assert-ExactSet $projectFrameworks $expectedFrameworks 'Backend.LlamaSharp target frameworks'
$managedReferences = @($project.SelectNodes("//*[local-name()='PackageReference' and @Include='LLamaSharp']"))
if ($managedReferences.Count -ne 1) { throw "Backend.LlamaSharp must reference managed LLamaSharp exactly once; found $($managedReferences.Count)." }
$nativeReferences = @($project.SelectNodes("//*[local-name()='PackageReference' and starts-with(@Include,'LLamaSharp.Backend.')]"))
if ($nativeReferences.Count -ne 0) { throw 'Backend.LlamaSharp must not reference a native LLamaSharp backend package.' }

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ([int]$lock.version -ne 2) { throw "Unsupported Backend.LlamaSharp lock-file version: $($lock.version)." }
$lockFrameworks = [System.Collections.Generic.List[string]]::new()
foreach ($framework in $lock.dependencies.PSObject.Properties) {
    $lockFrameworks.Add($framework.Name)
    $managed = $framework.Value.PSObject.Properties['LLamaSharp']
    if ($null -eq $managed -or [string]$managed.Value.type -ne 'Direct' -or [string]$managed.Value.resolved -ne $managedVersion -or -not ([string]$managed.Value.contentHash)) {
        throw "Backend.LlamaSharp lock entry is invalid for $($framework.Name)."
    }
    $native = @($framework.Value.PSObject.Properties | Where-Object { $_.Name -like 'LLamaSharp.Backend.*' })
    if ($native.Count -ne 0) { throw "Backend.LlamaSharp lock contains a native backend for $($framework.Name)." }
}
Assert-ExactSet $lockFrameworks.ToArray() @('.NETStandard,Version=v2.0', 'net8.0') 'Backend.LlamaSharp lock frameworks'

$assetDocument = Get-Content -LiteralPath $assets -Raw | ConvertFrom-Json
$assetFrameworks = @($assetDocument.targets.PSObject.Properties.Name)
Assert-ExactSet $assetFrameworks $expectedFrameworks 'Backend.LlamaSharp assets targets'
$assetLibraries = @($assetDocument.libraries.PSObject.Properties.Name)
if ($assetLibraries -notcontains "LLamaSharp/$managedVersion") { throw "Managed LLamaSharp/$managedVersion is missing from Backend.LlamaSharp assets." }
if (@($assetLibraries | Where-Object { $_ -like 'LLamaSharp.Backend.*/*' }).Count -ne 0) { throw 'Backend.LlamaSharp assets contain a native backend package.' }
foreach ($target in $assetDocument.targets.PSObject.Properties) {
    $managedTarget = $target.Value.PSObject.Properties["LLamaSharp/$managedVersion"]
    if ($null -eq $managedTarget) { throw "Managed LLamaSharp/$managedVersion is missing from assets target $($target.Name)." }
    if ($null -ne $managedTarget.Value.native -or $null -ne $managedTarget.Value.runtimeTargets) {
        throw "Managed LLamaSharp assets unexpectedly contain native/runtimeTargets for $($target.Name)."
    }
}

[xml]$testProject = Get-Content -LiteralPath $testProjectPath -Raw
$testCpuReference = Get-SingleXmlNode @($testProject.SelectNodes("//*[local-name()='PackageReference' and @Include='LLamaSharp.Backend.Cpu']")) 'test-owned CPU backend reference'
if (-not [string]::IsNullOrWhiteSpace([string]$testCpuReference.Version)) { throw 'The test CPU backend must use the central package version.' }

[xml]$consumerProject = Get-Content -LiteralPath $consumerProjectPath -Raw
$consumerCpuReference = Get-SingleXmlNode @($consumerProject.SelectNodes("//*[local-name()='PackageReference' and @Include='LLamaSharp.Backend.Cpu']")) 'consumer-owned CPU backend reference'
if ([string]$consumerCpuReference.Version -ne $cpuVersion) { throw "The clean consumer CPU backend version is '$($consumerCpuReference.Version)', expected '$cpuVersion'." }
if ([string]$consumerCpuReference.Condition -ne "'`$(IncludeLlamaNativeBackend)' == 'true'") { throw 'The clean consumer CPU backend reference is not controlled by IncludeLlamaNativeBackend.' }

$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) {
        if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate NuGet entry: $($entry.FullName)" }
        $entries.Add($entry.FullName, $entry)
    }

    $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($nuspecEntries.Count -ne 1) { throw "Expected one nuspec; found $($nuspecEntries.Count)." }
    $nuspecName = $nuspecEntries[0].FullName
    $required = @($nuspecName, 'README.md', 'logo.jpg')
    foreach ($framework in $expectedFrameworks) {
        $required += "lib/$framework/$backendAssemblyName.dll"
        $required += "lib/$framework/$backendAssemblyName.xml"
    }
    foreach ($name in $required) {
        if (-not $entries.ContainsKey($name)) { throw "Required NuGet payload is missing: $name" }
    }

    foreach ($name in $entries.Keys) {
        $allowed = (
            ($required -contains $name) -or
            ($name -eq '_rels/.rels') -or
            ($name -eq '[Content_Types].xml') -or
            ($name -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$') -or
            ($name -match '^package/services/digital-signature/[^/]+\.p7s$')
        )
        if (-not $allowed) { throw "Unexpected NuGet payload: $name" }
        if ($name -match '(^|/)(runtimes|native)(/|$)' -or $name -match '(^|/)(llama|ggml[^/]*)\.(dll|so|dylib)$' -or $name -match '(^|/)LLamaSharp\.Backend\.') {
            throw "Native LLamaSharp payload leaked into Backend.LlamaSharp: $name"
        }
    }

    [xml]$nuspec = Get-EntryText $nuspecEntries[0]
    $metadata = Get-SingleXmlNode @($nuspec.SelectNodes("/*[local-name()='package']/*[local-name()='metadata']")) 'NuGet metadata'
    if ($metadata.SelectSingleNode("*[local-name()='id']").InnerText -ne 'JYPPX.DeploySharp.Backend.LlamaSharp') { throw 'Unexpected Backend.LlamaSharp package ID.' }
    $groups = @($nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))
    Assert-ExactSet @($groups | ForEach-Object { [string]$_.targetFramework }) $expectedNuspecFrameworks 'Backend.LlamaSharp nuspec dependency groups'
    foreach ($group in $groups) {
        $dependencies = @($group.SelectNodes("*[local-name()='dependency']"))
        $managedDependency = @($dependencies | Where-Object { [string]$_.id -eq 'LLamaSharp' })
        if ($managedDependency.Count -ne 1 -or [string]$managedDependency[0].version -ne $managedVersion) {
            throw "Nuspec group '$($group.targetFramework)' must contain LLamaSharp $managedVersion exactly once."
        }
        if (@($dependencies | Where-Object { [string]$_.id -like 'LLamaSharp.Backend.*' }).Count -ne 0) {
            throw "Nuspec group '$($group.targetFramework)' contains a native LLamaSharp backend."
        }
    }

    foreach ($framework in $expectedFrameworks) {
        $references = @(Get-AssemblyReferenceNames $entries["lib/$framework/$backendAssemblyName.dll"])
        foreach ($requiredReference in @('JYPPX.DeploySharp.Core', 'JYPPX.DeploySharp.LLM', 'LLamaSharp')) {
            if ($references -notcontains $requiredReference) { throw "$framework assembly is missing reference '$requiredReference'." }
        }
        if (@($references | Where-Object { $_ -like 'LLamaSharp.Backend.*' }).Count -ne 0) {
            throw "$framework assembly references a native LLamaSharp backend."
        }
    }

    $semanticComparison = 'not-requested'
    $rawIdentical = 'not-requested'
    if (-not [string]::IsNullOrWhiteSpace($ComparisonPackagePath)) {
        $comparisonPackage = (Resolve-Path -LiteralPath $ComparisonPackagePath).Path
        $comparisonArchive = [IO.Compression.ZipFile]::OpenRead($comparisonPackage)
        try {
            $primaryMap = Get-SemanticEntryMap $archive
            $comparisonMap = Get-SemanticEntryMap $comparisonArchive
            Assert-ExactSet @($primaryMap.Keys) @($comparisonMap.Keys) 'Semantic NuGet entry set'
            $contentDifferences = @($primaryMap.Keys | Where-Object { $primaryMap[$_] -ne $comparisonMap[$_] })
            if ($contentDifferences.Count -ne 0) { throw "Semantic NuGet payload differs: $($contentDifferences -join ',')." }
            $semanticComparison = 'match'
            $rawIdentical = [string]::Equals(
                (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash,
                (Get-FileHash -LiteralPath $comparisonPackage -Algorithm SHA256).Hash,
                [StringComparison]::OrdinalIgnoreCase).ToString().ToLowerInvariant()
        }
        finally {
            $comparisonArchive.Dispose()
        }
    }

    Write-Output "DEPLOYSHARP_LLAMASHARP_PACKAGE_BOUNDARY_OK managed=$managedVersion native=$cpuVersion tfms=$($expectedFrameworks -join ',') entries=$($entries.Count) native-owner=consumer semantic-comparison=$semanticComparison raw-identical=$rawIdentical"
}
finally {
    $archive.Dispose()
}
