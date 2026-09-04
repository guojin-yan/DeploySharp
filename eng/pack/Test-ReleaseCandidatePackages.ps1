[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [string]$ComparisonPackageDirectory,
    [string]$RepositoryRoot,
    [string]$BaselinePath,
    [string]$Configuration = 'Release',
    [switch]$RequireReleaseEligible
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
if ([string]::IsNullOrWhiteSpace($BaselinePath)) { $BaselinePath = Join-Path $PSScriptRoot 'release-candidate-packages.json' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$comparisonRoot = if ([string]::IsNullOrWhiteSpace($ComparisonPackageDirectory)) { $null } else { (Resolve-Path -LiteralPath $ComparisonPackageDirectory).Path }
$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
if ($baseline.schemaVersion -ne '1.0') { throw "Unsupported release package baseline schema: $($baseline.schemaVersion)." }
if ([string]$baseline.packageSigningPolicy -notin @('required', 'optional-alpha-preview-required-ga-commercial')) { throw "Unsupported package signing policy: $($baseline.packageSigningPolicy)." }
$signingRequired = [string]$baseline.packageSigningPolicy -eq 'required'

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata

function Assert-ExactSet {
    param([object[]]$Actual, [object[]]$Expected, [string]$Description)
    $actualValues = @($Actual | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $expectedValues = @($Expected | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    if ($actualValues.Count -ne $expectedValues.Count -or (Compare-Object $expectedValues $actualValues)) {
        throw "$Description mismatch. Expected '$($expectedValues -join ',')'; found '$($actualValues -join ',')'."
    }
}

function Get-SingleNode {
    param([System.Xml.XmlNode[]]$Nodes, [string]$Description)
    $items = @($Nodes)
    if ($items.Count -ne 1) { throw "Expected exactly one $Description; found $($items.Count)." }
    return $items[0]
}

function Get-ChildText {
    param([System.Xml.XmlNode]$Node, [string]$Name)
    $child = $Node.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $child) { return $null }
    return [string]$child.InnerText
}

function Get-EntryText {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)
    $reader = [IO.StreamReader]::new($Entry.Open())
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Get-StreamSha256 {
    param([IO.Stream]$Stream)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-TextSha256 {
    param([string]$Text)
    $stream = [IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes($Text))
    try { return Get-StreamSha256 $stream }
    finally { $stream.Dispose() }
}

function Get-EntrySha256 {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)
    $stream = $Entry.Open()
    try { return Get-StreamSha256 $stream }
    finally { $stream.Dispose() }
}

function Get-AssemblyReferences {
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
            } | Sort-Object -Unique)
        }
        finally { $pe.Dispose() }
    }
    finally {
        $memory.Dispose()
        $entryStream.Dispose()
    }
}

function Get-SemanticEntryMap {
    param([System.IO.Compression.ZipArchive]$Archive)
    $map = @{}
    foreach ($entry in $Archive.Entries) {
        if ($entry.FullName -eq '_rels/.rels' -or
            $entry.FullName -eq '.signature.p7s' -or
            $entry.FullName -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' -or
            $entry.FullName -match '^package/services/digital-signature/') { continue }
        if ($map.ContainsKey($entry.FullName)) { throw "Duplicate semantic NuGet entry: $($entry.FullName)" }
        $map.Add($entry.FullName, (Get-EntrySha256 $entry))
    }
    return $map
}

function Get-DependencyLines {
    param([xml]$Nuspec)
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($group in @($Nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))) {
        $target = [string]$group.targetFramework
        $lines.Add("group|$target")
        foreach ($dependency in @($group.SelectNodes("*[local-name()='dependency']"))) {
            $lines.Add("dependency|$target|$($dependency.id)|$($dependency.version)|$($dependency.exclude)")
        }
    }
    return @($lines | Sort-Object)
}

