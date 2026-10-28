[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Exercise the gate's helpers without rebuilding packages or requiring publication credentials.
$gatePath = Join-Path $PSScriptRoot 'Test-ReleaseEvidence.ps1'
$parseErrors = $null
$tokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($gatePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Release evidence gate contains PowerShell syntax errors.' }
foreach ($name in @('Get-CanonicalValue', 'Get-CanonicalJson', 'Assert-SameKeySet')) {
    $functions = @($ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $false))
    if ($functions.Count -ne 1) { throw "Expected exactly one gate helper: $name" }
    . ([scriptblock]::Create($functions[0].Extent.Text))
}

foreach ($json in @('[]', '["one"]', '["one","two"]', '{"empty":[],"nested":[[]],"single":["one"]}')) {
    $value = ConvertFrom-Json -InputObject $json -AsHashtable -NoEnumerate
    $actual = Get-CanonicalJson $value
    if ($actual -ne $json) { throw "JSON collection shape changed: expected $json; found $actual" }
}

$retained = Get-CanonicalValue (@{ releaseBlockers = @('publication-authority-not-granted') })
$authorized = Get-CanonicalValue (@{ releaseBlockers = @() })
$runStateBlockers = @('dirty-worktree', 'publication-authority-not-granted')
$expected = @($retained.releaseBlockers | Where-Object { $_ -notin $runStateBlockers })
$actual = @($authorized.releaseBlockers | Where-Object { $_ -notin $runStateBlockers })
Assert-SameKeySet $expected $actual 'Authorized release blockers'

foreach ($pair in @(
    @{ Expected = @(); Actual = @('sourcelink-missing-or-drifted') },
    @{ Expected = @('sourcelink-missing-or-drifted'); Actual = @() }
)) {
    $rejected = $false
    try { Assert-SameKeySet $pair.Expected $pair.Actual 'Technical release blockers' }
    catch {
        if ($_.Exception.Message -notlike 'Technical release blockers drift.*') { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw 'Technical release blocker drift was accepted.' }
}

Write-Output 'DEPLOYSHARP_RELEASE_EVIDENCE_COLLECTIONS_OK json-shapes=4 authorized-transition=1 blocker-drift-rejected=2'
