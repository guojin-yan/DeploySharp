param([string]$Destination = 'E:\Model\PaddleDocument\validation')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$assets = @{
    'img_rot180_demo.jpg' = 'c5a77e031470e13878ff4f28a06ca843fd455d95c20b1b49b486681e346209ed'
    'table_recognition.jpg' = 'acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c'
    'general_formula_rec_001.png' = '7885d4a349edcdfbfbc439305b8b554c2500f095949bc53d14835656b902dbcc'
}
foreach ($name in $assets.Keys) {
    $target = Join-Path $Destination $name
    if (!(Test-Path -LiteralPath $target)) {
        Invoke-WebRequest "https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/$name" -OutFile $target
    }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actual -ne $assets[$name]) { throw "Official example hash mismatch: $target" }
    Write-Output "$name SHA256=$actual"
}
