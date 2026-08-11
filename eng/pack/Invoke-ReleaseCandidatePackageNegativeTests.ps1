[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,
    [string]$GatePath = (Join-Path $PSScriptRoot 'Test-ReleaseCandidatePackages.ps1')
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
$gate = (Resolve-Path -LiteralPath $GatePath).Path
if (Test-Path -LiteralPath $WorkingDirectory) { throw "Negative-test working directory already exists: $WorkingDirectory" }
$work = [IO.Path]::GetFullPath($WorkingDirectory)
New-Item -ItemType Directory -Path $work | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression

function New-Scenario {
    param([string]$Name)
    $path = Join-Path $work $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    Copy-Item -Path (Join-Path $source '*.nupkg') -Destination $path
    return $path
}

function Add-ZipEntry {
    param([string]$PackagePath, [string]$EntryName, [string]$Contents)
    $archive = [IO.Compression.ZipFile]::Open($PackagePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.CreateEntry($EntryName)
        $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($Contents) }
        finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Update-Nuspec {
    param([string]$PackagePath, [scriptblock]$Mutation)
    $archive = [IO.Compression.ZipFile]::Open($PackagePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })[0]
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$document = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        & $Mutation $document
        $name = $entry.FullName
        $entry.Delete()
        $replacement = $archive.CreateEntry($name)
        $writer = [IO.StreamWriter]::new($replacement.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($document.OuterXml) }
        finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Assert-GateRejects {
    param([string]$Name, [string]$Directory, [string]$ExpectedPattern)
    $message = $null
    try {
        $output = & $gate -PackageDirectory $Directory 2>&1 | Out-String
        throw "Gate accepted negative scenario '$Name'. Output: $output"
    }
    catch {
        $message = $_.Exception.Message
        if ($message -like "Gate accepted negative scenario*") { throw }
    }
    if ($message -notmatch $ExpectedPattern) { throw "Scenario '$Name' failed for the wrong reason: $message" }
    Write-Output "DEPLOYSHARP_RELEASE_PACKAGE_GATE_NEGATIVE_OK scenario=$Name"
}

$native = New-Scenario 'native-leak'
Add-ZipEntry (Join-Path $native 'JYPPX.DeploySharp.Backend.LlamaSharp.2.0.0-alpha.1.nupkg') 'runtimes/win-x64/native/llama.dll' 'negative-native-probe'
Assert-GateRejects 'native-leak' $native 'Unexpected NuGet payload|Native payload leaked'

$tfm = New-Scenario 'wrong-tfm'
$corePackage = Join-Path $tfm 'JYPPX.DeploySharp.Core.2.0.0-alpha.1.nupkg'
$sourceArchive = [IO.Compression.ZipFile]::OpenRead($corePackage)
try {
    $sourceEntry = @($sourceArchive.Entries | Where-Object { $_.FullName -eq 'lib/net10.0/JYPPX.DeploySharp.Core.dll' })[0]
    $memory = [IO.MemoryStream]::new()
    try { $entryStream = $sourceEntry.Open(); try { $entryStream.CopyTo($memory) } finally { $entryStream.Dispose() }; $bytes = $memory.ToArray() }
    finally { $memory.Dispose() }
}
finally { $sourceArchive.Dispose() }
$updateArchive = [IO.Compression.ZipFile]::Open($corePackage, [IO.Compression.ZipArchiveMode]::Update)
try { $entry = $updateArchive.CreateEntry('lib/net11.0/JYPPX.DeploySharp.Core.dll'); $stream = $entry.Open(); try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() } }
finally { $updateArchive.Dispose() }
Assert-GateRejects 'wrong-tfm' $tfm 'package TFMs mismatch'

$dependency = New-Scenario 'dependency-version'
Update-Nuspec (Join-Path $dependency 'JYPPX.DeploySharp.ModelPack.Json.2.0.0-alpha.1.nupkg') {
    param($document)
    $node = @($document.SelectNodes("//*[local-name()='dependency' and @id='JYPPX.DeploySharp.Core']"))[0]
    $node.SetAttribute('version', '9.9.9')
}
Assert-GateRejects 'dependency-version' $dependency 'generated/package dependency graph mismatch'

$metadata = New-Scenario 'missing-metadata'
Update-Nuspec (Join-Path $metadata 'JYPPX.DeploySharp.ModelFactory.2.0.0-alpha.1.nupkg') {
    param($document)
    $node = @($document.SelectNodes("//*[local-name()='repository']"))[0]
    [void]$node.ParentNode.RemoveChild($node)
}
Assert-GateRejects 'missing-metadata' $metadata 'repository metadata'

$payload = New-Scenario 'unexpected-payload'
Add-ZipEntry (Join-Path $payload 'JYPPX.DeploySharp.Core.2.0.0-alpha.1.nupkg') 'tools/probe.txt' 'unexpected-payload-probe'
Assert-GateRejects 'unexpected-payload' $payload 'Unexpected NuGet payload'

Write-Output 'DEPLOYSHARP_RELEASE_PACKAGE_GATE_NEGATIVE_SUITE_OK scenarios=5'
