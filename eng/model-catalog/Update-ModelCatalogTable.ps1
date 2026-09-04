param(
    [string] $CatalogPath = "src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json",
    [string] $OutputPath = "docs/articles/model-catalog.md",
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$catalog = Get-Content -Raw -Encoding UTF8 -LiteralPath $CatalogPath | ConvertFrom-Json
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# 官方模型目录')
$lines.Add('')
$lines.Add('本表由 `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json` 自动生成，请勿手工编辑表格行。')
$lines.Add('')
$entryCount = @($catalog.entries).Count
$lines.Add(('当前目录包含 {0} 条 `preview` 记录。Preview 条目可以下载，并会在 ModelFactory 中按大小和 SHA-256 校验；查询时必须显式设置 `includePreview: true`。' -f $entryCount))
$lines.Add('')
$lines.Add('模型资产按模型族放在长期维护的 GitHub Release 集合中：视觉模型使用 [`models-visual.1`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-visual.1)，语言模型使用 [`models-llm.1`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-llm.1)。后续兼容模型直接追加到对应 Release，并在 Release notes 中按上传日期记录；只有发生不兼容的资产合同变化时才提升集合版本号。')
$lines.Add('')
$lines.Add('| 模型 ID | 模型族 / 任务 | 工件 | 格式 | 后端 | 精度 / 量化 | 可移植 | Release 标签 | 大小 | SHA256 | 下载 | 测试输入 | 许可标识 |')
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
$lines.Add('目录中的条目对应已经发布到 GitHub Release 的 ModelFactory 资产；下载时会校验清单、文件大小和 SHA-256。Preview 条目需要在查询中显式设置 `includePreview: true`。')
$content = ($lines -join "`n") + "`n"

if ($Check) {
    $existing = (Get-Content -Raw -Encoding UTF8 -LiteralPath $OutputPath) -replace "`r`n", "`n"
    if ($existing -ne $content) { throw "Generated model catalog table is stale. Run eng/model-catalog/Update-ModelCatalogTable.ps1." }
    Write-Host 'Generated model catalog table is current.'
} else {
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $OutputPath), $content, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated $OutputPath"
}
