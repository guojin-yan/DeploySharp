param(
    [Parameter(Mandatory = $true)]
    [string[]] $DocumentationFile
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($inputPath in $DocumentationFile) {
  foreach ($path in ($inputPath -split ',')) {
    $resolvedPath = Resolve-Path -LiteralPath $path
    [xml] $document = Get-Content -Raw -Encoding UTF8 -LiteralPath $resolvedPath

    foreach ($member in $document.doc.members.member) {
        $content = $member.InnerText
        if ($content -notmatch '[A-Za-z]') {
            $failures.Add("$($member.name): missing English documentation")
        }

        if ($content -notmatch '[\u3400-\u9fff]') {
            $failures.Add("$($member.name): missing Chinese documentation")
        }
    }
  }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Bilingual API documentation validation failed with $($failures.Count) error(s)."
}

Write-Host "Bilingual API documentation validation passed for $($DocumentationFile.Count) file(s)."
