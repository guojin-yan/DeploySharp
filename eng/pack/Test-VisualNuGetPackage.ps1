[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$frameworks = @(
    'net46', 'net461', 'net462', 'net47', 'net471', 'net472', 'net48', 'net481',
    'netstandard2.0', 'netcoreapp3.1', 'net5.0', 'net6.0', 'net7.0', 'net8.0', 'net9.0', 'net10.0'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-EntrySha256 {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)
    $stream = $Entry.Open()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-FileSha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
$entries = @{}
    foreach ($entry in $archive.Entries) {
        if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate NuGet entry: $($entry.FullName)" }
        $entries.Add($entry.FullName, $entry)
    }

    $required = @('JYPPX.DeploySharp.Visual.nuspec', 'README.md', 'logo.jpg')
    foreach ($framework in $frameworks) {
        $required += "lib/$framework/JYPPX.DeploySharp.Visual.dll"
        $required += "lib/$framework/JYPPX.DeploySharp.Visual.xml"
    }
    foreach ($name in $required) {
        if (-not $entries.ContainsKey($name)) { throw "Required NuGet payload is missing: $name" }
    }

    # A strict payload allowlist prevents model, image, backend, and native files from entering Visual.
    foreach ($name in $entries.Keys) {
        $allowed = (
            ($required -contains $name) -or
            ($name -eq '_rels/.rels') -or
            ($name -eq '[Content_Types].xml') -or
            ($name -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$') -or
            ($name -match '^package/services/digital-signature/[^/]+\.p7s$')
        )
        if (-not $allowed) { throw "Unexpected NuGet payload: $name" }
    }

    if ((Get-EntrySha256 $entries['README.md']) -ne (Get-FileSha256 (Join-Path $repositoryRoot 'README.md'))) {
        throw 'The package README is not the repository English README.'
    }
    if ((Get-EntrySha256 $entries['logo.jpg']) -ne (Get-FileSha256 (Join-Path $repositoryRoot 'nuget\logo.jpg'))) {
        throw 'The package logo is not nuget/logo.jpg.'
    }

    $reader = New-Object System.IO.StreamReader($entries['JYPPX.DeploySharp.Visual.nuspec'].Open())
    $nuspecText = $reader.ReadToEnd()
    $reader.Dispose()
    [xml]$nuspec = $nuspecText
    $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($null -eq $metadata) { throw 'NuGet metadata is missing.' }
    if ($metadata.SelectSingleNode("*[local-name()='id']").InnerText -ne 'JYPPX.DeploySharp.Visual') { throw 'Unexpected NuGet package ID.' }
    if ($metadata.SelectSingleNode("*[local-name()='version']").InnerText -ne '2.0.0-alpha.1') { throw 'Unexpected NuGet package version.' }
    if ($metadata.SelectSingleNode("*[local-name()='icon']").InnerText -ne 'logo.jpg') { throw 'NuGet icon metadata is invalid.' }
    if ($metadata.SelectSingleNode("*[local-name()='readme']").InnerText -ne 'README.md') { throw 'NuGet README metadata is invalid.' }
    $dependencies = @($nuspec.SelectNodes("//*[local-name()='dependency']"))
    $coreDependencies = @($dependencies | Where-Object { $_.id -eq 'JYPPX.DeploySharp.Core' })
    $tokenizerDependencies = @($dependencies | Where-Object { $_.id -eq 'Microsoft.ML.Tokenizers' })
    if ($coreDependencies.Count -ne $frameworks.Count) { throw "Expected one Core dependency for each of $($frameworks.Count) TFM groups; found $($coreDependencies.Count)." }
    if ($tokenizerDependencies.Count -ne 3) { throw "Expected Microsoft.ML.Tokenizers only for net8.0, net9.0, and net10.0; found $($tokenizerDependencies.Count) dependencies." }
    foreach ($dependency in $coreDependencies) {
        if ($dependency.version -ne '2.0.0-alpha.1') { throw "Unexpected Core dependency version: $($dependency.version)" }
    }
    foreach ($dependency in $tokenizerDependencies) {
        if ($dependency.version -ne '2.0.0') { throw "Unexpected tokenizer dependency version: $($dependency.version)" }
        $group = $dependency.ParentNode
        if (@('net8.0', 'net9.0', 'net10.0') -notcontains $group.targetFramework) { throw "Tokenizer dependency is present on unsupported TFM: $($group.targetFramework)" }
    }
    if ($dependencies.Count -ne ($coreDependencies.Count + $tokenizerDependencies.Count)) {
        throw 'Visual package contains an unexpected dependency.'
    }

Write-Host "DEPLOYSHARP_VISUAL_PACKAGE_AUDIT_OK tfms=$($frameworks.Count) entries=$($entries.Count)"
$archive.Dispose()
