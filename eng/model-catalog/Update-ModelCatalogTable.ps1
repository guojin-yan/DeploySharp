param(
    [string] $CatalogPath = "src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json",
    [string] $OutputPath = "docs/articles/model-catalog.md",
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$catalog = Get-Content -Raw -Encoding UTF8 -LiteralPath $CatalogPath | ConvertFrom-Json
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Official model catalog / 官方模型目录')
$lines.Add('')
$lines.Add('This table is generated from `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json`. Do not edit table rows by hand. / 本表由 `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json` 生成，请勿手工编辑表格行。')
$lines.Add('')
$lines.Add('The catalog currently contains 39 entries and every entry has `preview` status. Preview entries are public, downloadable, SHA-256-checked ModelFactory assets; they are not `AlgorithmVerified` and do not imply GA. Querying without `includePreview: true` intentionally excludes them. / 当前目录包含 39 条记录，且全部为 `preview`。Preview 是公开、可下载并完成 SHA-256 校验的 ModelFactory 资产；它们不是 `AlgorithmVerified`，也不代表 GA。不带 `includePreview: true` 的查询会按设计排除这些条目。')
$lines.Add('')
$lines.Add('The first PP-OCRv5 algorithm-admission candidate is `paddleocr/ppocrv5/mobile-cls`. Its source/archive binding, exporter reproduction, immutable Preview Release, ONNX hash, fixed-image preprocessing, ORT/OpenVINO local golden, and independent Paddle Predictor output are recorded. `license-and-redistribution` remains open in `paddleocr-release-admission.json` because no attributable model/dictionary redistribution approval has been recorded, so it remains Preview. / 首个 PP-OCRv5 算法准入候选为 `paddleocr/ppocrv5/mobile-cls`。其来源/归档绑定、导出复现、不可变 Preview Release、ONNX 哈希、固定图像前处理、ORT/OpenVINO 本机 golden 和独立 Paddle Predictor 输出均已记录；因尚未记录可归属的模型/字典再分发批准，`paddleocr-release-admission.json` 中的 `license-and-redistribution` 仍为 open，因此继续保持 Preview。')
$lines.Add('')
$lines.Add('| ModelId | Algorithm / Task | Artifact | Format | Backend | Precision / Quantization | Portable | Release tag | Size | SHA256 | Download | Test input | License |')
$lines.Add('|---|---|---|---|---|---|---|---|---:|---|---|---|---|')

$rows = 0
foreach ($entry in @($catalog.entries | Sort-Object modelId)) {
    foreach ($artifact in @($entry.artifacts | Sort-Object artifactId)) {
        $manifest = @($artifact.assets | Where-Object kind -eq 'manifest' | Select-Object -First 1)
        $models = @($artifact.assets | Where-Object kind -eq 'model')
        $tests = @($entry.testInputs | Where-Object kind -eq 'testInput')
        $size = ($models | Measure-Object -Property size -Sum).Sum
        if ($null -eq $size) { $size = 0 }
        $hash = ($models | ForEach-Object sha256) -join '<br>'
        $download = if ($manifest.Count -gt 0) { "[manifest]($($manifest[0].downloadUrl))" } else { '—' }
        $testLinks = (@($tests | ForEach-Object { "[$($_.assetId)]($($_.downloadUrl))" })) -join '<br>'
        if ([string]::IsNullOrWhiteSpace($testLinks)) { $testLinks = '—' }
        $backend = (@($artifact.compatibleBackends)) -join '<br>'
        $precision = "$($artifact.precision) / $($artifact.quantization)".Trim(' ', '/')
        if ([string]::IsNullOrWhiteSpace($precision)) { $precision = '—' }
        $license = if ($entry.source.licenseExpression) { $entry.source.licenseExpression } else { $entry.source.licenseFile }
        $lines.Add("| $($entry.modelId) | $($entry.family) / $($entry.task) | $($artifact.artifactId) | $($artifact.format) | $backend | $precision | $($artifact.portable) | $($entry.release.tag) | $size | $hash | $download | $testLinks | $license |")
        $rows++
    }
}

if ($rows -eq 0) {
    $lines.Add('| _No approved models published / 尚无已批准发布模型_ | — | — | — | — | — | — | — | — | — | — | — | — |')
}

$lines.Add('')
$lines.Add('The catalog lists only models actually published in an immutable GitHub Release with source, license, exact size, and SHA-256 metadata. Preview entries require an explicit `includePreview: true` query. / 目录仅列出已在不可变 GitHub Release 中实际发布，且带有来源、许可证、精确大小与 SHA-256 元数据的模型。预览条目须在查询中显式设置 `includePreview: true`。')
$content = ($lines -join "`n") + "`n"

if ($Check) {
    $existing = (Get-Content -Raw -Encoding UTF8 -LiteralPath $OutputPath) -replace "`r`n", "`n"
    if ($existing -ne $content) { throw "Generated model catalog table is stale. Run eng/model-catalog/Update-ModelCatalogTable.ps1." }
    Write-Host 'Generated model catalog table is current.'
} else {
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $OutputPath), $content, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated $OutputPath"
}