function Convert-ToLockFramework {
    param([string]$Tfm)
    if ($Tfm -eq 'netstandard2.0') { return '.NETStandard,Version=v2.0' }
    if ($Tfm -eq 'netcoreapp3.1') { return '.NETCoreApp,Version=v3.1' }
    if ($Tfm -eq 'net5.0') { return '.NETCoreApp,Version=v5.0' }
    if ($Tfm -match '^net4(\d)(\d?)$') {
        $minor = $Matches[1]
        $patch = $Matches[2]
        if ($patch) { return ".NETFramework,Version=v4.$minor.$patch" }
        return ".NETFramework,Version=v4.$minor"
    }
    return $Tfm
}

function Convert-ToNuspecFramework {
    param([string]$Tfm)
    if ($Tfm -eq 'netstandard2.0') { return '.NETStandard2.0' }
    if ($Tfm -eq 'netcoreapp3.1') { return '.NETCoreApp3.1' }
    if ($Tfm -match '^net4(\d)(\d?)$') {
        $minor = $Matches[1]
        $patch = $Matches[2]
        if ($patch) { return ".NETFramework4.$minor.$patch" }
        return ".NETFramework4.$minor"
    }
    return $Tfm
}

function Test-NativeRuntimePackage {
    param([string]$Id)
    return $Id -like 'LLamaSharp.Backend.*' -or
        $Id -eq 'Microsoft.ML.OnnxRuntime' -or
        $Id -like 'OpenVINO.runtime*' -or
        $Id -like 'JYPPX.OpenCV.runtime*'
}

[xml]$central = Get-Content -LiteralPath (Join-Path $repository 'Directory.Packages.props') -Raw
$actualCentral = @{}
foreach ($node in @($central.SelectNodes("//*[local-name()='PackageVersion']"))) {
    $id = [string]$node.Include
    if ($actualCentral.ContainsKey($id)) { throw "Duplicate central package version: $id" }
    $actualCentral.Add($id, [string]$node.Version)
}
$expectedCentral = @{}
foreach ($property in $baseline.centralPackages.PSObject.Properties) { $expectedCentral.Add($property.Name, [string]$property.Value) }
Assert-ExactSet @($actualCentral.Keys) @($expectedCentral.Keys) 'Central package IDs'
foreach ($id in $expectedCentral.Keys) {
    if ($actualCentral[$id] -ne $expectedCentral[$id]) { throw "Central package version drift: $id expected=$($expectedCentral[$id]) actual=$($actualCentral[$id])." }
}

