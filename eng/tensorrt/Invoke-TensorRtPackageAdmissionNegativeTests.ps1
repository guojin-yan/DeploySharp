[CmdletBinding()]
param(
    [string]$PackageRoot,
    [string]$SourceRepository = $env:DEPLOYSHARP_TENSORRT_SOURCE_REPOSITORY,
    [string]$EvidencePath = (Join-Path $PSScriptRoot 'evidence\tensorrt-4.0.0-admission.blocked.json'),
    [string]$GatePath = (Join-Path $PSScriptRoot 'Test-TensorRtPackageAdmission.ps1')
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $nugetRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { Join-Path $env:USERPROFILE '.nuget\packages' } else { $env:NUGET_PACKAGES }
    $PackageRoot = Join-Path $nugetRoot 'jyppx.tensorrt.csharp.api\4.0.0'
}
if ([string]::IsNullOrWhiteSpace($SourceRepository)) { $SourceRepository = 'E:\GitSpace\TensorRT-CSharp-API-4.0\TensorRtSharp4.0' }

$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$EvidencePath = [IO.Path]::GetFullPath($EvidencePath)
$GatePath = [IO.Path]::GetFullPath($GatePath)
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "deploysharp-tensorrt-admission-$([Guid]::NewGuid().ToString('N'))"

function Get-Sha512Base64 {
    param([string]$Path)
    $algorithm = [Security.Cryptography.SHA512]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [Convert]::ToBase64String($algorithm.ComputeHash($stream)) }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

function Copy-AdmissionCase {
    param([string]$Name)
    $caseRoot = Join-Path $temporaryRoot $Name
    $packageCopy = Join-Path $caseRoot 'package'
    New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null
    Copy-Item -LiteralPath $PackageRoot -Destination $packageCopy -Recurse -Force
    $evidenceCopy = Join-Path $caseRoot 'evidence.json'
    Copy-Item -LiteralPath $EvidencePath -Destination $evidenceCopy -Force
    return [pscustomobject]@{ Root = $caseRoot; Package = $packageCopy; Evidence = $evidenceCopy }
}

