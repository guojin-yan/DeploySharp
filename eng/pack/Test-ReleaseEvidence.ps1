[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [string]$RepositoryRoot,
    [string]$EvidenceDirectory,
    [string]$PackageCacheDirectory,
    [string]$Configuration = 'Release',
    [string]$ReleasePolicyPath = (Join-Path $PSScriptRoot 'release-evidence-policy.json'),
    [string]$ReleaseAuthorizationPath = (Join-Path $PSScriptRoot 'release-authorization.json'),
    [switch]$WriteBaseline,
    [switch]$RequireReleaseEligible
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = Join-Path $PSScriptRoot 'release-evidence' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\', '/')
$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packageCacheInput = if (-not [string]::IsNullOrWhiteSpace($PackageCacheDirectory)) {
    $PackageCacheDirectory
} elseif (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES
} else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.nuget\packages'
}
$packageCacheRoot = [IO.Path]::GetFullPath($packageCacheInput)
if (-not (Test-Path -LiteralPath $packageCacheRoot -PathType Container)) { throw "NuGet package cache is missing: $packageCacheRoot" }
$baselinePath = Join-Path $PSScriptRoot 'release-candidate-packages.json'
$releaseBaseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
if ([string]$releaseBaseline.packageSigningPolicy -notin @('required', 'optional-alpha-preview-required-ga-commercial')) { throw "Unsupported package signing policy: $($releaseBaseline.packageSigningPolicy)." }
$resolvedAuthorizationPath = (Resolve-Path -LiteralPath $ReleaseAuthorizationPath).Path
$releaseAuthorization = Get-Content -LiteralPath $resolvedAuthorizationPath -Raw | ConvertFrom-Json
if ($releaseAuthorization.schemaVersion -ne '1.0' -or $releaseAuthorization.packageVersion -ne $releaseBaseline.packageVersion) {
    throw 'Release authorization schema or package version is invalid.'
}
$head = (& git -C $repository rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-f]{40}$') { throw 'Unable to resolve the repository HEAD.' }
$globalJson = Get-Content -LiteralPath (Join-Path $repository 'global.json') -Raw | ConvertFrom-Json
$actualSdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($actualSdkVersion)) { throw 'Unable to resolve the .NET SDK identity.' }
$msbuildVersion = @(& dotnet msbuild -version -nologo | Where-Object { $_ -match '^\d+\.' } | Select-Object -Last 1)
if ($LASTEXITCODE -ne 0 -or $msbuildVersion.Count -ne 1) { throw 'Unable to resolve the MSBuild identity.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata

$provenanceFileName = 'package-provenance-sbom.json'
$symbolsFileName = 'release-symbols.json'
$apiFileName = 'public-api.json'
$sourceLinkKind = [Guid]'cc110556-a091-4d38-9fec-25ab9a351a6a'
$compilationOptionsKind = [Guid]'b5feec05-8cd0-4a83-96da-466284bb4bd8'
$embeddedSourceKind = [Guid]'0e8a571b-6926-466e-b4ad-8ab04611f5fe'

function Get-Sha256Bytes {
    param([byte[]]$Bytes)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([Convert]::ToHexString($algorithm.ComputeHash($Bytes))).ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-Sha256Text {
    param([string]$Text)
    return Get-Sha256Bytes ([Text.Encoding]::UTF8.GetBytes($Text))
}

function Get-FileSha256 {
    param([string]$Path)
    return ([string](Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash).ToLowerInvariant()
}

function Get-StreamSha256 {
    param([IO.Stream]$Stream)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([Convert]::ToHexString($algorithm.ComputeHash($Stream))).ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-EntryBytes {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $source = $Entry.Open()
    $memory = [IO.MemoryStream]::new()
    try {
        $source.CopyTo($memory)
        return $memory.ToArray()
    }
    finally {
        $memory.Dispose()
        $source.Dispose()
    }
}

function Get-EntryText {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    return [Text.Encoding]::UTF8.GetString((Get-EntryBytes $Entry)).TrimStart([char]0xfeff)
}

function Get-EntrySha256 {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $stream = $Entry.Open()
    try { return Get-StreamSha256 $stream }
    finally { $stream.Dispose() }
}

function Get-BlobHex {
    param([Reflection.Metadata.MetadataReader]$Reader, [Reflection.Metadata.BlobHandle]$Handle)
    if ($Handle.IsNil) { return '' }
    [byte[]]$bytes = @($Reader.GetBlobBytes($Handle))
    return ([Convert]::ToHexString($bytes)).ToLowerInvariant()
}

function Get-CanonicalValue {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string] -or $Value -is [ValueType]) { return $Value }
    if ($Value -is [Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in @($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object)) {
            $ordered[$key] = Get-CanonicalValue $Value[$key]
        }
        return $ordered
    }
    if ($Value -is [Collections.IEnumerable]) {
        return @($Value | ForEach-Object { Get-CanonicalValue $_ })
    }
    $properties = @($Value.PSObject.Properties | Where-Object { $_.MemberType -in @('NoteProperty', 'Property') } | Sort-Object Name)
    $object = [ordered]@{}
    foreach ($property in $properties) { $object[$property.Name] = Get-CanonicalValue $property.Value }
    return $object
}

function Get-CanonicalJson {
    param([object]$Value)
    return (Get-CanonicalValue $Value) | ConvertTo-Json -Depth 100 -Compress
}

function Convert-ToNuspecFramework {
    param([string]$Tfm)
    if ($Tfm -eq 'netstandard2.0') { return '.NETStandard2.0' }
    if ($Tfm -eq 'netcoreapp3.1') { return '.NETCoreApp3.1' }
    if ($Tfm -match '^net4(\d)(\d?)$') {
        if ($Matches[2]) { return ".NETFramework4.$($Matches[1]).$($Matches[2])" }
        return ".NETFramework4.$($Matches[1])"
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

function Get-MetadataChildText {
    param([Xml.XmlNode]$Metadata, [string]$Name)
    $node = $Metadata.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return $null }
    return [string]$node.InnerText
}

function Get-PackageCacheRecord {
    param(
        [string]$Id,
        [string]$Version,
        [string]$ContentHash,
        [string[]]$UsedBy,
        [string]$Ownership
    )

    $packageDirectory = Join-Path $packageCacheRoot (Join-Path $Id.ToLowerInvariant() $Version.ToLowerInvariant())
    if (-not (Test-Path -LiteralPath $packageDirectory -PathType Container)) { throw "Restored package cache is missing: $Id/$Version." }
    $nuspecFile = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nuspec')
    $nupkgFile = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nupkg')
    $sha512File = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nupkg.sha512')
    $metadataFile = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '.nupkg.metadata')
    if ($nuspecFile.Count -ne 1 -or $nupkgFile.Count -ne 1 -or $sha512File.Count -ne 1 -or $metadataFile.Count -ne 1) { throw "Incomplete restored package metadata for $Id/$Version." }

    $cacheContentHash = (Get-Content -LiteralPath $sha512File[0].FullName -Raw).Trim()
    $actualNupkgSha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($nupkgFile[0].FullName)))
    if ($actualNupkgSha512 -ne $cacheContentHash) { throw "Cached nupkg SHA512 drift for $Id/$Version." }
    $metadata = Get-Content -LiteralPath $metadataFile[0].FullName -Raw | ConvertFrom-Json
    if ([int]$metadata.version -ne 2 -or [string]::IsNullOrWhiteSpace([string]$metadata.contentHash)) { throw "Invalid NuGet package metadata for $Id/$Version." }
    if (-not [string]::IsNullOrWhiteSpace($ContentHash) -and [string]$metadata.contentHash -ne $ContentHash) {
        throw "NuGet package cache content hash does not match lock/assets for $Id/$Version."
    }

    [xml]$nuspec = Get-Content -LiteralPath $nuspecFile[0].FullName -Raw
    $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($null -eq $metadata) { throw "NuGet metadata is missing for $Id/$Version." }
    if ((Get-MetadataChildText $metadata 'id') -ine $Id -or (Get-MetadataChildText $metadata 'version') -ne $Version) {
        throw "NuGet cache identity drift for $Id/$Version."
    }

    $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
    $licenseUrl = Get-MetadataChildText $metadata 'licenseUrl'
    $licenseType = if ($null -eq $licenseNode) { 'missing' } else { [string]$licenseNode.type }
    $licenseValue = if ($null -eq $licenseNode) { $null } else { [string]$licenseNode.InnerText }
    $licenseStatus = 'blocker-missing-license'
    $licenseSource = 'missing'
    $licenseFileSha256 = $null
    $licenseFileBytes = 0
    $spdxExpression = $null
    $manualReview = $true

    if ($licenseType -eq 'expression') {
        $licenseSource = 'nuspec-license-expression'
        if ($licenseValue -in @('Apache-2.0', 'BSD-3-Clause', 'MIT')) {
            $spdxExpression = $licenseValue
            $licenseStatus = 'verified-spdx-expression'
            $manualReview = $false
        }
        else {
            $licenseStatus = 'blocker-non-spdx-or-unapproved-expression'
        }
    }
    elseif ($licenseType -eq 'file' -and -not [string]::IsNullOrWhiteSpace($licenseValue)) {
        $licenseSource = 'nuspec-license-file'
        $archive = [IO.Compression.ZipFile]::OpenRead($nupkgFile[0].FullName)
        try {
            $licenseEntry = @($archive.Entries | Where-Object { $_.FullName -ieq $licenseValue })
            if ($licenseEntry.Count -ne 1) { throw "Declared license file '$licenseValue' is missing from $Id/$Version." }
            $licenseFileSha256 = Get-EntrySha256 $licenseEntry[0]
            $licenseFileBytes = $licenseEntry[0].Length
            $licenseStatus = 'blocker-license-file-manual-review'
        }
        finally { $archive.Dispose() }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($licenseUrl)) {
        $licenseSource = 'nuspec-license-url'
        $licenseStatus = 'blocker-license-url-manual-review'
    }

    $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
    $repositoryEvidence = [ordered]@{
        type = if ($null -eq $repositoryNode) { $null } else { [string]$repositoryNode.type }
        url = if ($null -eq $repositoryNode) { $null } else { [string]$repositoryNode.url }
        commit = if ($null -eq $repositoryNode) { $null } else { [string]$repositoryNode.commit }
        projectUrl = Get-MetadataChildText $metadata 'projectUrl'
        status = if ($null -eq $repositoryNode -or [string]::IsNullOrWhiteSpace([string]$repositoryNode.url) -or [string]::IsNullOrWhiteSpace([string]$repositoryNode.commit)) { 'incomplete' } else { 'complete' }
    }

    return [ordered]@{
        id = $Id
        version = $Version
        ownership = $Ownership
        resolvedContentHash = if ([string]::IsNullOrWhiteSpace($ContentHash)) { $null } else { $ContentHash }
        cachedNupkgSha512 = $cacheContentHash
        nupkgSha256 = (Get-FileHash -LiteralPath $nupkgFile[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        cachePath = "$($Id.ToLowerInvariant())/$($Version.ToLowerInvariant())"
        usedBy = @($UsedBy | Sort-Object -Unique)
        license = [ordered]@{
            metadataType = $licenseType
            metadataValue = $licenseValue
            spdxExpression = $spdxExpression
            licenseUrl = $licenseUrl
            source = $licenseSource
            fileSha256 = $licenseFileSha256
            fileBytes = $licenseFileBytes
            status = $licenseStatus
            manualReview = $manualReview
        }
        repository = $repositoryEvidence
    }
}

function Get-EntityTypeName {
    param([Reflection.Metadata.MetadataReader]$Reader, [Reflection.Metadata.EntityHandle]$Handle)
    if ($Handle.IsNil) { return $null }
    if ($Handle.Kind -eq [Reflection.Metadata.HandleKind]::TypeDefinition) {
        $definition = $Reader.GetTypeDefinition([Reflection.Metadata.TypeDefinitionHandle]$Handle)
        $name = $Reader.GetString($definition.Name)
        $declaring = $definition.GetDeclaringType()
        if (-not $declaring.IsNil) { return "$(Get-EntityTypeName $Reader $declaring)+$name" }
        $namespace = $Reader.GetString($definition.Namespace)
        if ([string]::IsNullOrWhiteSpace($namespace)) { return $name }
        return "$namespace.$name"
    }
    if ($Handle.Kind -eq [Reflection.Metadata.HandleKind]::TypeReference) {
        $reference = $Reader.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$Handle)
        $name = $Reader.GetString($reference.Name)
        if ($reference.ResolutionScope.Kind -eq [Reflection.Metadata.HandleKind]::TypeReference) { return "$(Get-EntityTypeName $Reader $reference.ResolutionScope)+$name" }
        $namespace = $Reader.GetString($reference.Namespace)
        if ([string]::IsNullOrWhiteSpace($namespace)) { return $name }
        return "$namespace.$name"
    }
    if ($Handle.Kind -eq [Reflection.Metadata.HandleKind]::TypeSpecification) {
        $specification = $Reader.GetTypeSpecification([Reflection.Metadata.TypeSpecificationHandle]$Handle)
        return "typespec:$(Get-BlobHex $Reader $specification.Signature)"
    }
    return "handle:$($Handle.Kind):$($Handle.GetHashCode().ToString('x8'))"
}

function Get-CustomAttributeTypeName {
    param([Reflection.Metadata.MetadataReader]$Reader, [Reflection.Metadata.EntityHandle]$Constructor)
    if ($Constructor.Kind -eq [Reflection.Metadata.HandleKind]::MemberReference) {
        $member = $Reader.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$Constructor)
        return Get-EntityTypeName $Reader $member.Parent
    }
    if ($Constructor.Kind -eq [Reflection.Metadata.HandleKind]::MethodDefinition) {
        $method = $Reader.GetMethodDefinition([Reflection.Metadata.MethodDefinitionHandle]$Constructor)
        return Get-EntityTypeName $Reader $method.GetDeclaringType()
    }
    return "attribute-constructor:$($Constructor.Kind)"
}