$definitions = @($baseline.packages)
$expectedProjects = @($definitions | ForEach-Object { [string]$_.projectPath })
$actualProjects = @(Get-ChildItem -LiteralPath (Join-Path $repository 'src') -Recurse -Filter '*.csproj' | ForEach-Object {
    $_.FullName.Substring($repository.Length + 1).Replace('\', '/')
})
Assert-ExactSet $actualProjects $expectedProjects 'Packable source projects'

$expectedPackageFiles = @($definitions | ForEach-Object { "$($_.packageId).$($baseline.packageVersion).nupkg" })
$actualPackageFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.nupkg' | Select-Object -ExpandProperty Name)
Assert-ExactSet $actualPackageFiles $expectedPackageFiles 'Release package files'
$symbolFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.snupkg')
$expectedSymbolFiles = @($definitions | ForEach-Object { "$($_.packageId).$($baseline.packageVersion).snupkg" })
switch ([string]$baseline.symbolPolicy) {
    'required-snupkg' {
        $actualSymbolFileNames = @($symbolFiles | Select-Object -ExpandProperty Name)
        Assert-ExactSet $actualSymbolFileNames $expectedSymbolFiles 'Release symbol package files'
    }
    'not-produced' {
        if ($symbolFiles.Count -ne 0) { throw 'The baseline forbids symbol packages, but .snupkg files were found.' }
    }
    default { throw "Unsupported symbol policy: $($baseline.symbolPolicy)." }
}

$readmeHash = (Get-FileHash -LiteralPath (Join-Path $repository 'README.md') -Algorithm SHA256).Hash.ToLowerInvariant()
$iconHash = (Get-FileHash -LiteralPath (Join-Path $repository 'nuget\logo.jpg') -Algorithm SHA256).Hash.ToLowerInvariant()
$head = (& git -C $repository rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve repository HEAD.' }
# `applications/` is a separately managed workspace and is intentionally not
# part of the DeploySharp release candidate. Keep it out of the repository
# cleanliness gate without hiding any other tracked or untracked changes.
$dirtyEntries = @(& git -C $repository status --porcelain --untracked-files=all | Where-Object {
    [string]$_ -notmatch '^.{2}\s+applications(?:[\\/]|$)'
})
$dirty = $dirtyEntries.Count -ne 0
$results = @{}
$semanticMatches = 0
$rawMatches = 0
$signedPackages = 0
$totalFrameworks = 0

foreach ($definition in $definitions) {
    $projectPath = Join-Path $repository ([string]$definition.projectPath).Replace('/', '\')
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $projectId = [string](Get-SingleNode @($project.SelectNodes("//*[local-name()='PackageId']")) "$($definition.packageId) PackageId").InnerText
    $assemblyName = [string](Get-SingleNode @($project.SelectNodes("//*[local-name()='AssemblyName']")) "$($definition.packageId) AssemblyName").InnerText
    $isPackable = [string](Get-SingleNode @($project.SelectNodes("//*[local-name()='IsPackable']")) "$($definition.packageId) IsPackable").InnerText
    $license = [string](Get-SingleNode @($project.SelectNodes("//*[local-name()='PackageLicenseExpression']")) "$($definition.packageId) license").InnerText
    if ($projectId -ne $definition.packageId -or $assemblyName -ne $definition.assemblyName -or $isPackable -ne 'true' -or $license -ne $baseline.licenseExpression) {
        throw "Source package metadata drift: $($definition.packageId)."
    }

    $tfmNode = Get-SingleNode @($project.SelectNodes("//*[local-name()='TargetFrameworks']")) "$($definition.packageId) TargetFrameworks"
    $tfmText = [string]$tfmNode.InnerText
    if ($tfmText -eq '$(DeploySharpLibraryTargetFrameworks)') {
        [xml]$build = Get-Content -LiteralPath (Join-Path $repository 'Directory.Build.props') -Raw
        $tfmText = [string](Get-SingleNode @($build.SelectNodes("//*[local-name()='DeploySharpLibraryTargetFrameworks']")) 'central library TFMs').InnerText
    }
    $tfms = @($tfmText.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })
    Assert-ExactSet $tfms @($definition.targetFrameworks) "$($definition.packageId) project TFMs"
    $totalFrameworks += $tfms.Count

    foreach ($reference in @($project.SelectNodes("//*[local-name()='PackageReference']"))) {
        if (Test-NativeRuntimePackage ([string]$reference.Include)) { throw "$($definition.packageId) directly references consumer-owned native package '$($reference.Include)'." }
    }

    $projectDirectory = Split-Path -Parent $projectPath
    $lockPath = Join-Path $projectDirectory 'packages.lock.json'
    $assetsPath = Join-Path $projectDirectory 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) { throw "Lock file missing: $lockPath" }
    if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) { throw "Restore assets missing: $assetsPath" }
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    if ([int]$lock.version -ne 2) { throw "$($definition.packageId) lock version is not 2." }
    Assert-ExactSet @($lock.dependencies.PSObject.Properties.Name) @($tfms | ForEach-Object { Convert-ToLockFramework $_ }) "$($definition.packageId) lock frameworks"
    Assert-ExactSet @($assets.project.frameworks.PSObject.Properties.Name) $tfms "$($definition.packageId) assets project frameworks"
    Assert-ExactSet @($assets.targets.PSObject.Properties.Name) $tfms "$($definition.packageId) assets targets"
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($dependency in $framework.Value.PSObject.Properties) {
            if ($dependency.Value.type -in @('Direct', 'CentralTransitive') -and $actualCentral.ContainsKey($dependency.Name) -and [string]$dependency.Value.resolved -ne $actualCentral[$dependency.Name]) {
                throw "$($definition.packageId) lock/central drift for $($dependency.Name) on $($framework.Name)."
            }
            if (Test-NativeRuntimePackage $dependency.Name) { throw "$($definition.packageId) lock contains consumer-owned native package '$($dependency.Name)'." }
        }
    }
    foreach ($library in @($assets.libraries.PSObject.Properties.Name)) {
        $libraryId = ($library -split '/')[0]
        if (Test-NativeRuntimePackage $libraryId) { throw "$($definition.packageId) assets contain consumer-owned native package '$libraryId'." }
    }

    $packagePath = Join-Path $packageRoot "$($definition.packageId).$($baseline.packageVersion).nupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate NuGet entry in $($definition.packageId): $($entry.FullName)" }
            $entries.Add($entry.FullName, $entry)
        }
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspecEntries.Count -ne 1) { throw "$($definition.packageId) must contain exactly one nuspec." }
        $required = @($nuspecEntries[0].FullName, $baseline.readme, $baseline.icon)
        foreach ($tfm in $tfms) {
            $required += "lib/$tfm/$($definition.assemblyName).dll"
            $required += "lib/$tfm/$($definition.assemblyName).xml"
        }
        foreach ($name in $required) {
            if (-not $entries.ContainsKey($name)) { throw "Required NuGet payload is missing from $($definition.packageId): $name" }
        }
        $libTfms = @($archive.Entries.FullName | Where-Object { $_ -match '^lib/[^/]+/[^/]+\.dll$' } | ForEach-Object { ($_ -split '/')[1] })
        Assert-ExactSet $libTfms $tfms "$($definition.packageId) package TFMs"
        foreach ($name in $entries.Keys) {
            $allowed = $required -contains $name -or
                $name -eq '_rels/.rels' -or
                $name -eq '[Content_Types].xml' -or
                $name -eq '.signature.p7s' -or
                $name -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' -or
                $name -match '^package/services/digital-signature/'
            if (-not $allowed) { throw "Unexpected NuGet payload in $($definition.packageId): $name" }
            if ($name -match '(^|/)(runtimes|native)(/|$)' -or $name -match '(^|/)(llama|ggml[^/]*)\.(dll|so|dylib)$') {
                throw "Native payload leaked into $($definition.packageId): $name"
            }
        }
        if ((Get-EntrySha256 $entries[$baseline.readme]) -ne $readmeHash) { throw "$($definition.packageId) README content drift." }
        if ((Get-EntrySha256 $entries[$baseline.icon]) -ne $iconHash) { throw "$($definition.packageId) icon content drift." }

        [xml]$nuspec = Get-EntryText $nuspecEntries[0]
        $metadata = Get-SingleNode @($nuspec.SelectNodes("/*[local-name()='package']/*[local-name()='metadata']")) "$($definition.packageId) NuGet metadata"
        foreach ($pair in @(
            @('id', [string]$definition.packageId), @('version', [string]$baseline.packageVersion),
            @('authors', [string]$baseline.authors), @('readme', [string]$baseline.readme), @('icon', [string]$baseline.icon))) {
            if ((Get-ChildText $metadata $pair[0]) -ne $pair[1]) { throw "$($definition.packageId) nuspec $($pair[0]) drift." }
        }
        foreach ($requiredText in @('title', 'description', 'tags', 'copyright')) {
            if ([string]::IsNullOrWhiteSpace((Get-ChildText $metadata $requiredText))) { throw "$($definition.packageId) nuspec $requiredText is missing." }
        }
        $licenseNode = Get-SingleNode @($metadata.SelectNodes("*[local-name()='license']")) "$($definition.packageId) license metadata"
        if ([string]$licenseNode.type -ne 'expression' -or $licenseNode.InnerText -ne $baseline.licenseExpression) { throw "$($definition.packageId) license metadata drift." }
        $repositoryNode = Get-SingleNode @($metadata.SelectNodes("*[local-name()='repository']")) "$($definition.packageId) repository metadata"
        if ([string]$repositoryNode.type -ne $baseline.repositoryType -or [string]$repositoryNode.url -ne $baseline.repositoryUrl -or [string]$repositoryNode.commit -ne $head) {
            throw "$($definition.packageId) repository metadata drift."
        }

        $generatedNuspecPath = Join-Path $projectDirectory "obj\$Configuration\$($definition.packageId).$($baseline.packageVersion).nuspec"
        if (-not (Test-Path -LiteralPath $generatedNuspecPath -PathType Leaf)) { throw "Generated nuspec missing: $generatedNuspecPath" }
        [xml]$generatedNuspec = Get-Content -LiteralPath $generatedNuspecPath -Raw
        Assert-ExactSet (Get-DependencyLines $nuspec) (Get-DependencyLines $generatedNuspec) "$($definition.packageId) generated/package dependency graph"
        $dependencyGroups = @{}
        foreach ($group in @($nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))) {
            $dependencyGroups[[string]$group.targetFramework] = @($group.SelectNodes("*[local-name()='dependency']") | ForEach-Object { [string]$_.id })
            foreach ($dependency in $dependencyGroups[[string]$group.targetFramework]) {
                if (Test-NativeRuntimePackage $dependency) { throw "$($definition.packageId) nuspec contains consumer-owned native dependency '$dependency'." }
            }
        }
        Assert-ExactSet @($dependencyGroups.Keys) @($tfms | ForEach-Object { Convert-ToNuspecFramework $_ }) "$($definition.packageId) nuspec dependency groups"

        $referenceLines = [Collections.Generic.List[string]]::new()
        $referenceMap = @{}
        foreach ($tfm in ($tfms | Sort-Object)) {
            $references = @(Get-AssemblyReferences $entries["lib/$tfm/$($definition.assemblyName).dll"])
            if (@($references | Where-Object { $_ -like 'LLamaSharp.Backend.*' }).Count -ne 0) { throw "$($definition.packageId) assembly references a native LLamaSharp backend." }
            $referenceMap[$tfm] = $references
            $referenceLines.Add("$tfm|$($references -join ',')")
        }
        $referenceDigest = Get-TextSha256 (($referenceLines -join "`n") + "`n")
        if ($referenceDigest -ne $definition.assemblyReferenceSha256) { throw "$($definition.packageId) assembly-reference baseline drift: $referenceDigest." }

        $signed = $entries.ContainsKey('.signature.p7s')
        if ($signed) { $signedPackages++ }
        $semantic = 'not-requested'
        $rawIdentical = 'not-requested'
        if ($null -ne $comparisonRoot) {
            $comparisonPath = Join-Path $comparisonRoot (Split-Path -Leaf $packagePath)
            $comparisonArchive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $comparisonPath).Path)
            try {
                $primaryMap = Get-SemanticEntryMap $archive
                $comparisonMap = Get-SemanticEntryMap $comparisonArchive
                Assert-ExactSet @($primaryMap.Keys) @($comparisonMap.Keys) "$($definition.packageId) semantic entry set"
                $differences = @($primaryMap.Keys | Where-Object { $primaryMap[$_] -ne $comparisonMap[$_] })
                if ($differences.Count -ne 0) { throw "$($definition.packageId) semantic payload drift: $($differences -join ',')." }
                $semantic = 'match'
                $semanticMatches++
                $rawIdentical = [string]::Equals((Get-FileHash $packagePath -Algorithm SHA256).Hash, (Get-FileHash $comparisonPath -Algorithm SHA256).Hash, [StringComparison]::OrdinalIgnoreCase)
                if ($rawIdentical) { $rawMatches++ }
            }
            finally { $comparisonArchive.Dispose() }
        }
        $results[$definition.packageId] = [pscustomobject]@{ Definition = $definition; Dependencies = $dependencyGroups; References = $referenceMap }
        Write-Output "DEPLOYSHARP_RELEASE_PACKAGE_OK id=$($definition.packageId) tfms=$($tfms.Count) entries=$($entries.Count) refsha256=$referenceDigest signed=$($signed.ToString().ToLowerInvariant()) semantic=$semantic raw-identical=$($rawIdentical.ToString().ToLowerInvariant())"
    }
    finally { $archive.Dispose() }
}