function Sync-PackageIdentity {
    param([pscustomobject]$Case)
    $nupkgPath = Join-Path $Case.Package 'jyppx.tensorrt.csharp.api.4.0.0.nupkg'
    $sha512 = Get-Sha512Base64 $nupkgPath
    $sha256 = (Get-FileHash -LiteralPath $nupkgPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$nupkgPath.sha512", $sha512, [Text.UTF8Encoding]::new($false))
    $metadataPath = Join-Path $Case.Package '.nupkg.metadata'
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $metadata.contentHash = $sha512
    [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $evidence = Get-Content -LiteralPath $Case.Evidence -Raw | ConvertFrom-Json
    $evidence.package.sha256 = $sha256
    $evidence.package.sha512Base64 = $sha512
    $evidence.package.contentHash = $sha512
    $evidence.package.bytes = (Get-Item -LiteralPath $nupkgPath).Length
    [IO.File]::WriteAllText($Case.Evidence, ($evidence | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
}

function Update-PackageNuspec {
    param([pscustomobject]$Case, [scriptblock]$Mutation)
    $nupkgPath = Join-Path $Case.Package 'jyppx.tensorrt.csharp.api.4.0.0.nupkg'
    $archive = [IO.Compression.ZipFile]::Open($nupkgPath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.GetEntry('JYPPX.TensorRT.CSharp.API.nuspec')
        if ($null -eq $entry) { throw 'TensorRT package nuspec is missing from the negative-test copy.' }
        $reader = [IO.StreamReader]::new($entry.Open(), [Text.Encoding]::UTF8, $true)
        try { [xml]$document = $reader.ReadToEnd() }
        finally { $reader.Dispose() }

        $metadata = $document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) { throw 'TensorRT package metadata is missing from the negative-test copy.' }
        & $Mutation $document $metadata

        $settings = [Xml.XmlWriterSettings]::new()
        $settings.Encoding = [Text.UTF8Encoding]::new($false)
        $settings.Indent = $true
        $settings.NewLineChars = "`r`n"
        $settings.NewLineHandling = [Xml.NewLineHandling]::Replace
        $memory = [IO.MemoryStream]::new()
        $writer = [Xml.XmlWriter]::Create($memory, $settings)
        try { $document.Save($writer) }
        finally { $writer.Dispose() }
        $nuspecText = [Text.Encoding]::UTF8.GetString($memory.ToArray())
        $memory.Dispose()

        $entry.Delete()
        $replacement = $archive.CreateEntry('JYPPX.TensorRT.CSharp.API.nuspec')
        $stream = $replacement.Open()
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($nuspecText)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally { $stream.Dispose() }
    }
    finally { $archive.Dispose() }

    [IO.File]::WriteAllText((Join-Path $Case.Package 'jyppx.tensorrt.csharp.api.nuspec'), $nuspecText, [Text.UTF8Encoding]::new($false))
    Sync-PackageIdentity $Case
}

function Invoke-ExpectedFailure {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        throw "Negative TensorRT admission scenario unexpectedly passed: $Name"
    }
    catch {
        if ($_.Exception.Message -like 'Negative TensorRT admission scenario unexpectedly passed:*') { throw }
        Write-Output "DEPLOYSHARP_TENSORRT_ADMISSION_NEGATIVE_OK scenario=$Name"
    }
}

if (-not (Test-Path -LiteralPath $GatePath -PathType Leaf)) { throw "TensorRT admission gate is missing: $GatePath" }
if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) { throw "TensorRT package cache is missing: $PackageRoot" }
if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) { throw "TensorRT evidence is missing: $EvidencePath" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

    Invoke-ExpectedFailure 'require-admitted' {
        & $GatePath -PackageRoot $PackageRoot -SourceRepository $SourceRepository -EvidencePath $EvidencePath -RequireAdmitted | Out-Null
    }

    $shaCase = Copy-AdmissionCase 'sha512-drift'
    $shaPath = Join-Path $shaCase.Package 'jyppx.tensorrt.csharp.api.4.0.0.nupkg.sha512'
    [IO.File]::WriteAllText($shaPath, 'invalid-sha512', [Text.UTF8Encoding]::new($false))
    Invoke-ExpectedFailure 'sha512-drift' {
        & $GatePath -PackageRoot $shaCase.Package -SourceRepository $SourceRepository -EvidencePath $shaCase.Evidence | Out-Null
    }

    $blockerCase = Copy-AdmissionCase 'blocker-baseline-drift'
    $blockerEvidence = Get-Content -LiteralPath $blockerCase.Evidence -Raw | ConvertFrom-Json
    $blockerEvidence.blockers = @($blockerEvidence.blockers | Select-Object -Skip 1)
    [IO.File]::WriteAllText($blockerCase.Evidence, ($blockerEvidence | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectedFailure 'blocker-baseline-drift' {
        & $GatePath -PackageRoot $blockerCase.Package -SourceRepository $SourceRepository -EvidencePath $blockerCase.Evidence | Out-Null
    }

    Invoke-ExpectedFailure 'source-repository-unavailable' {
        & $GatePath -PackageRoot $PackageRoot -SourceRepository (Join-Path $temporaryRoot 'missing-source') -EvidencePath $EvidencePath | Out-Null
    }

    $licenseCase = Copy-AdmissionCase 'license-metadata-mutation'
    Update-PackageNuspec $licenseCase {
        param([xml]$Document, [Xml.XmlElement]$Metadata)
        $license = $Metadata.SelectSingleNode("*[local-name()='license']")
        if ($null -eq $license) {
            $license = $Document.CreateElement('license', $Metadata.NamespaceURI)
            $dependencies = $Metadata.SelectSingleNode("*[local-name()='dependencies']")
            $Metadata.InsertBefore($license, $dependencies) | Out-Null
        }
        $license.SetAttribute('type', 'expression')
        $license.InnerText = 'GPL-3.0-only'
    }
    Invoke-ExpectedFailure 'license-metadata-mutation' {
        & $GatePath -PackageRoot $licenseCase.Package -SourceRepository $SourceRepository -EvidencePath $licenseCase.Evidence | Out-Null
    }

    $repositoryCase = Copy-AdmissionCase 'repository-metadata-mutation'
    Update-PackageNuspec $repositoryCase {
        param([xml]$Document, [Xml.XmlElement]$Metadata)
        $repository = $Metadata.SelectSingleNode("*[local-name()='repository']")
        if ($null -eq $repository) { throw 'TensorRT package repository metadata is missing from the negative-test copy.' }
        $repository.SetAttribute('url', 'https://example.invalid/unapproved-tensorrt-source')
    }
    Invoke-ExpectedFailure 'repository-metadata-mutation' {
        & $GatePath -PackageRoot $repositoryCase.Package -SourceRepository $SourceRepository -EvidencePath $repositoryCase.Evidence | Out-Null
    }

    $nativeCase = Copy-AdmissionCase 'native-payload-injection'
    $nativeNupkg = Join-Path $nativeCase.Package 'jyppx.tensorrt.csharp.api.4.0.0.nupkg'
    $nativeArchive = [IO.Compression.ZipFile]::Open($nativeNupkg, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $nativeArchive.CreateEntry('runtimes/win-x64/native/nvinfer_10.dll')
        $stream = $entry.Open()
        try { $stream.WriteByte(0x54) }
        finally { $stream.Dispose() }
    }
    finally { $nativeArchive.Dispose() }
    Sync-PackageIdentity $nativeCase
    Invoke-ExpectedFailure 'native-payload-injection' {
        & $GatePath -PackageRoot $nativeCase.Package -SourceRepository $SourceRepository -EvidencePath $nativeCase.Evidence | Out-Null
    }

    $apiCase = Copy-AdmissionCase 'managed-api-removal'
    $apiNupkg = Join-Path $apiCase.Package 'jyppx.tensorrt.csharp.api.4.0.0.nupkg'
    $apiArchive = [IO.Compression.ZipFile]::Open($apiNupkg, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $apiEntry = $apiArchive.GetEntry('lib/net8.0/JYPPX.TensorRtSharp.xml')
        $reader = [IO.StreamReader]::new($apiEntry.Open(), [Text.Encoding]::UTF8, $true)
        try { $apiText = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $apiEntry.Delete()
        $apiText = $apiText.Replace('M:JYPPX.TensorRtSharp.TensorRtEngine.CreateExecutionContext', 'M:JYPPX.TensorRtSharp.TensorRtEngine.RemovedForNegativeTest')
        $replacement = $apiArchive.CreateEntry('lib/net8.0/JYPPX.TensorRtSharp.xml')
        $writer = [IO.StreamWriter]::new($replacement.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($apiText) }
        finally { $writer.Dispose() }
    }
    finally { $apiArchive.Dispose() }
    Sync-PackageIdentity $apiCase
    Invoke-ExpectedFailure 'managed-api-removal' {
        & $GatePath -PackageRoot $apiCase.Package -SourceRepository $SourceRepository -EvidencePath $apiCase.Evidence | Out-Null
    }

    Write-Output 'DEPLOYSHARP_TENSORRT_ADMISSION_NEGATIVE_SUITE_OK scenarios=8'
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolvedTemporaryRoot.StartsWith($resolvedSystemTemp, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTemporaryRoot)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}