function Add-CustomAttributeEvidence {
    param(
        [Reflection.Metadata.MetadataReader]$Reader,
        [object]$Definition,
        [string]$Owner,
        [Collections.Generic.List[string]]$Attributes,
        [Collections.Generic.List[string]]$NullableAttributes
    )
    foreach ($handle in $Definition.GetCustomAttributes()) {
        $attribute = $Reader.GetCustomAttribute($handle)
        $typeName = Get-CustomAttributeTypeName $Reader $attribute.Constructor
        $line = "$Owner|$typeName|$(Get-BlobHex $Reader $attribute.Value)"
        $Attributes.Add($line)
        if ($typeName -like 'System.Runtime.CompilerServices.Nullable*' -or $typeName -eq 'System.Runtime.CompilerServices.TupleElementNamesAttribute') {
            $NullableAttributes.Add($line)
        }
    }
}

function Test-VisibleType {
    param([Reflection.Metadata.MetadataReader]$Reader, [Reflection.Metadata.TypeDefinitionHandle]$Handle, [hashtable]$Cache)
    $token = $Handle.GetHashCode()
    if ($Cache.ContainsKey($token)) { return [bool]$Cache[$token] }
    $definition = $Reader.GetTypeDefinition($Handle)
    $visibility = $definition.Attributes -band [Reflection.TypeAttributes]::VisibilityMask
    $declaring = $definition.GetDeclaringType()
    if ($declaring.IsNil) {
        $visible = $visibility -eq [Reflection.TypeAttributes]::Public
    }
    else {
        $visible = $visibility -in @([Reflection.TypeAttributes]::NestedPublic, [Reflection.TypeAttributes]::NestedFamily, [Reflection.TypeAttributes]::NestedFamORAssem) -and (Test-VisibleType $Reader $declaring $Cache)
    }
    $Cache[$token] = $visible
    return $visible
}

function Test-VisibleMethodAttributes {
    param([Reflection.MethodAttributes]$Attributes)
    $access = $Attributes -band [Reflection.MethodAttributes]::MemberAccessMask
    return $access -in @([Reflection.MethodAttributes]::Public, [Reflection.MethodAttributes]::Family, [Reflection.MethodAttributes]::FamORAssem)
}

function Test-VisibleFieldAttributes {
    param([Reflection.FieldAttributes]$Attributes)
    $access = $Attributes -band [Reflection.FieldAttributes]::FieldAccessMask
    return $access -in @([Reflection.FieldAttributes]::Public, [Reflection.FieldAttributes]::Family, [Reflection.FieldAttributes]::FamORAssem)
}

function Add-GenericParameterEvidence {
    param(
        [Reflection.Metadata.MetadataReader]$Reader,
        [object]$Definition,
        [string]$Owner,
        [Collections.Generic.List[string]]$GenericLines,
        [Collections.Generic.List[string]]$Attributes,
        [Collections.Generic.List[string]]$NullableAttributes
    )
    foreach ($handle in $Definition.GetGenericParameters()) {
        $parameter = $Reader.GetGenericParameter($handle)
        $name = $Reader.GetString($parameter.Name)
        $constraints = @($parameter.GetConstraints() | ForEach-Object {
            $constraint = $Reader.GetGenericParameterConstraint($_)
            Get-EntityTypeName $Reader $constraint.Type
        } | Sort-Object)
        $genericOwner = "$Owner|generic:$($parameter.Index):$name"
        $GenericLines.Add("$genericOwner|flags=$([int]$parameter.Attributes)|constraints=$($constraints -join ',')")
        Add-CustomAttributeEvidence $Reader $parameter $genericOwner $Attributes $NullableAttributes
    }
}

function Get-PublicMetadataEvidence {
    param([byte[]]$AssemblyBytes)
    $stream = [IO.MemoryStream]::new($AssemblyBytes, $false)
    $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $surface = [Collections.Generic.List[string]]::new()
        $attributes = [Collections.Generic.List[string]]::new()
        $nullableAttributes = [Collections.Generic.List[string]]::new()
        $genericLines = [Collections.Generic.List[string]]::new()
        $visibleCache = @{}

        $assembly = $reader.GetAssemblyDefinition()
        $module = $reader.GetModuleDefinition()
        Add-CustomAttributeEvidence $reader $assembly 'assembly' $attributes $nullableAttributes
        Add-CustomAttributeEvidence $reader $module 'module' $attributes $nullableAttributes

        foreach ($typeHandle in $reader.TypeDefinitions) {
            if (-not (Test-VisibleType $reader $typeHandle $visibleCache)) { continue }
            $type = $reader.GetTypeDefinition($typeHandle)
            $typeName = Get-EntityTypeName $reader $typeHandle
            $baseType = Get-EntityTypeName $reader $type.BaseType
            $interfaces = @($type.GetInterfaceImplementations() | ForEach-Object {
                $implementation = $reader.GetInterfaceImplementation($_)
                Get-EntityTypeName $reader $implementation.Interface
            } | Sort-Object)
            $surface.Add("type|$typeName|flags=$([int]$type.Attributes)|base=$baseType|interfaces=$($interfaces -join ',')")
            Add-CustomAttributeEvidence $reader $type "type:$typeName" $attributes $nullableAttributes
            Add-GenericParameterEvidence $reader $type "type:$typeName" $genericLines $attributes $nullableAttributes

            $visibleMethods = @{}
            foreach ($methodHandle in $type.GetMethods()) {
                $method = $reader.GetMethodDefinition($methodHandle)
                if (-not (Test-VisibleMethodAttributes $method.Attributes)) { continue }
                $methodName = $reader.GetString($method.Name)
                $owner = "method:$typeName.${methodName}:$(Get-BlobHex $reader $method.Signature)"
                $visibleMethods[$methodHandle.GetHashCode()] = $true
                $surface.Add("$owner|flags=$([int]$method.Attributes)|impl=$([int]$method.ImplAttributes)")
                Add-CustomAttributeEvidence $reader $method $owner $attributes $nullableAttributes
                Add-GenericParameterEvidence $reader $method $owner $genericLines $attributes $nullableAttributes
                foreach ($parameterHandle in $method.GetParameters()) {
                    $parameter = $reader.GetParameter($parameterHandle)
                    $parameterOwner = "$owner|parameter:$($parameter.SequenceNumber):$($reader.GetString($parameter.Name))"
                    $constantText = 'none'
                    $constantHandle = $parameter.GetDefaultValue()
                    if (-not $constantHandle.IsNil) {
                        $constant = $reader.GetConstant($constantHandle)
                        $constantText = "$($constant.TypeCode):$(Get-BlobHex $reader $constant.Value)"
                    }
                    $surface.Add("$parameterOwner|flags=$([int]$parameter.Attributes)|default=$constantText")
                    Add-CustomAttributeEvidence $reader $parameter $parameterOwner $attributes $nullableAttributes
                }
            }

            foreach ($fieldHandle in $type.GetFields()) {
                $field = $reader.GetFieldDefinition($fieldHandle)
                if (-not (Test-VisibleFieldAttributes $field.Attributes)) { continue }
                $fieldName = $reader.GetString($field.Name)
                $owner = "field:$typeName.${fieldName}:$(Get-BlobHex $reader $field.Signature)"
                $constantText = 'none'
                $constantHandle = $field.GetDefaultValue()
                if (-not $constantHandle.IsNil) {
                    $constant = $reader.GetConstant($constantHandle)
                    $constantText = "$($constant.TypeCode):$(Get-BlobHex $reader $constant.Value)"
                }
                $surface.Add("$owner|flags=$([int]$field.Attributes)|default=$constantText")
                Add-CustomAttributeEvidence $reader $field $owner $attributes $nullableAttributes
            }

            foreach ($propertyHandle in $type.GetProperties()) {
                $property = $reader.GetPropertyDefinition($propertyHandle)
                $accessors = $property.GetAccessors()
                $tokens = @()
                if (-not $accessors.Getter.IsNil) { $tokens += $accessors.Getter.GetHashCode() }
                if (-not $accessors.Setter.IsNil) { $tokens += $accessors.Setter.GetHashCode() }
                $tokens += @($accessors.Others | ForEach-Object { $_.GetHashCode() })
                if (@($tokens | Where-Object { $visibleMethods.ContainsKey($_) }).Count -eq 0) { continue }
                $owner = "property:$typeName.$($reader.GetString($property.Name)):$(Get-BlobHex $reader $property.Signature)"
                $surface.Add("$owner|flags=$([int]$property.Attributes)")
                Add-CustomAttributeEvidence $reader $property $owner $attributes $nullableAttributes
            }

            foreach ($eventHandle in $type.GetEvents()) {
                $event = $reader.GetEventDefinition($eventHandle)
                $accessors = $event.GetAccessors()
                $tokens = @()
                if (-not $accessors.Adder.IsNil) { $tokens += $accessors.Adder.GetHashCode() }
                if (-not $accessors.Remover.IsNil) { $tokens += $accessors.Remover.GetHashCode() }
                if (-not $accessors.Raiser.IsNil) { $tokens += $accessors.Raiser.GetHashCode() }
                $tokens += @($accessors.Others | ForEach-Object { $_.GetHashCode() })
                if (@($tokens | Where-Object { $visibleMethods.ContainsKey($_) }).Count -eq 0) { continue }
                $owner = "event:$typeName.$($reader.GetString($event.Name)):$(Get-EntityTypeName $reader $event.Type)"
                $surface.Add("$owner|flags=$([int]$event.Attributes)")
                Add-CustomAttributeEvidence $reader $event $owner $attributes $nullableAttributes
            }
        }

        $references = @($reader.AssemblyReferences | ForEach-Object {
            $reference = $reader.GetAssemblyReference($_)
            "$($reader.GetString($reference.Name))|$($reference.Version)|flags=$([int]$reference.Flags)|pkt=$(Get-BlobHex $reader $reference.PublicKeyOrToken)"
        } | Sort-Object)
        $surfaceLines = @($surface | Sort-Object -Unique)
        $attributeLines = @($attributes | Sort-Object -Unique)
        $nullableLines = @($nullableAttributes | Sort-Object -Unique)
        $generics = @($genericLines | Sort-Object -Unique)
        return [ordered]@{
            visibleMetadataEntries = $surfaceLines.Count
            surfaceMetadataSha256 = Get-Sha256Text (($surfaceLines -join "`n") + "`n")
            attributeEntries = $attributeLines.Count
            attributeMetadataSha256 = Get-Sha256Text (($attributeLines -join "`n") + "`n")
            nullableAttributeEntries = $nullableLines.Count
            nullableMetadataSha256 = Get-Sha256Text (($nullableLines -join "`n") + "`n")
            genericEntries = $generics.Count
            genericMetadataSha256 = Get-Sha256Text (($generics -join "`n") + "`n")
            assemblyReferences = $references
            assemblyReferenceSha256 = Get-Sha256Text (($references -join "`n") + "`n")
        }
    }
    finally {
        $pe.Dispose()
        $stream.Dispose()
    }
}

