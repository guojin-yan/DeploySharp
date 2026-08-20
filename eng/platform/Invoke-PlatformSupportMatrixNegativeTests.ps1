[CmdletBinding()]
param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot 'platform-support.json'),
    [string]$GatePath = (Join-Path $PSScriptRoot 'Test-PlatformSupportMatrix.ps1'),
    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $MatrixPath).Path
$gate = (Resolve-Path -LiteralPath $GatePath).Path
$work = [IO.Path]::GetFullPath($WorkingDirectory)
if (Test-Path -LiteralPath $work) { throw "Negative-test working directory already exists: $work" }
New-Item -ItemType Directory -Path $work | Out-Null

function New-Scenario {
    param([string]$Name, [scriptblock]$Mutation)
    $document = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
    & $Mutation $document
    $path = Join-Path $work "$Name.json"
    [IO.File]::WriteAllText($path, (($document | ConvertTo-Json -Depth 100) + "`n"), [Text.UTF8Encoding]::new($false))
    return $path
}

function Assert-GateRejects {
    param([string]$Name, [string]$ScenarioPath, [string]$ExpectedPattern)
    $message = $null
    try {
        $output = & $gate -MatrixPath $ScenarioPath 2>&1 | Out-String
        throw "Gate accepted negative scenario '$Name'. Output: $output"
    }
    catch {
        $message = $_.Exception.Message
        if ($message -like 'Gate accepted negative scenario*') { throw }
    }
    if ($message -notmatch $ExpectedPattern) { throw "Scenario '$Name' failed for the wrong reason: $message" }
    Write-Output "DEPLOYSHARP_PLATFORM_MATRIX_NEGATIVE_OK scenario=$Name"
}

$releaseSupported = New-Scenario 'release-supported-overclaim' {
    param($document)
    $document.platforms[0].claims[0].level = 'ReleaseSupported'
}
Assert-GateRejects 'release-supported-overclaim' $releaseSupported 'cannot contain ReleaseSupported'

$missingEvidence = New-Scenario 'missing-evidence' {
    param($document)
    $document.platforms[0].claims[0].evidence[0] = 'missing/platform-evidence.json'
}
Assert-GateRejects 'missing-evidence' $missingEvidence 'evidence path is missing'

$runtimeDrift = New-Scenario 'runtime-version-drift' {
    param($document)
    $document.platforms[0].claims[0].runtimeDependencies[0].version = '99.0.0'
}
Assert-GateRejects 'runtime-version-drift' $runtimeDrift 'runtime version drift'

$untestedClaim = New-Scenario 'untested-platform-claim' {
    param($document)
    $document.platforms[1].claims = @($document.platforms[0].claims[5])
}
Assert-GateRejects 'untested-platform-claim' $untestedClaim 'must have zero positive claims'

$missingPackage = New-Scenario 'missing-package-claim' {
    param($document)
    $document.platforms[0].claims = @($document.platforms[0].claims | Select-Object -Skip 1)
}
Assert-GateRejects 'missing-package-claim' $missingPackage 'Windows x64 CPU package claims mismatch'

Write-Output 'DEPLOYSHARP_PLATFORM_MATRIX_NEGATIVE_SUITE_OK scenarios=5'