foreach ($packageId in $results.Keys) {
    $result = $results[$packageId]
    foreach ($tfm in @($result.Definition.targetFrameworks)) {
        $group = Convert-ToNuspecFramework $tfm
        $reachable = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $queue = [Collections.Generic.Queue[string]]::new()
        $directDependencies = if ($result.Dependencies.ContainsKey($group)) { @($result.Dependencies[$group]) } else { @() }
        foreach ($dependency in @($directDependencies | Where-Object { $null -ne $_ -and $results.ContainsKey($_) })) { $queue.Enqueue($dependency) }
        while ($queue.Count -gt 0) {
            $dependency = $queue.Dequeue()
            if (-not $reachable.Add($dependency)) { continue }
            $dependencyResult = $results[$dependency]
            $childDependencies = if ($dependencyResult.Dependencies.ContainsKey($group)) { @($dependencyResult.Dependencies[$group]) } else { @() }
            foreach ($child in @($childDependencies | Where-Object { $null -ne $_ -and $results.ContainsKey($_) })) { $queue.Enqueue($child) }
        }
        foreach ($reference in @($result.References[$tfm] | Where-Object { $_ -like 'JYPPX.DeploySharp.*' -and $_ -ne $result.Definition.assemblyName })) {
            if (-not $reachable.Contains($reference)) { throw "$packageId/$tfm references internal assembly '$reference' outside its nuspec dependency closure." }
        }
    }
}