function Get-CompilationOptions {
    param([byte[]]$Bytes)
    $parts = @([Text.Encoding]::UTF8.GetString($Bytes).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    $options = [ordered]@{}
    for ($index = 0; $index + 1 -lt $parts.Count; $index += 2) { $options[$parts[$index]] = $parts[$index + 1] }
    return $options
}

function Get-SymbolEvidence {
    param(
        [string]$DllPath,
        [string]$PdbPath,
        [string]$ExpectedAssemblySha256
    )
    if (-not (Test-Path -LiteralPath $DllPath -PathType Leaf)) { throw "Release assembly output is missing: $DllPath" }
    if (-not (Test-Path -LiteralPath $PdbPath -PathType Leaf)) { throw "Release PDB output is missing: $PdbPath" }
    $dllSha256 = (Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $assemblyStream = [IO.File]::OpenRead($DllPath)
    $pe = [Reflection.PortableExecutable.PEReader]::new($assemblyStream)
    try {
        $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $module = $reader.GetModuleDefinition()
        $mvid = $reader.GetGuid($module.Mvid).ToString('D')
        $debugEntries = @($pe.ReadDebugDirectory())
        $reproducible = @($debugEntries | Where-Object { $_.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::Reproducible }).Count -eq 1
        $embeddedPdb = @($debugEntries | Where-Object { $_.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::EmbeddedPortablePdb }).Count -ne 0
        $codeViewEntries = @($debugEntries | Where-Object { $_.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView })
        if ($codeViewEntries.Count -ne 1) { throw "Expected one CodeView debug entry: $DllPath" }
        $codeView = $pe.ReadCodeViewDebugDirectoryData($codeViewEntries[0])
    }
    finally {
        $pe.Dispose()
        $assemblyStream.Dispose()
    }

    $pdbBytes = [IO.File]::ReadAllBytes($PdbPath)
    $portable = $pdbBytes.Length -ge 4 -and [Text.Encoding]::ASCII.GetString($pdbBytes, 0, 4) -ceq 'BSJB'
    if (-not $portable) { throw "Release PDB is not portable: $PdbPath" }
    $pdbStream = [IO.MemoryStream]::new($pdbBytes, $false)
    $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($pdbStream)
    try {
        $pdbReader = $provider.GetMetadataReader()
        [byte[]]$pdbIdBytes = @($pdbReader.DebugMetadataHeader.Id)
        $documents = [Collections.Generic.List[string]]::new()
        $documentNames = @{}
        foreach ($documentHandle in $pdbReader.Documents) {
            $document = $pdbReader.GetDocument($documentHandle)
            $name = $pdbReader.GetString($document.Name)
            $token = $documentHandle.GetHashCode()
            $documentNames[$token] = $name
            $documents.Add("$name|algorithm=$($pdbReader.GetGuid($document.HashAlgorithm))|hash=$(Get-BlobHex $pdbReader $document.Hash)")
        }

        $sourceLinkText = $null
        $compilationOptions = [ordered]@{}
        $embeddedSourceCount = 0
        $moduleHandle = [Reflection.Metadata.Ecma335.MetadataTokens]::EntityHandle(1)
        foreach ($handle in $pdbReader.GetCustomDebugInformation($moduleHandle)) {
            $information = $pdbReader.GetCustomDebugInformation($handle)
            $kind = $pdbReader.GetGuid($information.Kind)
            [byte[]]$value = @($pdbReader.GetBlobBytes($information.Value))
            if ($kind -eq $sourceLinkKind) { $sourceLinkText = [Text.Encoding]::UTF8.GetString($value) }
            elseif ($kind -eq $compilationOptionsKind) { $compilationOptions = Get-CompilationOptions $value }
            elseif ($kind -eq $embeddedSourceKind) { $embeddedSourceCount++ }
        }
        foreach ($documentHandle in $pdbReader.Documents) {
            foreach ($handle in $pdbReader.GetCustomDebugInformation($documentHandle)) {
                $information = $pdbReader.GetCustomDebugInformation($handle)
                if ($pdbReader.GetGuid($information.Kind) -eq $embeddedSourceKind) { $embeddedSourceCount++ }
            }
        }

        $sequenceLines = [Collections.Generic.List[string]]::new()
        foreach ($debugHandle in $pdbReader.MethodDebugInformation) {
            $debug = $pdbReader.GetMethodDebugInformation($debugHandle)
            $defaultDocumentToken = if ($debug.Document.IsNil) { 0 } else { $debug.Document.GetHashCode() }
            foreach ($point in $debug.GetSequencePoints()) {
                $documentToken = if ($point.Document.IsNil) { $defaultDocumentToken } else { $point.Document.GetHashCode() }
                $documentName = if ($documentNames.ContainsKey($documentToken)) { $documentNames[$documentToken] } else { 'none' }
                $sequenceLines.Add("$($debugHandle.GetHashCode())|$($point.Offset)|$documentName|$($point.StartLine):$($point.StartColumn)-$($point.EndLine):$($point.EndColumn)|hidden=$($point.IsHidden)")
            }
        }

        $sourceLinkStatus = 'missing'
        $sourceLinkSha256 = $null
        $sourceLinkCommit = $null
        if (-not [string]::IsNullOrWhiteSpace($sourceLinkText)) {
            $sourceLinkSha256 = Get-Sha256Text $sourceLinkText
            try {
                $sourceLink = $sourceLinkText | ConvertFrom-Json -AsHashtable
                $mappings = @($sourceLink.documents.GetEnumerator())
                if ($mappings.Count -eq 1 -and $mappings[0].Value -match '/([0-9a-f]{40})/\*$') {
                    $sourceLinkCommit = $Matches[1]
                    $sourceLinkStatus = if ($sourceLinkCommit -eq $head) { 'present-valid-head' } else { 'present-wrong-commit' }
                }
                else { $sourceLinkStatus = 'present-invalid-map' }
            }
            catch { $sourceLinkStatus = 'present-invalid-json' }
        }

        $documentPaths = @($documentNames.Values | Sort-Object -Unique)
        # /_/ is the compiler's deterministic virtual source root, not a local filesystem path.
        $physicalAbsolutePaths = @($documentPaths | Where-Object { [IO.Path]::IsPathRooted($_) -and -not $_.StartsWith('/_/', [StringComparison]::Ordinal) }).Count
        $pathMode = if ($documentPaths.Count -gt 0 -and $physicalAbsolutePaths -eq $documentPaths.Count) { 'absolute' } elseif ($physicalAbsolutePaths -eq 0) { 'mapped-or-relative' } else { 'mixed' }
        $documentLines = @($documents | Sort-Object)
        $sequencePointLines = @($sequenceLines | Sort-Object)
        return [ordered]@{
            assemblyBytes = (Get-Item -LiteralPath $DllPath).Length
            assemblySha256 = $dllSha256
            packageAssemblySha256 = $ExpectedAssemblySha256
            packageBuildAssemblyMatch = $dllSha256 -eq $ExpectedAssemblySha256
            mvid = $mvid
            deterministicReproducibleMarker = $reproducible
            debugType = if ($embeddedPdb) { 'embedded-portable' } else { 'portable' }
            codeViewGuid = $codeView.Guid.ToString('D')
            codeViewAge = $codeView.Age
            codeViewPathMode = if ([IO.Path]::IsPathRooted($codeView.Path)) { 'absolute' } else { 'relative' }
            pdbBytes = $pdbBytes.Length
            pdbSha256 = Get-Sha256Bytes $pdbBytes
            portablePdbId = ([Convert]::ToHexString($pdbIdBytes)).ToLowerInvariant()
            documentCount = $documentLines.Count
            documentPathMode = $pathMode
            documentSha256 = Get-Sha256Text (($documentLines -join "`n") + "`n")
            sequencePointSha256 = Get-Sha256Text (($sequencePointLines -join "`n") + "`n")
            sourceLinkStatus = $sourceLinkStatus
            sourceLinkCommit = $sourceLinkCommit
            sourceLinkSha256 = $sourceLinkSha256
            embeddedSourceCount = $embeddedSourceCount
            compilerVersion = [string]$compilationOptions['compiler-version']
            languageVersion = [string]$compilationOptions['language-version']
            optimization = [string]$compilationOptions['optimization']
            nullable = [string]$compilationOptions['nullable']
            runtimeVersion = [string]$compilationOptions['runtime-version']
            compilationOptionsSha256 = Get-Sha256Text (Get-CanonicalJson $compilationOptions)
        }
    }
    finally {
        $provider.Dispose()
        $pdbStream.Dispose()
    }
}

function Get-AssemblyReferenceNames {
    param([byte[]]$AssemblyBytes)
    $metadata = Get-PublicMetadataEvidence $AssemblyBytes
    return @($metadata.assemblyReferences)
}

function Get-SemanticPackageDigest {
    param([IO.Compression.ZipArchive]$Archive)
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($entry in $Archive.Entries) {
        if ($entry.FullName -eq '_rels/.rels' -or
            $entry.FullName -eq '.signature.p7s' -or
            $entry.FullName -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' -or
            $entry.FullName -match '^package/services/digital-signature/') { continue }
        $lines.Add("$($entry.FullName)|$($entry.Length)|$(Get-EntrySha256 $entry)")
    }
    $values = @($lines | Sort-Object)
    return Get-Sha256Text (($values -join "`n") + "`n")
}

function Get-PackageEntries {
    param([IO.Compression.ZipArchive]$Archive)
    $entries = @{}
    foreach ($entry in $Archive.Entries) {
        if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate NuGet payload: $($entry.FullName)" }
        $entries[$entry.FullName] = $entry
    }
    return $entries
}

function Get-DependencyGroupMap {
    param([xml]$Nuspec)
    $groups = @{}
    foreach ($group in @($Nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))) {
        $dependencies = @($group.SelectNodes("*[local-name()='dependency']") | ForEach-Object {
            [ordered]@{ id = [string]$_.id; version = [string]$_.version; exclude = [string]$_.exclude }
        } | Sort-Object { $_.id }, { $_.version })
        $groups[[string]$group.targetFramework] = $dependencies
    }
    return $groups
}

function Get-ObjectMap {
    param([object[]]$Values, [string]$Key)
    $map = @{}
    foreach ($value in @($Values)) {
        $property = if ($value -is [Collections.IDictionary]) { $value[$Key] } else { $value.PSObject.Properties[$Key].Value }
        $map[[string]$property] = $value
    }
    return $map
}

function Assert-SameKeySet {
    param([object[]]$Expected, [object[]]$Actual, [string]$Description)
    $expectedValues = @($Expected | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $actualValues = @($Actual | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    if ($expectedValues.Count -ne $actualValues.Count -or (Compare-Object $expectedValues $actualValues)) {
        throw "$Description drift. Expected '$($expectedValues -join ',')'; found '$($actualValues -join ',')'."
    }
}

$definitions = @($releaseBaseline.packages)
$expectedSymbolPackageFiles = @($definitions | ForEach-Object { "$($_.packageId).$($releaseBaseline.packageVersion).snupkg" })
$actualSymbolPackageFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.snupkg' | Select-Object -ExpandProperty Name)
$symbolPackageRecords = [Collections.Generic.List[object]]::new()
switch ([string]$releaseBaseline.symbolPolicy) {
    'required-snupkg' {
        Assert-SameKeySet $expectedSymbolPackageFiles $actualSymbolPackageFiles 'Release symbol package files'
        foreach ($definition in $definitions) {
            $symbolPackagePath = Join-Path $packageRoot "$($definition.packageId).$($releaseBaseline.packageVersion).snupkg"
            $archive = [IO.Compression.ZipFile]::OpenRead($symbolPackagePath)
            try {
                $expectedPdbEntries = @($definition.targetFrameworks | ForEach-Object { "lib/$_/$($definition.assemblyName).pdb" })
                $actualPdbEntries = @($archive.Entries | Where-Object { $_.FullName -match '^lib/[^/]+/[^/]+\.pdb$' } | Select-Object -ExpandProperty FullName)
                Assert-SameKeySet $expectedPdbEntries $actualPdbEntries "$($definition.packageId) symbol PDB entries"
                $symbolPackageRecords.Add([ordered]@{
                    id = $definition.packageId
                    rawPackageBytes = (Get-Item -LiteralPath $symbolPackagePath).Length
                    rawPackageSha256 = Get-FileSha256 $symbolPackagePath
                    pdbEntries = @($actualPdbEntries | Sort-Object)
                })
            }
            finally { $archive.Dispose() }
        }
    }
    'not-produced' {
        if ($actualSymbolPackageFiles.Count -ne 0) { throw 'The release baseline forbids .snupkg files.' }
    }
    default { throw "Unsupported symbol policy: $($releaseBaseline.symbolPolicy)." }
}
$componentAccumulator = @{}
$projectState = @{}

foreach ($definition in $definitions) {
    $projectPath = Join-Path $repository ([string]$definition.projectPath).Replace('/', '\')
    $projectDirectory = Split-Path -Parent $projectPath
    $lockPath = Join-Path $projectDirectory 'packages.lock.json'
    $assetsPath = Join-Path $projectDirectory 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf) -or -not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
        throw "Lock/assets input is missing for $($definition.packageId)."
    }
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json -AsHashtable
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    if ([int]$lock.version -ne 2 -or [int]$assets.version -ne 4) { throw "Unsupported lock/assets schema for $($definition.packageId)." }

    $assetPackagesByTfm = @{}
    foreach ($tfm in @($definition.targetFrameworks)) {
        if (-not $assets.targets.ContainsKey($tfm)) { throw "Restore assets target '$tfm' is missing for $($definition.packageId)." }
        $resolved = [Collections.Generic.List[string]]::new()
        foreach ($targetLibrary in $assets.targets[$tfm].GetEnumerator()) {
            if (-not $assets.libraries.ContainsKey($targetLibrary.Key) -or $assets.libraries[$targetLibrary.Key].type -ne 'package') { continue }
            if (Test-NativeRuntimePackage (($targetLibrary.Key -split '/', 2)[0])) { throw "Release restore graph contains consumer-owned native runtime '$($targetLibrary.Key)'." }
            $resolved.Add($targetLibrary.Key)
            if (-not $componentAccumulator.ContainsKey($targetLibrary.Key)) {
                $library = $assets.libraries[$targetLibrary.Key]
                $parts = $targetLibrary.Key -split '/', 2
                $componentAccumulator[$targetLibrary.Key] = [ordered]@{
                    id = $parts[0]
                    version = $parts[1]
                    contentHash = [string]$library.sha512
                    usedBy = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                }
            }
            $null = $componentAccumulator[$targetLibrary.Key].usedBy.Add("$($definition.packageId)|$tfm")
        }
        $assetPackagesByTfm[$tfm] = @($resolved | Sort-Object)
    }

    foreach ($framework in $lock.dependencies.GetEnumerator()) {
        foreach ($dependency in $framework.Value.GetEnumerator()) {
            if (Test-NativeRuntimePackage $dependency.Key) { throw "Release lock graph contains consumer-owned native runtime '$($dependency.Key)'." }
            if ($dependency.Key -like 'JYPPX.DeploySharp.*') { continue }
            $version = [string]$dependency.Value.resolved
            $contentHash = [string]$dependency.Value.contentHash
            if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($contentHash)) { throw "Incomplete lock identity for $($dependency.Key) on $($framework.Key)." }
            $key = "$($dependency.Key)/$version"
            if (-not $componentAccumulator.ContainsKey($key)) {
                $componentAccumulator[$key] = [ordered]@{
                    id = $dependency.Key
                    version = $version
                    contentHash = $contentHash
                    usedBy = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                }
            }
            elseif ($componentAccumulator[$key].contentHash -ne $contentHash) { throw "Dependency content hash drift for $key." }
        }
    }

    $projectState[$definition.packageId] = [ordered]@{
        projectPath = $projectPath
        projectDirectory = $projectDirectory
        lockPath = $lockPath
        assetsPath = $assetsPath
        assets = $assets
        packagesByTfm = $assetPackagesByTfm
    }
}

function Complete-ReleaseEvidence {
$qwenManifestPath = Join-Path $repository 'eng\models\llm\manifests\qwen2.5-0.5b-instruct-q4-k-m.modelpack.json'
$qwenManifest = Get-Content -LiteralPath $qwenManifestPath -Raw | ConvertFrom-Json -AsHashtable
$qwenArtifact = @($qwenManifest.artifacts | Where-Object { $_.format -eq 'gguf' })
if ($qwenArtifact.Count -ne 1) { throw 'The exact Qwen GGUF manifest must contain one GGUF artifact.' }
$qwenExtensions = $qwenArtifact[0].extensions
$qwenRootExtensions = $qwenManifest.extensions
$qwenModelFile = @($qwenArtifact[0].files | Where-Object { $_.role -eq 'model' })
if ($qwenModelFile.Count -ne 1) { throw 'The exact Qwen GGUF manifest must contain one model file.' }
$officialCatalogPath = Join-Path $repository 'src\DeploySharp.ModelFactory\catalog\deploysharp-official-catalog.json'
$officialCatalog = Get-Content -LiteralPath $officialCatalogPath -Raw | ConvertFrom-Json -AsHashtable
$officialEntries = @($officialCatalog.entries)
if ([string]$officialCatalog.schemaVersion -ne '1.0' -or [string]::IsNullOrWhiteSpace([string]$officialCatalog.catalogRevision) -or $officialEntries.Count -eq 0) {
    throw 'The official model catalog identity is invalid.'
}
$officialModelIds = @($officialEntries | ForEach-Object { [string]$_.modelId })
if (@($officialModelIds | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0 -or @($officialModelIds | Sort-Object -Unique).Count -ne $officialEntries.Count) {
    throw 'The official model catalog must contain unique non-empty model IDs.'
}
$officialQwen = @($officialEntries | Where-Object { $_.modelId -eq 'llm/qwen2.5-0.5b-instruct-q4-k-m' })
if ($officialQwen.Count -ne 1) { throw 'The published Qwen alpha-preview catalog entry is missing.' }
if ([string]$officialQwen[0].status -ne 'preview' -or -not [bool]$officialQwen[0].source.redistributionAllowed -or [string]$officialQwen[0].source.licenseExpression -ne 'Apache-2.0' -or [string]$officialQwen[0].release.tag -ne 'models-llm.1' -or [string]$officialQwen[0].release.commit -ne 'd8c4ffaed3684d120f80dec832c74a1a83e562a5') {
    throw 'The published Qwen alpha-preview catalog provenance drifted.'
}
$officialQwenAssets = @($officialQwen[0].artifacts[0].assets)
if ($officialQwen[0].artifacts.Count -ne 1 -or $officialQwenAssets.Count -ne 7 -or @($officialQwenAssets | Where-Object { $_.assetId -eq 'qwen-model' -and $_.size -eq 491400032 -and $_.sha256 -eq '74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db' }).Count -ne 1) {
    throw 'The published Qwen alpha-preview catalog assets drifted.'
}
$thirdPartyNoticesPath = Join-Path $repository 'THIRD-PARTY-NOTICES.md'
if (-not (Test-Path -LiteralPath $thirdPartyNoticesPath -PathType Leaf)) { throw 'THIRD-PARTY-NOTICES.md is missing.' }
$thirdPartyNoticesText = Get-Content -LiteralPath $thirdPartyNoticesPath -Raw
$noticePackageIds = [Collections.Generic.List[string]]::new()
foreach ($package in $releaseBaseline.centralPackages.PSObject.Properties) {
    $packageId = [string]$package.Name
    $packageVersion = [string]$package.Value
    if ($thirdPartyNoticesText.IndexOf($packageId, [StringComparison]::Ordinal) -lt 0 -or $thirdPartyNoticesText.IndexOf($packageVersion, [StringComparison]::Ordinal) -lt 0) {
        throw "Third-party notice is missing central dependency identity: $packageId/$packageVersion."
    }
    $noticePackageIds.Add($packageId)
}
if ($thirdPartyNoticesText.IndexOf('not an approval of any model license', [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
    $thirdPartyNoticesText.IndexOf('does not relicense', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw 'Third-party notice ownership/model boundary is missing.'
}
$thirdPartyNoticesEvidence = [ordered]@{
    path = 'THIRD-PARTY-NOTICES.md'
    bytes = (Get-Item -LiteralPath $thirdPartyNoticesPath).Length
    sha256 = Get-FileSha256 $thirdPartyNoticesPath
    centralPackageIds = @($noticePackageIds | Sort-Object)
    centralPackageCount = $noticePackageIds.Count
    modelRedistributionApproval = $false
}
$modelLicenses = @([ordered]@{
    modelId = $qwenManifest.modelId
    ownership = 'external-model'
    licenseExpression = $qwenManifest.source.licenseExpression
    licenseSource = 'modelpack-source-license-and-sidecar'
    licenseFile = $qwenManifest.source.licenseFile
    redistributionAllowed = [bool]$qwenManifest.source.redistributionAllowed
    algorithmVerified = [string]$qwenRootExtensions['deploysharp.algorithm-verified'] -eq 'true'
    uploaded = [string]$qwenRootExtensions['deploysharp.uploaded'] -eq 'true'
    downloadable = [string]$qwenRootExtensions['deploysharp.downloadable'] -eq 'true'
    modelBytes = [int64]$qwenModelFile[0].size
    modelSha256 = [string]$qwenModelFile[0].sha256
    admissionStatus = [string]$qwenRootExtensions['deploysharp.stage31-admission-status']
    releaseBlocker = [string]$qwenExtensions['deploysharp.blocker']
})
if ($modelLicenses[0].ownership -ne 'external-model' -or $modelLicenses[0].redistributionAllowed -or $modelLicenses[0].algorithmVerified -or $modelLicenses[0].uploaded -or $modelLicenses[0].downloadable) {
    throw 'The exact Qwen external/publication boundary drifted.'
}

# `applications/` is a separately managed workspace and is intentionally
# excluded from the DeploySharp project release gates.
$dirtyEntries = @(& git -C $repository status --porcelain --untracked-files=all | Where-Object {
    [string]$_ -notmatch '^.{2}\s+applications(?:[\\/]|$)'
})
$dirty = $dirtyEntries.Count -ne 0
$licenseBlockers = @($managedDependencies | Where-Object { $_.license.manualReview } | ForEach-Object { "dependency-license-review:$($_.id)/$($_.version):$($_.license.status)" })
$nativeLicenseBlockers = @($consumerOwnedNativeRuntimes | Where-Object { $_.license.manualReview } | ForEach-Object { "consumer-native-license-review:$($_.id)/$($_.version):$($_.license.status)" })
$repositoryBlockers = @($managedDependencies | Where-Object { $_.repository.status -ne 'complete' } | ForEach-Object { "dependency-repository-incomplete:$($_.id)/$($_.version)" })
$resolvedPolicyPath = (Resolve-Path -LiteralPath $ReleasePolicyPath).Path
$releasePolicy = Get-Content -LiteralPath $resolvedPolicyPath -Raw | ConvertFrom-Json -AsHashtable
if ([string]$releasePolicy.schemaVersion -ne '1.0' -or [bool]$releasePolicy.commercialRelease.knownFindingsRemainBlocking -ne $true) {
    throw 'Release evidence policy schema/commercial boundary is invalid.'
}
$previewPolicies = @($releasePolicy.profiles | Where-Object { $_.id -eq 'oss-noncommercial-alpha-preview' })
if ($previewPolicies.Count -ne 1) { throw 'Release evidence policy must define exactly one alpha preview profile.' }
$previewPolicy = $previewPolicies[0]
if ($releaseBaseline.packageVersion -ne '2.0.0-alpha.1' -or [string]$previewPolicy.packageVersion -ne '2.0.0-alpha.1') {
    throw 'Alpha preview policy is constrained to package version 2.0.0-alpha.1.'
}
if ([string]$previewPolicy.distributionScope -ne 'open-source-non-commercial-preview' -or [bool]$previewPolicy.commercialRelease) {
    throw 'Alpha preview policy distribution scope is invalid.'
}
$expectedAdvisoryPrefixes = @('consumer-native-license-review:', 'dependency-license-review:', 'dependency-repository-incomplete:')
Assert-SameKeySet @($previewPolicy.advisoryFindingPrefixes) $expectedAdvisoryPrefixes 'Alpha preview policy advisory prefixes'
$expectedFindingCounts = $previewPolicy.expectedFindingCounts
if ([int]$expectedFindingCounts.managedDependencyLicenseReview -ne 20 -or [int]$expectedFindingCounts.consumerNativeLicenseReview -ne 2 -or [int]$expectedFindingCounts.dependencyRepositoryIncomplete -ne 18) {
    throw 'Alpha preview policy must retain the exact 20/2/18 finding counts.'
}
$knownAdvisoryFindings = @($licenseBlockers + $nativeLicenseBlockers + $repositoryBlockers | Sort-Object -Unique)
foreach ($finding in $knownAdvisoryFindings) {
    if (@($expectedAdvisoryPrefixes | Where-Object { $finding.StartsWith($_, [StringComparison]::Ordinal) }).Count -ne 1) {
        throw "Alpha preview policy cannot classify finding: $finding"
    }
}
if ($licenseBlockers.Count -ne $expectedFindingCounts.managedDependencyLicenseReview -or $nativeLicenseBlockers.Count -ne $expectedFindingCounts.consumerNativeLicenseReview -or $repositoryBlockers.Count -ne $expectedFindingCounts.dependencyRepositoryIncomplete) {
    throw "Alpha preview advisory finding count drift. Expected 20 managed licenses, 2 consumer-native licenses, and 18 repository records; found $($licenseBlockers.Count), $($nativeLicenseBlockers.Count), $($repositoryBlockers.Count)."
}
$releaseBlockers = [Collections.Generic.List[string]]::new()
if ($dirty) { $releaseBlockers.Add('dirty-worktree') }
if ($releaseAuthorization.packageSigning.status -notin @('configured', 'not-required-alpha-preview')) { throw "Unsupported package signing status: $($releaseAuthorization.packageSigning.status)." }
if ($releaseBaseline.packageSigningPolicy -eq 'required' -and $signedCount -ne $definitions.Count) { $releaseBlockers.Add('unsigned-packages') }
if ($releaseBaseline.symbolPolicy -eq 'not-produced') { $releaseBlockers.Add('symbol-package-policy-not-authorized') }
if ($releaseAuthorization.publication.status -ne 'authorized') { $releaseBlockers.Add('publication-authority-not-granted') }

$symbolBlockers = [Collections.Generic.List[string]]::new()
if ($releaseBaseline.symbolPolicy -eq 'not-produced') { $symbolBlockers.Add('symbol-package-policy-not-authorized') }
if ($sourceLinkValidCount -ne $symbolAssemblies.Count) { $symbolBlockers.Add('sourcelink-missing-or-drifted') }
if ($absolutePdbPathCount -ne 0) { $symbolBlockers.Add('portable-pdb-contains-absolute-source-paths') }
if ($packageBuildDriftCount -ne 0) { $symbolBlockers.Add("package-build-assembly-drift:$packageBuildDriftCount") }
foreach ($blocker in $symbolBlockers) { $releaseBlockers.Add($blocker) }

$commercialReleaseBlockers = [Collections.Generic.List[string]]::new()
foreach ($blocker in $releaseBlockers) { $commercialReleaseBlockers.Add($blocker) }
if ($releaseBaseline.packageSigningPolicy -eq 'optional-alpha-preview-required-ga-commercial' -and $signedCount -ne $definitions.Count) { $commercialReleaseBlockers.Add('unsigned-packages') }
foreach ($finding in $knownAdvisoryFindings) { $commercialReleaseBlockers.Add($finding) }
$alphaPreviewPolicyEvidence = [ordered]@{
    id = [string]$previewPolicy.id
    packageVersion = [string]$previewPolicy.packageVersion
    distributionScope = [string]$previewPolicy.distributionScope
    commercialRelease = [bool]$previewPolicy.commercialRelease
    commercialKnownFindingsRemainBlocking = [bool]$releasePolicy.commercialRelease.knownFindingsRemainBlocking
    policySha256 = Get-FileSha256 $resolvedPolicyPath
    advisoryFindingPrefixes = @($expectedAdvisoryPrefixes | Sort-Object)
    expectedFindingCounts = [ordered]@{
        managedDependencyLicenseReview = [int]$expectedFindingCounts.managedDependencyLicenseReview
        consumerNativeLicenseReview = [int]$expectedFindingCounts.consumerNativeLicenseReview
        dependencyRepositoryIncomplete = [int]$expectedFindingCounts.dependencyRepositoryIncomplete
    }
}

$provenance = [ordered]@{
    schemaVersion = '1.0'
    format = [ordered]@{
        name = 'DeploySharpReleaseEvidence'
        mediaType = 'application/vnd.deploysharp.release-evidence+json'
        standard = 'custom'
        spdx = $false
        cyclonedx = $false
        conclusion = 'A validated DeploySharp-specific provenance/SBOM document; it does not claim SPDX or CycloneDX conformance.'
    }
    subject = [ordered]@{
        name = 'DeploySharp'
        version = $releaseBaseline.packageVersion
        repositoryUrl = $releaseBaseline.repositoryUrl
        repositoryCommit = $head
        repositoryDirty = $dirty
    }
    ownershipScopes = @(
        [ordered]@{ name = 'deploysharp-package'; rule = 'Only DeploySharp managed DLL/XML payload and nuspec dependencies are shipped.' },
        [ordered]@{ name = 'managed-dependency'; rule = 'Resolved managed package dependencies are recorded from lock/assets and local NuGet metadata.' },
        [ordered]@{ name = 'consumer-owned-native-runtime'; rule = 'Optional native runtime packages are selected and licensed by the consumer and are absent from DeploySharp package dependencies/payload.' },
        [ordered]@{ name = 'external-model'; rule = 'Model license and redistribution state are independent from NuGet package/runtime licenses.' }
    )
    releasePackages = @($releasePackages)
    managedDependencies = @($managedDependencies)
    consumerOwnedNativeRuntimes = @($consumerOwnedNativeRuntimes)
    modelLicenses = $modelLicenses
    officialCatalogEntries = $officialEntries.Count
    officialCatalog = [ordered]@{
        schemaVersion = [string]$officialCatalog.schemaVersion
        revision = [string]$officialCatalog.catalogRevision
        entries = $officialEntries.Count
        bytes = (Get-Item -LiteralPath $officialCatalogPath).Length
        sha256 = Get-FileSha256 $officialCatalogPath
    }
    thirdPartyNotices = $thirdPartyNoticesEvidence
    alphaPreviewPolicy = $alphaPreviewPolicyEvidence
    releaseAuthorization = [ordered]@{
        publicationStatus = [string]$releaseAuthorization.publication.status
        packageSigningStatus = [string]$releaseAuthorization.packageSigning.status
        rawPackageReproducibilityStatus = [string]$releaseAuthorization.rawPackageReproducibility.status
        policySha256 = Get-FileSha256 $resolvedAuthorizationPath
    }
    knownAdvisoryFindings = $knownAdvisoryFindings
    releaseBlockers = @($releaseBlockers | Sort-Object -Unique)
    commercialReleaseBlockers = @($commercialReleaseBlockers | Sort-Object -Unique)
    summary = [ordered]@{
        releasePackages = $releasePackages.Count
        targetFrameworkGroups = @($releasePackages | ForEach-Object { $_.frameworks }).Count
        managedDependencyComponents = $managedDependencies.Count
        consumerOwnedNativeComponents = $consumerOwnedNativeRuntimes.Count
        deploySharpPackageLicenses = @($releasePackages | Where-Object { $_.licenseExpression -eq 'Apache-2.0' }).Count
        verifiedSpdxManagedDependencies = @($managedDependencies | Where-Object { -not $_.license.manualReview }).Count
        managedDependencyLicenseBlockers = $licenseBlockers.Count
        consumerNativeLicenseBlockers = $nativeLicenseBlockers.Count
        dependencyRepositoryIncompleteFindings = $repositoryBlockers.Count
        alphaPreviewKnownAdvisoryFindings = $knownAdvisoryFindings.Count
        signedPackages = $signedCount
        alphaPreviewCandidateEligible = $releaseBlockers.Count -eq 0
        commercialReleaseEligible = $commercialReleaseBlockers.Count -eq 0
        releaseEligible = $releaseBlockers.Count -eq 0
    }
}

$symbols = [ordered]@{
    schemaVersion = '1.0'
    format = [ordered]@{
        name = 'DeploySharpReleaseSymbolsEvidence'
        mediaType = 'application/vnd.deploysharp.release-symbols+json'
        standard = 'custom'
    }
    repositoryCommit = $head
    configuration = $Configuration
    sdkIdentity = [ordered]@{
        globalJsonVersion = [string]$globalJson.sdk.version
        globalJsonRollForward = [string]$globalJson.sdk.rollForward
        globalJsonAllowPrerelease = [bool]$globalJson.sdk.allowPrerelease
        actualSdkVersion = $actualSdkVersion
        msbuildVersion = [string]$msbuildVersion[0]
    }
    deterministicSetting = $true
    symbolPackagePolicy = $releaseBaseline.symbolPolicy
    rawSnupkgReproducibility = if ($releaseBaseline.symbolPolicy -eq 'required-snupkg') { 'two-independent-pack-invocations-normalized-and-compared-before-signing' } else { 'not-applicable-not-produced' }
    assemblySymbolSemanticDefinition = 'MVID, deterministic marker, portable PDB ID/documents/sequence points, compiler options, and SourceLink identity.'
    rawNupkgDefinition = 'SHA256 of the complete NuGet ZIP container; tracked separately from semantic payload.'
    assemblies = @($symbolAssemblies | Sort-Object packageId, tfm)
    symbolPackages = @($symbolPackageRecords | Sort-Object id)
    blockers = @($symbolBlockers | Sort-Object -Unique)
    summary = [ordered]@{
        assemblies = $symbolAssemblies.Count
        deterministicAssemblies = @($symbolAssemblies | Where-Object { $_.evidence.deterministicReproducibleMarker }).Count
        portablePdbs = $portablePdbCount
        validHeadSourceLink = $sourceLinkValidCount
        absoluteDocumentPaths = $absolutePdbPathCount
        packageBuildAssemblyDrifts = $packageBuildDriftCount
        embeddedSources = @($symbolAssemblies | ForEach-Object { $_.evidence.embeddedSourceCount } | Measure-Object -Sum).Sum
        symbolPackages = $symbolPackageRecords.Count
    }
}

$api = [ordered]@{
    schemaVersion = '1.0'
    format = [ordered]@{
        name = 'DeploySharpPublicApiEvidence'
        mediaType = 'application/vnd.deploysharp.public-api+json'
        standard = 'custom'
    }
    repositoryCommit = $head
    coverage = 'public/protected XML contract IDs plus visible metadata flags/signatures, generic constraints, nullable/custom attributes, defaults, and assembly references'
    packages = @($apiPackages | Sort-Object packageId)
    summary = [ordered]@{
        packages = $apiPackages.Count
        targetFrameworkContracts = $apiContractCount
        crossTfmConsistentPackages = @($apiPackages | Where-Object { $_.contractConsistency -eq 'identical-across-supported-tfms' }).Count
    }
}

function Write-EvidenceFile {
    param([string]$Path, [object]$Value)
    $json = $Value | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json + "`n", [Text.UTF8Encoding]::new($false))
}

if ($WriteBaseline) {
    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) { New-Item -ItemType Directory -Path $EvidenceDirectory | Out-Null }
    Write-EvidenceFile (Join-Path $EvidenceDirectory $provenanceFileName) $provenance
    Write-EvidenceFile (Join-Path $EvidenceDirectory $symbolsFileName) $symbols
    Write-EvidenceFile (Join-Path $EvidenceDirectory $apiFileName) $api
    Write-Output "DEPLOYSHARP_RELEASE_EVIDENCE_BASELINE_WRITTEN packages=$($releasePackages.Count) tfms=$($symbolAssemblies.Count) dependencies=$($managedDependencies.Count) native=$($consumerOwnedNativeRuntimes.Count) api-contracts=$apiContractCount"
}
else {
    $provenancePath = Join-Path $EvidenceDirectory $provenanceFileName
    $symbolsPath = Join-Path $EvidenceDirectory $symbolsFileName
    $apiPath = Join-Path $EvidenceDirectory $apiFileName
    foreach ($path in @($provenancePath, $symbolsPath, $apiPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release evidence baseline is missing: $path" }
    }
    $expectedProvenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json -AsHashtable
    $expectedSymbols = Get-Content -LiteralPath $symbolsPath -Raw | ConvertFrom-Json -AsHashtable
    $expectedApi = Get-Content -LiteralPath $apiPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($document in @($expectedProvenance, $expectedSymbols, $expectedApi)) {
        if ($document.schemaVersion -ne '1.0' -or $document.format.standard -ne 'custom') { throw 'Release evidence schema/format is invalid.' }
    }

    $baselineCommit = [string]$expectedProvenance.subject.repositoryCommit
    if ($baselineCommit -notmatch '^[0-9a-f]{40}$' -or [string]$expectedSymbols.repositoryCommit -ne $baselineCommit -or [string]$expectedApi.repositoryCommit -ne $baselineCommit) {
        throw 'Release evidence baseline commit identity is inconsistent.'
    }
    & git -C $repository merge-base --is-ancestor $baselineCommit $head 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Release evidence baseline commit is not an ancestor of HEAD: $baselineCommit." }
    if ([string]$provenance.subject.repositoryCommit -ne $head -or [string]$symbols.repositoryCommit -ne $head -or [string]$api.repositoryCommit -ne $head) {
        throw 'Live release evidence is not bound to the current repository HEAD.'
    }
    foreach ($assembly in @($symbols.assemblies)) {
        if ([string]$assembly.evidence.sourceLinkStatus -ne 'present-valid-head' -or [string]$assembly.evidence.sourceLinkCommit -ne $head) {
            throw "SourceLink is not bound to HEAD: $($assembly.packageId)/$($assembly.tfm)."
        }
    }

    Assert-SameKeySet @($expectedProvenance.releasePackages | ForEach-Object { $_.id }) @($provenance.releasePackages | ForEach-Object { $_.id }) 'SBOM release package set'
    Assert-SameKeySet @($expectedProvenance.managedDependencies | ForEach-Object { "$($_.id)/$($_.version)" }) @($provenance.managedDependencies | ForEach-Object { "$($_.id)/$($_.version)" }) 'SBOM managed dependency set'
    Assert-SameKeySet @($expectedProvenance.consumerOwnedNativeRuntimes | ForEach-Object { "$($_.id)/$($_.version)" }) @($provenance.consumerOwnedNativeRuntimes | ForEach-Object { "$($_.id)/$($_.version)" }) 'SBOM native ownership set'
    Assert-SameKeySet @($expectedProvenance.knownAdvisoryFindings) @($provenance.knownAdvisoryFindings) 'SBOM alpha preview advisory findings'
    if ((Get-CanonicalJson $expectedProvenance.officialCatalog) -ne (Get-CanonicalJson $provenance.officialCatalog)) { throw 'Official catalog identity drift.' }
    if ((Get-CanonicalJson $expectedProvenance.thirdPartyNotices) -ne (Get-CanonicalJson $provenance.thirdPartyNotices)) { throw 'Third-party notice identity drift.' }
    if ((Get-CanonicalJson $expectedProvenance.alphaPreviewPolicy) -ne (Get-CanonicalJson $provenance.alphaPreviewPolicy)) { throw 'Alpha preview release policy drift.' }
    if ((Get-CanonicalJson $expectedProvenance.releaseAuthorization) -ne (Get-CanonicalJson $provenance.releaseAuthorization)) { throw 'Release authorization policy drift.' }

    $expectedNative = Get-ObjectMap $expectedProvenance.consumerOwnedNativeRuntimes 'id'
    $actualNative = Get-ObjectMap $provenance.consumerOwnedNativeRuntimes 'id'
    foreach ($id in $expectedNative.Keys) {
        if ($expectedNative[$id].ownership -ne $actualNative[$id].ownership -or $actualNative[$id].ownership -ne 'consumer-owned-native-runtime') { throw "Native ownership drift: $id." }
    }

    $expectedManaged = @{}
    foreach ($component in @($expectedProvenance.managedDependencies)) {
        $expectedManaged["$($component.id)/$($component.version)"] = $component
    }
    $actualManaged = @{}
    foreach ($component in $provenance.managedDependencies) { $actualManaged["$($component.id)/$($component.version)"] = $component }
    foreach ($key in $expectedManaged.Keys) {
        if ((Get-CanonicalJson $expectedManaged[$key].license) -ne (Get-CanonicalJson $actualManaged[$key].license)) { throw "License baseline drift: $key." }
        if ($expectedManaged[$key].resolvedContentHash -ne $actualManaged[$key].resolvedContentHash -or $expectedManaged[$key].cachedNupkgSha512 -ne $actualManaged[$key].cachedNupkgSha512 -or $expectedManaged[$key].nupkgSha256 -ne $actualManaged[$key].nupkgSha256) { throw "Dependency content hash drift: $key." }
    }

    $expectedRelease = Get-ObjectMap $expectedProvenance.releasePackages 'id'
    $actualRelease = Get-ObjectMap $provenance.releasePackages 'id'
    $rawMatches = 0
    foreach ($id in $expectedRelease.Keys) {
        if ([string]$expectedRelease[$id].repositoryCommit -ne $baselineCommit) { throw "Retained repository commit drift: $id." }
        if ([string]$actualRelease[$id].repositoryCommit -ne $head) { throw "Repository commit drift: $id." }
        if ($expectedRelease[$id].rawPackageSha256 -eq $actualRelease[$id].rawPackageSha256) { $rawMatches++ }
    }

    $normalizedExpected = Get-CanonicalValue $expectedProvenance
    $normalizedActual = Get-CanonicalValue $provenance
    $normalizedActual.subject.repositoryCommit = $normalizedExpected.subject.repositoryCommit
    # A retained baseline's dirty flag is historical; every other blocker must still match exactly.
    $expectedNonWorktreeBlockers = @($normalizedExpected.releaseBlockers | Where-Object { $_ -ne 'dirty-worktree' })
    $actualNonWorktreeBlockers = @($normalizedActual.releaseBlockers | Where-Object { $_ -ne 'dirty-worktree' })
    Assert-SameKeySet $expectedNonWorktreeBlockers $actualNonWorktreeBlockers 'SBOM release blockers excluding worktree state'
    $expectedCommercialNonWorktreeBlockers = @($normalizedExpected.commercialReleaseBlockers | Where-Object { $_ -ne 'dirty-worktree' })
    $actualCommercialNonWorktreeBlockers = @($normalizedActual.commercialReleaseBlockers | Where-Object { $_ -ne 'dirty-worktree' })
    Assert-SameKeySet $expectedCommercialNonWorktreeBlockers $actualCommercialNonWorktreeBlockers 'SBOM commercial release blockers excluding worktree state'
    $normalizedActual.subject.repositoryDirty = $normalizedExpected.subject.repositoryDirty
    $normalizedActual.releaseBlockers = $normalizedExpected.releaseBlockers
    $normalizedActual.commercialReleaseBlockers = $normalizedExpected.commercialReleaseBlockers
    $normalizedExpectedRelease = Get-ObjectMap $normalizedExpected.releasePackages 'id'
    $normalizedActualRelease = Get-ObjectMap $normalizedActual.releasePackages 'id'
    foreach ($id in $normalizedExpectedRelease.Keys) {
        foreach ($field in @('repositoryCommit', 'rawPackageSha256', 'rawPackageBytes', 'semanticPayloadSha256')) {
            $normalizedActualRelease[$id][$field] = $normalizedExpectedRelease[$id][$field]
        }
        $expectedFrameworks = Get-ObjectMap $normalizedExpectedRelease[$id].frameworks 'tfm'
        $actualFrameworks = Get-ObjectMap $normalizedActualRelease[$id].frameworks 'tfm'
        foreach ($tfm in $expectedFrameworks.Keys) {
            $actualFrameworks[$tfm].assemblySha256 = $expectedFrameworks[$tfm].assemblySha256
        }
    }
    if ((Get-CanonicalJson $normalizedExpected) -ne (Get-CanonicalJson $normalizedActual)) { throw 'Provenance/SBOM baseline drift.' }

    $normalizedExpectedSymbols = Get-CanonicalValue $expectedSymbols
    $normalizedActualSymbols = Get-CanonicalValue $symbols
    $normalizedActualSymbols.repositoryCommit = $normalizedExpectedSymbols.repositoryCommit
    # The installed SDK and MSBuild patch versions are host observations. Keep the
    # pinned global.json contract strict while allowing local and hosted runners in
    # the same feature band to retain their actual host identities in evidence.
    foreach ($field in @('actualSdkVersion', 'msbuildVersion')) {
        $normalizedActualSymbols.sdkIdentity[$field] = $normalizedExpectedSymbols.sdkIdentity[$field]
    }
    $expectedSymbolMap = @{}
    foreach ($assembly in @($normalizedExpectedSymbols.assemblies)) { $expectedSymbolMap["$($assembly.packageId)|$($assembly.tfm)"] = $assembly }
    $actualSymbolMap = @{}
    foreach ($assembly in @($normalizedActualSymbols.assemblies)) { $actualSymbolMap["$($assembly.packageId)|$($assembly.tfm)"] = $assembly }
    Assert-SameKeySet @($expectedSymbolMap.Keys) @($actualSymbolMap.Keys) 'PDB/SourceLink assembly set'
    $commitBoundSymbolFields = @('assemblySha256', 'packageAssemblySha256', 'mvid', 'codeViewGuid', 'pdbBytes', 'pdbSha256', 'portablePdbId', 'documentSha256', 'sourceLinkCommit', 'sourceLinkSha256')
    foreach ($key in $expectedSymbolMap.Keys) {
        foreach ($field in $commitBoundSymbolFields) {
            $actualSymbolMap[$key].evidence[$field] = $expectedSymbolMap[$key].evidence[$field]
        }
        # Roslyn's servicing build and its host runtime can differ across Windows build agents
        # while the pinned SDK feature band, language settings, sequence points, documents, and
        # SourceLink contract remain the same. Preserve these fields in generated evidence, but
        # do not make a retained local baseline reject the GitHub-hosted build for that reason.
        foreach ($field in @('compilerVersion', 'runtimeVersion', 'compilationOptionsSha256')) {
            $actualSymbolMap[$key].evidence[$field] = $expectedSymbolMap[$key].evidence[$field]
        }
    }
    $expectedSymbolPackages = Get-ObjectMap $normalizedExpectedSymbols.symbolPackages 'id'
    $actualSymbolPackages = Get-ObjectMap $normalizedActualSymbols.symbolPackages 'id'
    Assert-SameKeySet @($expectedSymbolPackages.Keys) @($actualSymbolPackages.Keys) 'Symbol package identity set'
    foreach ($id in $expectedSymbolPackages.Keys) {
        $actualSymbolPackages[$id].rawPackageBytes = $expectedSymbolPackages[$id].rawPackageBytes
        $actualSymbolPackages[$id].rawPackageSha256 = $expectedSymbolPackages[$id].rawPackageSha256
    }
    if ((Get-CanonicalJson $normalizedExpectedSymbols) -ne (Get-CanonicalJson $normalizedActualSymbols)) {
        # Keep the CI failure actionable without dumping the complete evidence document.
        $symbolDiffs = [Collections.Generic.List[string]]::new()
        foreach ($property in @('schemaVersion', 'configuration', 'deterministicSetting', 'symbolPackagePolicy', 'rawSnupkgReproducibility', 'assemblySymbolSemanticDefinition', 'rawNupkgDefinition', 'blockers', 'summary')) {
            if ((Get-CanonicalJson $normalizedExpectedSymbols.$property) -ne (Get-CanonicalJson $normalizedActualSymbols.$property)) {
                $symbolDiffs.Add("root.$property")
            }
        }
        if ((Get-CanonicalJson $normalizedExpectedSymbols.sdkIdentity) -ne (Get-CanonicalJson $normalizedActualSymbols.sdkIdentity)) {
            $symbolDiffs.Add('root.sdkIdentity')
            foreach ($property in @('globalJsonVersion', 'globalJsonRollForward', 'globalJsonAllowPrerelease', 'actualSdkVersion', 'msbuildVersion')) {
                if ((Get-CanonicalJson $normalizedExpectedSymbols.sdkIdentity.$property) -ne (Get-CanonicalJson $normalizedActualSymbols.sdkIdentity.$property)) {
                    $symbolDiffs.Add("root.sdkIdentity.$property")
                }
            }
        }
        foreach ($key in $expectedSymbolMap.Keys) {
            $expectedAssembly = $expectedSymbolMap[$key]
            $actualAssembly = $actualSymbolMap[$key]
            foreach ($property in @('packageId', 'tfm')) {
                if ([string]$expectedAssembly.$property -ne [string]$actualAssembly.$property) { $symbolDiffs.Add("assemblies[$key].$property") }
            }
            foreach ($property in @('assemblyBytes', 'packageBuildAssemblyMatch', 'mvid', 'deterministicReproducibleMarker', 'debugType', 'codeViewAge', 'codeViewPathMode', 'pdbBytes', 'documentCount', 'documentPathMode', 'sequencePointSha256', 'sourceLinkStatus', 'embeddedSourceCount', 'compilerVersion', 'languageVersion', 'optimization', 'nullable', 'runtimeVersion', 'compilationOptionsSha256')) {
                if ((Get-CanonicalJson $expectedAssembly.evidence.$property) -ne (Get-CanonicalJson $actualAssembly.evidence.$property)) {
                    $symbolDiffs.Add("assemblies[$key].evidence.$property")
                }
            }
        }
        foreach ($id in $expectedSymbolPackages.Keys) {
            foreach ($property in @('id', 'version', 'packageSha256', 'packageBytes', 'packageBuildMatch')) {
                if ((Get-CanonicalJson $expectedSymbolPackages[$id].$property) -ne (Get-CanonicalJson $actualSymbolPackages[$id].$property)) {
                    $symbolDiffs.Add("symbolPackages[$id].$property")
                }
            }
        }
        $diffText = (@($symbolDiffs | Sort-Object -Unique | Select-Object -First 40) -join ', ')
        throw "PDB/SourceLink baseline drift: $diffText"
    }

    $normalizedExpectedApi = Get-CanonicalValue $expectedApi
    $normalizedActualApi = Get-CanonicalValue $api
    $normalizedActualApi.repositoryCommit = $normalizedExpectedApi.repositoryCommit
    $expectedApiPackages = Get-ObjectMap $normalizedExpectedApi.packages 'packageId'
    $actualApiPackages = Get-ObjectMap $normalizedActualApi.packages 'packageId'
    foreach ($packageId in $expectedApiPackages.Keys) {
        $expectedFrameworks = Get-ObjectMap $expectedApiPackages[$packageId].frameworks 'tfm'
        $actualFrameworks = Get-ObjectMap $actualApiPackages[$packageId].frameworks 'tfm'
        foreach ($tfm in $expectedFrameworks.Keys) { $actualFrameworks[$tfm].attributeMetadataSha256 = $expectedFrameworks[$tfm].attributeMetadataSha256 }
    }
    if ((Get-CanonicalJson $normalizedExpectedApi) -ne (Get-CanonicalJson $normalizedActualApi)) { throw 'Public API baseline drift.' }

    Write-Output "DEPLOYSHARP_RELEASE_EVIDENCE_GATE_OK packages=$($releasePackages.Count) tfms=$($symbolAssemblies.Count) dependencies=$($managedDependencies.Count) native=$($consumerOwnedNativeRuntimes.Count) licenses-spdx=$($provenance.summary.verifiedSpdxManagedDependencies) license-blockers=$($provenance.summary.managedDependencyLicenseBlockers) sourcelink=$sourceLinkValidCount/$($symbolAssemblies.Count) portable-pdb=$portablePdbCount/$($symbolAssemblies.Count) api=$apiContractCount raw-nupkg-identical=$rawMatches/$($releasePackages.Count) release-eligible=$($provenance.summary.releaseEligible.ToString().ToLowerInvariant())"
}

if ($RequireReleaseEligible -and -not $provenance.summary.releaseEligible) {
    throw "Release evidence remains blocked: $($provenance.releaseBlockers -join ',')."
}
}

$managedDependencies = @($componentAccumulator.GetEnumerator() | Sort-Object Name | ForEach-Object {
    $value = $_.Value
    Get-PackageCacheRecord -Id $value.id -Version $value.version -ContentHash $value.contentHash -UsedBy @($value.usedBy) -Ownership 'managed-dependency'
})

[xml]$central = Get-Content -LiteralPath (Join-Path $repository 'Directory.Packages.props') -Raw
$centralVersions = @{}
foreach ($node in @($central.SelectNodes("//*[local-name()='PackageVersion']"))) { $centralVersions[[string]$node.Include] = [string]$node.Version }
$nativeRuntimeIds = @('JYPPX.OpenCV.runtime.win-x64', 'LLamaSharp.Backend.Cpu', 'Microsoft.ML.OnnxRuntime', 'OpenVINO.runtime.win')
$consumerOwnedNativeRuntimes = @($nativeRuntimeIds | Sort-Object | ForEach-Object {
    $id = $_
    if (-not $centralVersions.ContainsKey($id)) { throw "Central native runtime version is missing: $id." }
    Get-PackageCacheRecord -Id $id -Version $centralVersions[$id] -ContentHash $null -UsedBy @('clean-consumer-opt-in') -Ownership 'consumer-owned-native-runtime'
})

$releasePackages = [Collections.Generic.List[object]]::new()
$symbolAssemblies = [Collections.Generic.List[object]]::new()
$apiPackages = [Collections.Generic.List[object]]::new()
$signedCount = 0
$sourceLinkValidCount = 0
$portablePdbCount = 0
$absolutePdbPathCount = 0
$packageBuildDriftCount = 0
$apiContractCount = 0

foreach ($definition in $definitions) {
    $packagePath = Join-Path $packageRoot "$($definition.packageId).$($releaseBaseline.packageVersion).nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "Release package is missing: $packagePath" }
    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entries = Get-PackageEntries $archive
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspecEntries.Count -ne 1) { throw "$($definition.packageId) must contain one nuspec." }
        [xml]$nuspec = Get-EntryText $nuspecEntries[0]
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
        $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
        if ([string]$licenseNode.type -ne 'expression' -or [string]$licenseNode.InnerText -ne $releaseBaseline.licenseExpression) { throw "DeploySharp package license drift: $($definition.packageId)." }
        if ([string]$repositoryNode.url -ne $releaseBaseline.repositoryUrl -or [string]$repositoryNode.type -ne $releaseBaseline.repositoryType -or [string]$repositoryNode.commit -ne $head) {
            throw "Repository commit drift: $($definition.packageId)."
        }
        $dependencyGroups = Get-DependencyGroupMap $nuspec
        $frameworkEvidence = [Collections.Generic.List[object]]::new()
        $packageMembers = $null
        $packageContractSha = $null
        $contractConsistency = 'identical-across-supported-tfms'
        $apiFrameworks = [Collections.Generic.List[object]]::new()

        foreach ($tfm in @($definition.targetFrameworks)) {
            $dllEntryName = "lib/$tfm/$($definition.assemblyName).dll"
            $xmlEntryName = "lib/$tfm/$($definition.assemblyName).xml"
            if (-not $entries.ContainsKey($dllEntryName) -or -not $entries.ContainsKey($xmlEntryName)) { throw "Package contract payload is missing: $($definition.packageId)/$tfm." }
            $assemblyBytes = Get-EntryBytes $entries[$dllEntryName]
            $assemblySha256 = Get-Sha256Bytes $assemblyBytes
            $publicMetadata = Get-PublicMetadataEvidence $assemblyBytes
            [xml]$xmlDocument = Get-EntryText $entries[$xmlEntryName]
            $members = @($xmlDocument.doc.members.member | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
            $contractSha = Get-Sha256Text (($members -join "`n") + "`n")
            if ($null -eq $packageMembers) { $packageMembers = $members; $packageContractSha = $contractSha }
            elseif ($contractSha -ne $packageContractSha -or (Compare-Object $packageMembers $members)) {
                # Conditional compilation can intentionally expose different APIs
                # on legacy and modern TFMs. Keep every framework contract below
                # instead of treating a documented variant as a release failure.
                $contractConsistency = 'varies-by-target-framework'
            }
            $apiContractCount++

            $nuspecFramework = Convert-ToNuspecFramework $tfm
            if (-not $dependencyGroups.ContainsKey($nuspecFramework)) { throw "Nuspec dependency group is missing for $($definition.packageId)/$tfm." }
            $dependencies = @($dependencyGroups[$nuspecFramework])
            foreach ($dependency in $dependencies) {
                if (Test-NativeRuntimePackage $dependency.id) { throw "Nuspec contains consumer-owned native runtime '$($dependency.id)'." }
            }
            $frameworkEvidence.Add([ordered]@{
                tfm = $tfm
                assemblySha256 = $assemblySha256
                nuspecDependencies = $dependencies
                resolvedManagedPackages = @($projectState[$definition.packageId].packagesByTfm[$tfm])
                actualAssemblyReferences = @($publicMetadata.assemblyReferences)
            })
            $apiFrameworks.Add([ordered]@{
                tfm = $tfm
                memberCount = $members.Count
                contractSha256 = $contractSha
                visibleMetadataEntries = $publicMetadata.visibleMetadataEntries
                surfaceMetadataSha256 = $publicMetadata.surfaceMetadataSha256
                attributeEntries = $publicMetadata.attributeEntries
                attributeMetadataSha256 = $publicMetadata.attributeMetadataSha256
                nullableAttributeEntries = $publicMetadata.nullableAttributeEntries
                nullableMetadataSha256 = $publicMetadata.nullableMetadataSha256
                genericEntries = $publicMetadata.genericEntries
                genericMetadataSha256 = $publicMetadata.genericMetadataSha256
                assemblyReferences = @($publicMetadata.assemblyReferences)
                assemblyReferenceSha256 = $publicMetadata.assemblyReferenceSha256
            })

            $buildDll = Join-Path $projectState[$definition.packageId].projectDirectory "bin\$Configuration\$tfm\$($definition.assemblyName).dll"
            $buildPdb = [IO.Path]::ChangeExtension($buildDll, '.pdb')
            $symbol = Get-SymbolEvidence -DllPath $buildDll -PdbPath $buildPdb -ExpectedAssemblySha256 $assemblySha256
            if (-not $symbol.packageBuildAssemblyMatch) { $packageBuildDriftCount++ }
            if ($symbol.debugType -eq 'portable') { $portablePdbCount++ }
            if ($symbol.sourceLinkStatus -eq 'present-valid-head') { $sourceLinkValidCount++ }
            if ($symbol.documentPathMode -eq 'absolute') { $absolutePdbPathCount++ }
            $symbolAssemblies.Add([ordered]@{ packageId = $definition.packageId; tfm = $tfm; evidence = $symbol })
        }

        $signed = $entries.ContainsKey('.signature.p7s')
        if ($signed) { $signedCount++ }
        $releasePackages.Add([ordered]@{
            id = $definition.packageId
            version = $releaseBaseline.packageVersion
            licenseScope = 'deploysharp-package'
            licenseExpression = [string]$licenseNode.InnerText
            repositoryType = [string]$repositoryNode.type
            repositoryUrl = [string]$repositoryNode.url
            repositoryCommit = [string]$repositoryNode.commit
            rawPackageBytes = (Get-Item -LiteralPath $packagePath).Length
            rawPackageSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
            semanticPayloadSha256 = Get-SemanticPackageDigest $archive
            signed = $signed
            internalDependencyClosure = 'validated-for-all-supported-tfms'
            frameworks = @($frameworkEvidence)
        })
        $apiPackages.Add([ordered]@{
            packageId = $definition.packageId
            assemblyName = $definition.assemblyName
            contractConsistency = $contractConsistency
            contractSha256 = $packageContractSha
            members = @($packageMembers)
            frameworks = @($apiFrameworks)
        })
    }
    finally { $archive.Dispose() }
}

$releasePackageMap = @{}
foreach ($package in $releasePackages) { $releasePackageMap[$package.id] = $package }
foreach ($package in $releasePackages) {
    foreach ($framework in @($package.frameworks)) {
        $reachable = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $queue = [Collections.Generic.Queue[string]]::new()
        foreach ($dependency in @($framework.nuspecDependencies | Where-Object { $releasePackageMap.ContainsKey($_.id) })) { $queue.Enqueue($dependency.id) }
        while ($queue.Count -gt 0) {
            $dependencyId = $queue.Dequeue()
            if (-not $reachable.Add($dependencyId)) { continue }
            $dependencyPackage = $releasePackageMap[$dependencyId]
            $dependencyFramework = @($dependencyPackage.frameworks | Where-Object { $_.tfm -eq $framework.tfm })[0]
            if ($null -ne $dependencyFramework) {
                foreach ($child in @($dependencyFramework.nuspecDependencies | Where-Object { $releasePackageMap.ContainsKey($_.id) })) { $queue.Enqueue($child.id) }
            }
        }
        foreach ($reference in @($framework.actualAssemblyReferences)) {
            $referenceId = ([string]$reference -split '\|', 2)[0]
            if ($referenceId -like 'JYPPX.DeploySharp.*' -and $referenceId -ne $package.id -and -not $reachable.Contains($referenceId)) {
                throw "$($package.id)/$($framework.tfm) references internal assembly '$referenceId' outside its nuspec dependency closure."
            }
        }
    }
}

Complete-ReleaseEvidence
