[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,
    [Parameter(Mandatory = $true)]
    [string]$CertificatePasswordEnvironmentVariable,
    [Parameter(Mandatory = $true)]
    [string]$TimestampServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $PackageDirectory).Path
$certificate = (Resolve-Path -LiteralPath $CertificatePath).Path
$password = [Environment]::GetEnvironmentVariable($CertificatePasswordEnvironmentVariable)
if ([string]::IsNullOrWhiteSpace($password)) {
    throw "The package-signing password environment variable '$CertificatePasswordEnvironmentVariable' is not set."
}
if (-not ([Uri]$TimestampServer).Scheme.Equals('https', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package-signing timestamp server must use HTTPS.'
}

$packages = @(Get-ChildItem -LiteralPath $root -File -Filter '*.nupkg' | Sort-Object Name)
if ($packages.Count -eq 0) { throw "No .nupkg files found in $root." }
foreach ($package in $packages) {
    & dotnet nuget sign $package.FullName --certificate-path $certificate --certificate-password $password --timestamper $TimestampServer --overwrite
    if ($LASTEXITCODE -ne 0) { throw "NuGet signing failed: $($package.Name)." }
    Write-Output "DEPLOYSHARP_RELEASE_PACKAGE_SIGNED file=$($package.Name)"
}
