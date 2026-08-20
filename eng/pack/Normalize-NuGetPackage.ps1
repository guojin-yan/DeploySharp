[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = (Resolve-Path -LiteralPath $PackageDirectory).Path
$fixedTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$fixedCorePath = 'package/services/metadata/core-properties/core.psmdcp'

function Get-EntryBytes {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)
    $stream = $Entry.Open()
    $memory = [System.IO.MemoryStream]::new()
    try {
        $stream.CopyTo($memory)
        return $memory.ToArray()
    }
    finally {
        $stream.Dispose()
        $memory.Dispose()
    }
}

function Get-EntryText {
    param([byte[]]$Bytes)
    return [Text.Encoding]::UTF8.GetString($Bytes)
}

function Get-NormalizedRelationships {
    param([string]$Text, [string]$OldCorePath)
    [xml]$document = $Text
    foreach ($relationship in @($document.SelectNodes("//*[local-name()='Relationship']"))) {
        if ([string]$relationship.Type -eq 'http://schemas.microsoft.com/packaging/2010/07/manifest') {
            $relationship.Id = 'rIdManifest'
        }
        elseif ([string]$relationship.Type -eq 'http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties') {
            $relationship.Id = 'rIdCoreProperties'
            $relationship.Target = '/' + $fixedCorePath
        }
    }
    return $document.OuterXml
}

function Get-NormalizedCoreProperties {
    param([string]$Text)
    $normalized = [regex]::Replace($Text, '<dcterms:(?:created|modified)[^>]*>.*?</dcterms:(?:created|modified)>', '', [Text.RegularExpressions.RegexOptions]::Singleline)
    return $normalized
}

$files = @(Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Extension -in @('.nupkg', '.snupkg') } | Sort-Object Name)
if ($files.Count -eq 0) { throw "No .nupkg or .snupkg files found in $root." }

foreach ($file in $files) {
    $archive = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        if ($null -ne $archive.GetEntry('.signature.p7s')) {
            throw "Refusing to normalize signed package: $($file.Name). Normalize before signing."
        }
        $entries = [ordered]@{}
        $coreEntries = @($archive.Entries | Where-Object { $_.FullName -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$' })
        if ($coreEntries.Count -gt 1) { throw "Package contains multiple core-properties entries: $($file.Name)." }
        $oldCorePath = if ($coreEntries.Count -eq 1) { $coreEntries[0].FullName } else { $null }

        foreach ($entry in @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })) {
            $name = $entry.FullName
            $bytes = Get-EntryBytes -Entry $entry
            if ($null -ne $oldCorePath -and $name -eq $oldCorePath) {
                $name = $fixedCorePath
                $bytes = [Text.Encoding]::UTF8.GetBytes((Get-NormalizedCoreProperties -Text (Get-EntryText -Bytes $bytes)))
            }
            elseif ($name -eq '_rels/.rels' -and $null -ne $oldCorePath) {
                $bytes = [Text.Encoding]::UTF8.GetBytes((Get-NormalizedRelationships -Text (Get-EntryText -Bytes $bytes) -OldCorePath $oldCorePath))
            }
            $entries[$name] = $bytes
        }
    }
    finally { $archive.Dispose() }

    $tempPath = $file.FullName + '.deterministic.tmp'
    if (Test-Path -LiteralPath $tempPath) { [IO.File]::Delete($tempPath) }
    $output = [IO.Compression.ZipFile]::Open($tempPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @($entries.Keys | Sort-Object)) {
            $entry = $output.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTime
            $stream = $entry.Open()
            try { $stream.Write($entries[$name], 0, $entries[$name].Length) }
            finally { $stream.Dispose() }
        }
    }
    finally { $output.Dispose() }

    [IO.File]::Delete($file.FullName)
    [IO.File]::Move($tempPath, $file.FullName)
    Write-Output "DEPLOYSHARP_NUGET_NORMALIZED file=$($file.Name) entries=$($entries.Count)"
}
