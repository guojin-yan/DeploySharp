[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ModelPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$OpenVinoRoot = 'D:\Program Files\openvino_2025.4.0',

    [string]$PythonPath = 'C:\ProgramData\anaconda3\python.exe'
)

$ErrorActionPreference = 'Stop'

$resolvedModel = (Resolve-Path -LiteralPath $ModelPath).Path
$resolvedOpenVino = (Resolve-Path -LiteralPath $OpenVinoRoot).Path
$resolvedPython = (Resolve-Path -LiteralPath $PythonPath).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedModel)
$xmlPath = Join-Path $resolvedOutput ($baseName + '.xml')
$binPath = Join-Path $resolvedOutput ($baseName + '.bin')

# Keep OpenVINO's Python and native search paths local to this process.
$env:INTEL_OPENVINO_DIR = $resolvedOpenVino
$env:OPENVINO_LIB_PATHS = "$resolvedOpenVino\runtime\3rdparty\tbb\bin;$resolvedOpenVino\runtime\bin\intel64\Release;$resolvedOpenVino\runtime\bin\intel64\Debug"
$env:PATH = "$env:OPENVINO_LIB_PATHS;$env:PATH"
$env:PYTHONPATH = "$resolvedOpenVino\python;$resolvedOpenVino\python\python3"

# OVC conversion stays reproducible and keeps FP32 weights for numerical comparison.
if (-not (Test-Path -LiteralPath '.\eng\models\yolo\convert_yolo_onnx_to_openvino_ir.py' -PathType Leaf)) {
    throw 'Run this script from the DeploySharp Git root so the converter helper can be resolved.'
}
Write-Verbose ("Python: {0}; model: {1}; output: {2}" -f $resolvedPython, $resolvedModel, $xmlPath)
& $resolvedPython '.\eng\models\yolo\convert_yolo_onnx_to_openvino_ir.py' $resolvedModel $xmlPath
if ($LASTEXITCODE -ne 0) {
    throw "OpenVINO OVC conversion failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $xmlPath -PathType Leaf) -or -not (Test-Path -LiteralPath $binPath -PathType Leaf)) {
    throw 'OpenVINO conversion did not produce the expected XML/BIN pair.'
}

$xml = Get-Item -LiteralPath $xmlPath
$bin = Get-Item -LiteralPath $binPath
$xmlHash = (Get-FileHash -LiteralPath $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
$binHash = (Get-FileHash -LiteralPath $binPath -Algorithm SHA256).Hash.ToLowerInvariant()

[pscustomobject]@{
    OpenVinoVersion = '2025.4.0'
    SourceModel = $resolvedModel
    XmlPath = $xmlPath
    XmlSize = $xml.Length
    XmlSha256 = $xmlHash
    BinPath = $binPath
    BinSize = $bin.Length
    BinSha256 = $binHash
} | Format-List
