[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$OutputStem = 'paddleocr-ppstructure-source-build-20261011'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'eng\models\paddle-ocr\verification'
} else {
    $OutputDirectory = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$projects = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Recurse -Filter '*.csproj' | Sort-Object FullName)
if ($projects.Count -eq 0) { throw "No source projects found under $repoRoot\src." }
$sourceRevision = (& git -C $repoRoot rev-parse HEAD 2>$null).Trim()
$dirty = [bool](& git -C $repoRoot status --porcelain --untracked-files=no 2>$null)
$results = [System.Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $xml = [xml](Get-Content -Raw -LiteralPath $project.FullName)
    $group = @($xml.Project.PropertyGroup) | Select-Object -First 1
    $frameworks = if ($group.TargetFrameworks) { [string]$group.TargetFrameworks } elseif ($group.TargetFramework) { [string]$group.TargetFramework } else { 'implicit/imported' }
    $lines = @(& dotnet build $project.FullName -c Release --no-restore --nologo -v:q 2>&1 | ForEach-Object { [string]$_ })
    $code = $LASTEXITCODE
    $errors = @($lines | Where-Object { $_ -match '\berror\s+[A-Z]+\d+\b' })
    $warnings = @($lines | Where-Object { $_ -match '\bwarning\s+[A-Z]+\d+\b' })
    $results.Add([ordered]@{
        project = [IO.Path]::GetRelativePath($repoRoot, $project.FullName).Replace('\', '/')
        targetFrameworks = $frameworks
        configuration = 'Release'
        restore = '--no-restore'
        exitCode = $code
        success = ($code -eq 0)
        errors = $errors.Count
        warnings = $warnings.Count
        errorSummary = @($errors | Select-Object -First 8)
    })
}

$passed = @($results | Where-Object success).Count
$failed = $results.Count - $passed
$utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$report = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-ppstructure-source-build-v1'
    generatedUtc = $utc
    sourceRevision = $sourceRevision
    workingTreeWasDirty = $dirty
    environment = [ordered]@{ os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription; dotnet = (& dotnet --version).Trim(); configuration = 'Release'; restore = '--no-restore' }
    projects = @($results)
    total = [ordered]@{ projects = $results.Count; passed = $passed; failed = $failed }
    boundary = 'Build-only evidence. It does not prove model availability, native runtime loading, backend support, inference correctness, quality, performance, packaging, or long-running stability.'
}
$jsonPath = Join-Path $OutputDirectory ($OutputStem + '.json')
$mdPath = Join-Path $OutputDirectory ($OutputStem + '.md')
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# DeploySharp source project build evidence ($($utc.Substring(0,10)))")
[void]$md.AppendLine('')
[void]$md.AppendLine("Environment: $($report.environment.os), .NET SDK ``$($report.environment.dotnet)``, Release, ``--no-restore``. Source revision ``$sourceRevision``. Working tree dirty: ``$dirty``.")
[void]$md.AppendLine('')
[void]$md.AppendLine('| Source project | Declared target frameworks | Result | Errors | Warnings |')
[void]$md.AppendLine('| --- | --- | --- | ---: | ---: |')
foreach ($item in $results) {
    [void]$md.AppendLine("| ``$($item.project)`` | ``$($item.targetFrameworks)`` | $(if($item.success){'PASS'}else{'FAIL'}) | $($item.errors) | $($item.warnings) |")
}
[void]$md.AppendLine('')
[void]$md.AppendLine("**Total:** $passed passed, $failed failed, $($results.Count) projects.")
[void]$md.AppendLine('')
[void]$md.AppendLine($report.boundary)
[void]$md.AppendLine('')
[void]$md.AppendLine("Machine-readable record: [$([IO.Path]::GetFileName($jsonPath))]($([IO.Path]::GetFileName($jsonPath))).")
$md.ToString() | Set-Content -LiteralPath $mdPath -Encoding UTF8
Write-Output "Wrote $jsonPath"
Write-Output "Wrote $mdPath"
if ($failed -gt 0) { exit 1 }
