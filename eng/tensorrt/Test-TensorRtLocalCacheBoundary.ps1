[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$AssemblyPath,
    [string]$PublicApiPath,
    [string]$ReadmePath,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) { $AssemblyPath = Join-Path $repository 'src\DeploySharp.Backend.TensorRT\bin\Release\net8.0\JYPPX.DeploySharp.Backend.TensorRT.dll' }
if ([string]::IsNullOrWhiteSpace($PublicApiPath)) { $PublicApiPath = Join-Path $repository 'eng\pack\release-evidence\public-api.json' }
if ([string]::IsNullOrWhiteSpace($ReadmePath)) { $ReadmePath = Join-Path $repository 'README.md' }

$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$publicApi = (Resolve-Path -LiteralPath $PublicApiPath).Path
$readme = (Resolve-Path -LiteralPath $ReadmePath).Path
$package = (Resolve-Path -LiteralPath $PackagePath).Path

$legacyPrefixes = @(
    'TensorRtExternalCacheCoordination',
    'TensorRtExternalCacheLease',
    'TensorRtExternalCacheMaintenance',
    'TensorRtExternalCacheSnapshot',
    'TensorRtExternalCacheSnapshotDelta',
    'TensorRtExternalCacheSnapshotSignature',
    'TensorRtExternalCacheSnapshotBundle',
    'TensorRtExternalCacheTrustedDeployment',
    'TensorRtExternalCacheAuthorizedDeployment',
    'TensorRtExternalCacheApprovalPolicy'
)

function Assert-NoLegacyCacheTerms {
    param([string]$Name, [string]$Text)

    foreach ($prefix in $legacyPrefixes) {
        if ($Text.IndexOf($prefix, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Legacy TensorRT cache surface remains in ${Name}: $prefix"
        }
    }

    $contextPattern = '(?i)(TensorRtExternalCache|TensorRT cache)[A-Za-z0-9_.:+/ -]{0,96}(Snapshot|Delta|Signature|Bundle|TrustedDeployment|AuthorizedDeployment|ApprovalPolicy|Lease|Maintenance|WAL|Ledger|Receipt)'
    if ($Text -match $contextPattern) {
        throw "Legacy TensorRT cache terminology remains in ${Name}: $($Matches[0])"
    }
}

$assemblyBytes = [IO.File]::ReadAllBytes($assembly)
$assemblyText = [Text.Encoding]::UTF8.GetString($assemblyBytes) + "`n" + [Text.Encoding]::Unicode.GetString($assemblyBytes)
$publicApiText = [IO.File]::ReadAllText($publicApi)
$readmeText = [IO.File]::ReadAllText($readme)

Assert-NoLegacyCacheTerms 'assembly' $assemblyText
Assert-NoLegacyCacheTerms 'public API evidence' $publicApiText
Assert-NoLegacyCacheTerms 'README' $readmeText

foreach ($required in @('TensorRtLocalCacheOptions', 'TensorRtLocalSessionFactory')) {
    if ($publicApiText.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Required local TensorRT cache facade is absent from public API evidence: $required"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '(?i)\.(engine|plan|ptx|cubin)$') {
            throw "TensorRT generated cache payload was packaged: $($entry.FullName)"
        }

        $stream = $entry.Open()
        $memory = [IO.MemoryStream]::new()
        try {
            $stream.CopyTo($memory)
            $bytes = $memory.ToArray()
            $text = [Text.Encoding]::UTF8.GetString($bytes) + "`n" + [Text.Encoding]::Unicode.GetString($bytes)
            Assert-NoLegacyCacheTerms "NuGet entry $($entry.FullName)" $text
        }
        finally {
            $memory.Dispose()
            $stream.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Output 'DEPLOYSHARP_TENSORRT_LOCAL_CACHE_BOUNDARY_OK assembly=clean public-api=clean readme=clean nupkg=clean payloads=none'
