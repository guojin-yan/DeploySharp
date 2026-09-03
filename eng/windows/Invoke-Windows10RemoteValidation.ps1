[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ComputerName,
    [Parameter(Mandatory = $true)][string]$UserName,
    [string]$Port = '22',
    [string]$RemoteRoot = 'C:/DeploySharpTest',
    [string]$IdentityFile,
    [ValidateSet('preflight', 'managed', 'native', 'full')][string]$Mode = 'managed',
    [switch]$IncludeExternal,
    [string]$OutputDirectory = (Join-Path (Join-Path $PSScriptRoot '..\..') 'artifacts\windows-validation\remote')
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path.TrimEnd('\', '/')
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) { throw 'OpenSSH client ssh.exe is required.' }
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) { throw 'OpenSSH client scp.exe is required.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$archive = Join-Path $env:TEMP "deploysharp-$stamp.zip"
$staging = Join-Path $env:TEMP "deploysharp-source-$stamp"
$remoteArchive = "$RemoteRoot/deploysharp-$stamp.zip"
$target = "$UserName@$ComputerName"
$sshArgs = @('-p', $Port)
$scpArgs = @('-P', $Port)
if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
    $sshArgs += @('-i', $IdentityFile)
    $scpArgs += @('-i', $IdentityFile)
}

function Invoke-RemotePowerShell {
    param([string]$Script)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Script))
    & ssh @sshArgs $target "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded"
    if ($LASTEXITCODE -ne 0) { throw 'Remote PowerShell command failed.' }
}

try {
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    $sourceFiles = @(& git -C $repository ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0 -or $sourceFiles.Count -eq 0) { throw 'Could not enumerate the current source worktree.' }
    foreach ($relativePath in $sourceFiles) {
        $sourcePath = Join-Path $repository $relativePath
        $destinationPath = Join-Path $staging $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
    }
    $commit = (& git -C $repository rev-parse HEAD 2>$null | Out-String).Trim()
    Set-Content -LiteralPath (Join-Path $staging '.deploysharp-source-commit') -Value $commit -Encoding ascii
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal
    Invoke-RemotePowerShell "New-Item -ItemType Directory -Force -Path '$RemoteRoot' | Out-Null"
    & scp @scpArgs $archive ('{0}:{1}' -f $target, $remoteArchive)
    if ($LASTEXITCODE -ne 0) { throw 'Source archive transfer failed.' }
    Invoke-RemotePowerShell "Expand-Archive -LiteralPath '$remoteArchive' -DestinationPath '$RemoteRoot/source' -Force"
    $run = "Set-Location '$RemoteRoot/source'" + [Environment]::NewLine
    $run += "& pwsh -NoProfile -ExecutionPolicy Bypass -File './eng/windows/Invoke-WindowsValidation.ps1' -Mode '$Mode' -IncludeExternal:$($IncludeExternal.IsPresent) -ReportPath '$RemoteRoot/validation.json'" + [Environment]::NewLine
    $run += '$validationExit = $LASTEXITCODE' + [Environment]::NewLine
    $run += "if (Test-Path '$RemoteRoot/validation.logs') { Compress-Archive -Path '$RemoteRoot/validation.logs/*' -DestinationPath '$RemoteRoot/validation-logs.zip' -Force }" + [Environment]::NewLine
    $run += "if (Test-Path '$RemoteRoot/source/artifacts/windows-validation') { Remove-Item -LiteralPath '$RemoteRoot/source/artifacts/windows-validation' -Recurse -Force -ErrorAction SilentlyContinue }" + [Environment]::NewLine
    $run += 'exit $validationExit'
    $runEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($run))
    & ssh @sshArgs $target "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand $runEncoded"
    $remoteExit = $LASTEXITCODE
    $localReport = Join-Path $OutputDirectory "$stamp-$Mode.json"
    $localLogs = Join-Path $OutputDirectory "$stamp-$Mode.logs.zip"
    & scp @scpArgs ('{0}:{1}' -f $target, "$RemoteRoot/validation.json") $localReport
    if ($LASTEXITCODE -ne 0) { throw 'Validation report download failed.' }
    & scp @scpArgs ('{0}:{1}' -f $target, "$RemoteRoot/validation-logs.zip") $localLogs
    if ($LASTEXITCODE -ne 0) { throw 'Validation log download failed.' }
    $result = if ($remoteExit -eq 0) { 'OK' } else { 'FAILED' }
    Write-Output ("DEPLOYSHARP_WINDOWS_REMOTE_VALIDATION_{0} computer={1} report={2}" -f $result, $ComputerName, $localReport)
    exit $remoteExit
}
finally {
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
