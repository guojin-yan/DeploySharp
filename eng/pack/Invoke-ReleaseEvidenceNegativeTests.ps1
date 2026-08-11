[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,
    [string]$GatePath = (Join-Path $PSScriptRoot 'Test-ReleaseEvidence.ps1')
)

$ErrorActionPreference = 'Stop'

$packages = (Resolve-Path -LiteralPath $PackageDirectory).Path
$evidence = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$gate = (Resolve-Path -LiteralPath $GatePath).Path
$work = [IO.Path]::GetFullPath($WorkingDirectory)
if (Test-Path -LiteralPath $work) { throw "Negative-test working directory already exists: $work" }
New-Item -ItemType Directory -Path $work | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression

function New-PackageScenario {
    param([string]$Name)
    $path = Join-Path $work $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    Copy-Item -Path (Join-Path $packages '*.nupkg') -Destination $path
    return $path
}

function New-EvidenceScenario {
    param([string]$Name)
    $path = Join-Path $work $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    Copy-Item -Path (Join-Path $evidence '*.json') -Destination $path
    return $path
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

function Update-EvidenceJson {
    param([string]$Path, [scriptblock]$Mutation)
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    & $Mutation $document
    $json = $document | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json + "`n", [Text.UTF8Encoding]::new($false))
}

function Assert-GateRejects {
    param(
        [string]$Name,
        [string]$ScenarioPackageDirectory,
        [string]$ScenarioEvidenceDirectory,
        [string]$ExpectedPattern
    )
    $message = $null
    try {
        $output = & $gate -PackageDirectory $ScenarioPackageDirectory -EvidenceDirectory $ScenarioEvidenceDirectory 2>&1 | Out-String
        throw "Gate accepted negative scenario '$Name'. Output: $output"
    }
    catch {
        $message = $_.Exception.Message
        if ($message -like 'Gate accepted negative scenario*') { throw }
    }
    if ($message -notmatch $ExpectedPattern) { throw "Scenario '$Name' failed for the wrong reason: $message" }
    Write-Output "DEPLOYSHARP_RELEASE_EVIDENCE_NEGATIVE_OK scenario=$Name"
}

$licensePackages = New-PackageScenario 'license-missing'
Update-Nuspec (Join-Path $licensePackages 'JYPPX.DeploySharp.Core.2.0.0-alpha.1.nupkg') {
    param($document)
    $node = @($document.SelectNodes("//*[local-name()='license']"))[0]
    [void]$node.ParentNode.RemoveChild($node)
}
Assert-GateRejects 'license-missing' $licensePackages $evidence 'DeploySharp package license drift'

$hashEvidence = New-EvidenceScenario 'dependency-content-hash'
Update-EvidenceJson (Join-Path $hashEvidence 'package-provenance-sbom.json') {
    param($document)
    $document.managedDependencies[0].resolvedContentHash = 'sha512-negative-content-hash'
}
Assert-GateRejects 'dependency-content-hash' $packages $hashEvidence 'Dependency content hash drift'

$repositoryPackages = New-PackageScenario 'repository-commit'
Update-Nuspec (Join-Path $repositoryPackages 'JYPPX.DeploySharp.ModelFactory.2.0.0-alpha.1.nupkg') {
    param($document)
    $node = @($document.SelectNodes("//*[local-name()='repository']"))[0]
    $node.SetAttribute('commit', ('0' * 40))
}
Assert-GateRejects 'repository-commit' $repositoryPackages $evidence 'Repository commit drift'

$symbolsEvidence = New-EvidenceScenario 'sourcelink-drift'
Update-EvidenceJson (Join-Path $symbolsEvidence 'release-symbols.json') {
    param($document)
    $document.assemblies[0].evidence.sourceLinkStatus = 'missing'
    $document.assemblies[0].evidence.sourceLinkCommit = $null
}
Assert-GateRejects 'sourcelink-drift' $packages $symbolsEvidence 'PDB/SourceLink baseline drift'

$apiEvidence = New-EvidenceScenario 'api-signature'
Update-EvidenceJson (Join-Path $apiEvidence 'public-api.json') {
    param($document)
    $document.packages[0].frameworks[0].surfaceMetadataSha256 = ('0' * 64)
}
Assert-GateRejects 'api-signature' $packages $apiEvidence 'Public API baseline drift'

$omissionEvidence = New-EvidenceScenario 'sbom-omission'
Update-EvidenceJson (Join-Path $omissionEvidence 'package-provenance-sbom.json') {
    param($document)
    $document.managedDependencies = @($document.managedDependencies | Select-Object -Skip 1)
}
Assert-GateRejects 'sbom-omission' $packages $omissionEvidence 'SBOM managed dependency set.*drift'

$nativeEvidence = New-EvidenceScenario 'native-ownership'
Update-EvidenceJson (Join-Path $nativeEvidence 'package-provenance-sbom.json') {
    param($document)
    $document.consumerOwnedNativeRuntimes[0].ownership = 'deploysharp-package'
}
Assert-GateRejects 'native-ownership' $packages $nativeEvidence 'Native ownership drift'

Write-Output 'DEPLOYSHARP_RELEASE_EVIDENCE_NEGATIVE_SUITE_OK scenarios=7'
