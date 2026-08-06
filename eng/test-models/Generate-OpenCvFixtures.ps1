param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\tests\assets\opencv')
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Get-Crc32([byte[]]$bytes) {
    [uint32]$crc = [uint32]::MaxValue
    foreach ($value in $bytes) {
        $crc = $crc -bxor [uint32]$value
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 1) -ne 0) { $crc = ($crc -shr 1) -bxor 0xedb88320 } else { $crc = $crc -shr 1 }
        }
    }
    return [uint32]($crc -bxor 0xffffffff)
}

function Get-BigEndian([uint32]$value) {
    return ,([byte[]]@([byte](($value -shr 24) -band 255), [byte](($value -shr 16) -band 255), [byte](($value -shr 8) -band 255), [byte]($value -band 255)))
}

function Get-Adler32([byte[]]$bytes) {
    [uint32]$a = 1
    [uint32]$b = 0
    foreach ($value in $bytes) {
        $a = ($a + $value) % 65521
        $b = ($b + $a) % 65521
    }
    return (($b -shl 16) -bor $a)
}

function New-Png([int]$width, [int]$height, [byte]$colorType, [byte[]]$scanlines, [string]$path) {
    $script:pngMemory = New-Object System.IO.MemoryStream
    $script:pngMemory.Write([byte[]](137,80,78,71,13,10,26,10), 0, 8)
    function Write-Chunk([string]$name, [byte[]]$payload) {
        $type = [System.Text.Encoding]::ASCII.GetBytes($name)
        $length = Get-BigEndian ([uint32]$payload.Length)
        $crc = Get-BigEndian (Get-Crc32 ([byte[]]($type + $payload)))
        $script:pngMemory.Write($length, 0, 4)
        $script:pngMemory.Write($type, 0, 4)
        if ($payload.Length -gt 0) { $script:pngMemory.Write($payload, 0, $payload.Length) }
        $script:pngMemory.Write($crc, 0, 4)
    }
    $ihdr = New-Object byte[] 13
    [System.Buffer]::BlockCopy((Get-BigEndian ([uint32]$width)), 0, $ihdr, 0, 4)
    [System.Buffer]::BlockCopy((Get-BigEndian ([uint32]$height)), 0, $ihdr, 4, 4)
    $ihdr[8] = 8
    $ihdr[9] = $colorType
    Write-Chunk 'IHDR' $ihdr
    $compressed = New-Object System.IO.MemoryStream
    $deflate = New-Object System.IO.Compression.DeflateStream($compressed, [System.IO.Compression.CompressionLevel]::Optimal, $true)
    $deflate.Write($scanlines, 0, $scanlines.Length)
    $deflate.Dispose()
    # DeflateStream emits the raw DEFLATE payload; PNG IDAT requires a zlib header and Adler-32 trailer.
    $rawDeflate = $compressed.ToArray()
    Write-Chunk 'IDAT' ([byte[]](@(0x78, 0x9c) + $rawDeflate + (Get-BigEndian (Get-Adler32 $scanlines))))
    Write-Chunk 'IEND' ([byte[]]@())
    [System.IO.File]::WriteAllBytes($path, $script:pngMemory.ToArray())
    $script:pngMemory.Dispose()
    $compressed.Dispose()
}

New-Png 3 2 2 ([byte[]]@(0,255,0,0,0,255,0,0,0,255, 0,255,255,0,255,0,255,0,255,255)) (Join-Path $OutputDirectory 'rgb.png')
New-Png 3 2 0 ([byte[]]@(0,0,64,128, 0,192,224,255)) (Join-Path $OutputDirectory 'gray.png')
New-Png 2 2 6 ([byte[]]@(0,255,0,0,255,0,255,0,128, 0,0,0,255,0,255,255,255,64)) (Join-Path $OutputDirectory 'alpha.png')

$ocrScanlines = New-Object 'System.Collections.Generic.List[byte]'
for ($y = 0; $y -lt 16; $y++) {
    $ocrScanlines.Add(0)
    for ($x = 0; $x -lt 32; $x++) {
        if ($x -ge 2 -and $x -lt 14 -and $y -ge 2 -and $y -lt 6) { $red = 240; $green = 245; $blue = 250 }
        elseif ($x -ge 16 -and $x -lt 30 -and $y -ge 8 -and $y -lt 14) { $red = 245; $green = 210; $blue = 60 }
        else { $red = 20; $green = 30; $blue = 40 }
        $ocrScanlines.Add([byte]$red)
        $ocrScanlines.Add([byte]$green)
        $ocrScanlines.Add([byte]$blue)
    }
}
New-Png 32 16 2 $ocrScanlines.ToArray() (Join-Path $OutputDirectory 'ocr.png')

$anomalyScanlines = New-Object 'System.Collections.Generic.List[byte]'
for ($y = 0; $y -lt 3; $y++) {
    $anomalyScanlines.Add(0)
    for ($x = 0; $x -lt 5; $x++) {
        $anomalyScanlines.Add([byte](20 + ($x * 30)))
        $anomalyScanlines.Add([byte](15 + ($y * 70)))
        $anomalyScanlines.Add([byte](200 - ($x * 20) - ($y * 10)))
    }
}
New-Png 5 3 2 $anomalyScanlines.ToArray() (Join-Path $OutputDirectory 'anomaly.png')

$manifest = Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.png' | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    [ordered]@{ name = $_.Name; size = $_.Length; sha256 = $hash; license = 'Apache-2.0'; source = 'DeploySharp deterministic fixture generator'; purpose = 'Visual.OpenCV adapter contract; not an official model or image asset' }
}
[System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'fixtures.json'), ($manifest | ConvertTo-Json -Depth 4), $utf8)
[System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'golden.json'), (@{
    classification = @{ topIndex = 0; label = 'one' }
    detection = @{ count = 2; labels = @('dog', 'cat') }
    semanticSegmentation = @{ width = 3; height = 2; classes = @(0, 1, 2, 0, 0, 1); sha256 = '2ed4fa5094662ebe63d9265149adf86858fd7b03983a35118880f09517f824de' }
    pose = @{ count = 2; firstKeypoint = @(0.6, 0.4); sha256 = 'c075d9722f8bd716686a781022a5234b3b53cd977cccd8e989e7b4637b8ac4a0' }
    ocr = @{ count = 2; text = @('AB', 'CA'); characterSet = 'ABC'; blankIndex = 0 }
    anomaly = @{ imageScore = 0.875; width = 5; height = 3; channels = 2; threshold = 0.6; aggregation = 'maximum' }
} | ConvertTo-Json -Depth 4), $utf8)
[System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'README.md'), @'
# OpenCV adapter fixtures

These tiny RGB, grayscale, alpha, OCR-pattern, and anomaly-pattern PNG files are deterministic Apache-2.0 contract fixtures generated by `eng/test-models/Generate-OpenCvFixtures.ps1`. They are not official algorithm assets and are not part of the ModelFactory catalog or a GitHub Release.
'@, $utf8)
