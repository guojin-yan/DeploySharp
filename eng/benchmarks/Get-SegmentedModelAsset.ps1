[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Url,
    [Parameter(Mandatory = $true)]
    [string]$TargetPath,
    [Parameter(Mandatory = $true)]
    [long]$ExpectedSize,
    [string]$ExpectedSha256 = '',
    [ValidateRange(0, [long]::MaxValue)]
    [long]$RangeOffset = 0,
    [ValidateRange(1, 32)]
    [int]$SegmentCount = 16
)

$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($TargetPath)
$targetDirectory = Split-Path -Parent $target
$segmentDirectory = Join-Path $targetDirectory ([IO.Path]::GetFileName($target) + '.segments')
New-Item -ItemType Directory -Force -Path $targetDirectory, $segmentDirectory | Out-Null

$chunkSize = [long][Math]::Ceiling($ExpectedSize / [double]$SegmentCount)
$segments = for ($index = 0; $index -lt $SegmentCount; $index++) {
    $start = [long]($index * $chunkSize)
    $end = [long][Math]::Min($ExpectedSize - 1, $start + $chunkSize - 1)
    if ($start -gt $end) { continue }
    [pscustomobject]@{
        Index = $index
        Start = $start
        End = $end
        Path = Join-Path $segmentDirectory ($index.ToString('D2') + '.part')
    }
}

$jobs = foreach ($segment in $segments) {
    $expectedLength = $segment.End - $segment.Start + 1
    if ((Test-Path -LiteralPath $segment.Path) -and (Get-Item -LiteralPath $segment.Path).Length -eq $expectedLength) { continue }
    Start-Job -ArgumentList $Url, ($RangeOffset + $segment.Start), ($RangeOffset + $segment.End), $segment.Path -ScriptBlock {
        param($assetUrl, $rangeStart, $rangeEnd, $outputPath)
        $curlOutput = (& curl.exe --silent --show-error --fail --location --retry 5 --retry-all-errors --connect-timeout 15 --speed-time 60 --speed-limit 1024 --range "$rangeStart-$rangeEnd" --output $outputPath $assetUrl 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw "curl exited with code $LASTEXITCODE for range $rangeStart-$rangeEnd. $curlOutput" }
    }
}

if ($jobs.Count -gt 0) {
    $jobs | Wait-Job | Out-Null
    $failed = @($jobs | Where-Object State -ne 'Completed')
    $jobs | Receive-Job
    $jobs | Remove-Job -Force
    if ($failed.Count -gt 0) { throw "$($failed.Count) segmented downloads failed." }
}

foreach ($segment in $segments) {
    $expectedLength = $segment.End - $segment.Start + 1
    $actualLength = if (Test-Path -LiteralPath $segment.Path) { (Get-Item -LiteralPath $segment.Path).Length } else { -1 }
    if ($actualLength -ne $expectedLength) { throw "Segment $($segment.Index) length mismatch: expected $expectedLength, actual $actualLength." }
}

$mergePath = "$target.merge"
$output = [IO.File]::Create($mergePath)
try {
    foreach ($segment in $segments | Sort-Object Index) {
        $input = [IO.File]::OpenRead($segment.Path)
        try { $input.CopyTo($output) }
        finally { $input.Dispose() }
    }
}
finally {
    $output.Dispose()
}

$mergedSize = (Get-Item -LiteralPath $mergePath).Length
if ($mergedSize -ne $ExpectedSize) { throw "Merged size mismatch: expected $ExpectedSize, actual $mergedSize." }
$actualSha256 = (Get-FileHash -LiteralPath $mergePath -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256) -and $actualSha256 -ne $ExpectedSha256.ToLowerInvariant()) { throw "Merged SHA-256 mismatch: expected $ExpectedSha256, actual $actualSha256." }
Move-Item -LiteralPath $mergePath -Destination $target -Force

[ordered]@{
    path = $target
    size_bytes = $mergedSize
    sha256 = $actualSha256
    retained_segments = $segmentDirectory
} | ConvertTo-Json