$blockers = [Collections.Generic.List[string]]::new()
if ($dirty) { $blockers.Add('dirty-worktree') }
if ($signingRequired -and $signedPackages -ne $definitions.Count) { $blockers.Add('unsigned-packages') }
if ($null -eq $comparisonRoot) {
    $blockers.Add('raw-nupkg-container-bit-reproducibility-not-verified')
}
elseif ($rawMatches -ne $definitions.Count) {
    $blockers.Add('raw-nupkg-container-bit-reproducibility')
}
$releaseEligible = $blockers.Count -eq 0
$comparisonStatus = if ($null -eq $comparisonRoot) { 'not-requested' } else { "$semanticMatches/$($definitions.Count)" }
$rawStatus = if ($null -eq $comparisonRoot) { 'not-requested' } else { "$rawMatches/$($definitions.Count)" }
$blockerText = if ($blockers.Count -eq 0) { 'none' } else { $blockers -join ',' }
Write-Output "DEPLOYSHARP_RELEASE_CANDIDATE_GATE_OK packages=$($definitions.Count) tfms=$totalFrameworks locks=$($definitions.Count) assets=$($definitions.Count) semantic=$comparisonStatus raw-identical=$rawStatus signed=$signedPackages signing-policy=$($baseline.packageSigningPolicy) symbols=$($symbolFiles.Count) symbol-policy=$($baseline.symbolPolicy) native-owner=$($baseline.nativeRuntimePolicy) repository-clean=$((-not $dirty).ToString().ToLowerInvariant()) release-eligible=$($releaseEligible.ToString().ToLowerInvariant()) blockers=$blockerText"
if ($RequireReleaseEligible -and -not $releaseEligible) { throw "Release candidate is blocked: $blockerText." }
