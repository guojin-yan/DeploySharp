[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$OutputStem = 'paddleocr-ppstructure-samples-build-20261011'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$outputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repoRoot 'eng\models\paddle-ocr\verification'
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
}
$outputDirectory = (Resolve-Path -LiteralPath $outputDirectory -ErrorAction SilentlyContinue)?.Path
if (-not $outputDirectory) {
    $outputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        Join-Path $repoRoot 'eng\models\paddle-ocr\verification'
    } else {
        [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
    }
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$projects = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'samples') -Recurse -Filter '*.csproj' |
    Sort-Object FullName)
if ($projects.Count -eq 0) {
    throw "No sample projects found under $repoRoot\samples."
}

$sourceRevision = (& git -C $repoRoot rev-parse HEAD 2>$null).Trim()
$workingTreeWasDirty = [bool](& git -C $repoRoot status --porcelain --untracked-files=no 2>$null)
$dotnetVersion = (& dotnet --version).Trim()
$osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription

$results = [System.Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $projectXml = [xml](Get-Content -Raw -LiteralPath $project.FullName)
    $propertyGroup = @($projectXml.Project.PropertyGroup) | Select-Object -First 1
    $targetFramework = if ($propertyGroup.TargetFramework) {
        [string]$propertyGroup.TargetFramework
    } elseif ($propertyGroup.TargetFrameworks) {
        [string]$propertyGroup.TargetFrameworks
    } else {
        'implicit/unknown'
    }

    $lines = @(& dotnet build $project.FullName -c Release --no-restore --nologo -v:q 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $errorLines = @($lines | Where-Object { $_ -match '\berror\s+[A-Z]+\d+\b' })
    $warningLines = @($lines | Where-Object { $_ -match '\bwarning\s+[A-Z]+\d+\b' })
    $relativeProject = [IO.Path]::GetRelativePath($repoRoot, $project.FullName).Replace('\', '/')

    $results.Add([ordered]@{
        project = $relativeProject
        targetFramework = $targetFramework
        configuration = 'Release'
        restore = '--no-restore'
        exitCode = $exitCode
        success = ($exitCode -eq 0)
        errors = $errorLines.Count
        warnings = $warningLines.Count
        errorSummary = @($errorLines | Select-Object -First 8)
    })
}

$passed = @($results | Where-Object success).Count
$failed = $results.Count - $passed
$generatedUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$report = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-ppstructure-sample-build-v1'
    generatedUtc = $generatedUtc
    sourceRevision = $sourceRevision
    workingTreeWasDirty = $workingTreeWasDirty
    environment = [ordered]@{
        os = $osDescription
        dotnet = $dotnetVersion
        configuration = 'Release'
        restore = '--no-restore'
        frameworkSelection = 'Each sample project target framework declared in its csproj'
    }
    projects = @($results)
    total = [ordered]@{
        projects = $results.Count
        passed = $passed
        failed = $failed
    }
    boundary = 'Build-only evidence. A successful build does not prove model availability, backend support, inference correctness, quality, performance, or long-running stability.'
}

$jsonPath = Join-Path $outputDirectory ($OutputStem + '.json')
$markdownPath = Join-Path $outputDirectory ($OutputStem + '.md')
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# DeploySharp sample build evidence ($($generatedUtc.Substring(0,10)))")
[void]$md.AppendLine('')
[void]$md.AppendLine("Environment: $osDescription, .NET SDK ``$dotnetVersion``, Release, ``--no-restore``. Source revision ``$sourceRevision``. The working tree was dirty: ``$workingTreeWasDirty``. Each project was built using its own declared target framework.")
[void]$md.AppendLine('')
[void]$md.AppendLine('| Sample project | Target framework | Result | Errors | Warnings |')
[void]$md.AppendLine('| --- | --- | --- | ---: | ---: |')
foreach ($item in $results) {
    $resultText = if ($item.success) { 'PASS' } else { 'FAIL' }
    [void]$md.AppendLine("| ``$($item.project)`` | ``$($item.targetFramework)`` | $resultText | $($item.errors) | $($item.warnings) |")
}
[void]$md.AppendLine('')
[void]$md.AppendLine("**Total:** $passed passed, $failed failed, $($results.Count) projects.")
[void]$md.AppendLine('')
[void]$md.AppendLine('This is build-only evidence. It does not prove model availability, backend support, inference correctness, quality, performance, or long-running stability.')
[void]$md.AppendLine('')
[void]$md.AppendLine("Machine-readable record: [$([IO.Path]::GetFileName($jsonPath))]($([IO.Path]::GetFileName($jsonPath))).")
$md.ToString() | Set-Content -LiteralPath $markdownPath -Encoding UTF8

Write-Output "Wrote $jsonPath"
Write-Output "Wrote $markdownPath"
if ($failed -gt 0) {
    exit 1
}
