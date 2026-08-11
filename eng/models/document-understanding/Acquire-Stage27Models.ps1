[CmdletBinding()]
param(
    [string] $Warehouse = 'E:\DeploySharp-Models',
    [switch] $IncludeCordTest
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-PinnedFile {
    param(
        [Parameter(Mandatory = $true)][string] $Repository,
        [Parameter(Mandatory = $true)][string] $Revision,
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        if ((Get-Item -LiteralPath $Destination).Length -gt 0) { return }
        Remove-Item -LiteralPath $Destination -Force
    }

    $parent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $uri = "https://huggingface.co/$Repository/resolve/$Revision/$RelativePath`?download=true"
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $Destination -TimeoutSec 1200
            if ((Get-Item -LiteralPath $Destination).Length -le 0) { throw "Downloaded file is empty: $Destination" }
            return
        }
        catch {
            if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Force }
            if ($attempt -eq 5) { throw }
            Start-Sleep -Seconds ([Math]::Min(5 * $attempt, 20))
        }
    }
}

$models = @(
    @{
        Name = 'layoutlmv3-base'
        Repository = 'microsoft/layoutlmv3-base'
        Revision = 'cfbbbff0762e6aab37086fdd4739ad14fe7d5db4'
        Files = @('README.md', 'config.json', 'preprocessor_config.json', 'tokenizer_config.json', 'vocab.json', 'merges.txt')
    },
    @{
        Name = 'donut-base-finetuned-cord-v2'
        Repository = 'naver-clova-ix/donut-base-finetuned-cord-v2'
        Revision = '8003d433113256b4ce3a0f5bf604b29ff78a7451'
        Files = @('README.md', 'added_tokens.json', 'config.json', 'preprocessor_config.json', 'pytorch_model.bin', 'sentencepiece.bpe.model', 'special_tokens_map.json', 'tokenizer.json', 'tokenizer_config.json')
    },
    @{
        Name = 'pix2struct-docvqa-base'
        Repository = 'google/pix2struct-docvqa-base'
        Revision = '63f6b3de436e39f75c7a486881a9c2c14a7f4e89'
        Files = @('README.md', 'config.json', 'preprocessor_config.json', 'special_tokens_map.json', 'spiece.model', 'tokenizer.json', 'tokenizer_config.json')
    }
)

foreach ($model in $models) {
    foreach ($file in $model.Files) {
        $destination = Join-Path (Join-Path (Join-Path $Warehouse $model.Name) 'checkpoint') $file
        Get-PinnedFile -Repository $model.Repository -Revision $model.Revision -RelativePath $file -Destination $destination
    }
}

if ($IncludeCordTest) {
    $datasetRoot = Join-Path (Join-Path $Warehouse 'donut-base-finetuned-cord-v2') 'dataset'
    Get-PinnedFile `
        -Repository 'datasets/naver-clova-ix/cord-v2' `
        -Revision '7f0115a4b758a71d6473b8d085751692da2fef98' `
        -RelativePath 'data/test-00000-of-00001-9c204eb3f4e11791.parquet' `
        -Destination (Join-Path $datasetRoot 'test-00000-of-00001-9c204eb3f4e11791.parquet')
}

Write-Output 'DEPLOYSHARP_STAGE27_ACQUISITION_OK'
