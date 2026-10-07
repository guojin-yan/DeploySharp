param(
    [string]$DatasetRoot = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public',
    [string]$VerificationRoot = 'eng/models/paddle-document/verification',
    [string]$SummaryJsonPath = 'eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007-summary.json',
    [string]$SummaryMarkdownPath = 'eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$verificationFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $VerificationRoot))
$datasetFull = [IO.Path]::GetFullPath($DatasetRoot)
$datasetCsv = Join-Path $datasetFull 'test-corrected_normalized.csv'
$datasetManifest = Join-Path $datasetFull 'realFormula-manifest.jsonl'
$datasetArchive = [IO.Path]::GetFullPath((Join-Path $datasetFull '..\..\realFormula.zip'))
foreach ($requiredDatasetFile in @($datasetCsv, $datasetManifest)) {
    if (!(Test-Path -LiteralPath $requiredDatasetFile -PathType Leaf)) { throw "Missing pinned dataset file: $requiredDatasetFile" }
}
$models = @('pp-formulanet-plus-s', 'pp-formulanet-plus-m', 'pp-formulanet-plus-l', 'pp-formulanet-s', 'pp-formulanet-l', 'unimernet')
$rows = [System.Collections.Generic.List[object]]::new()
$datasetCsvSha = $null
$datasetManifestSha = (Get-FileHash -LiteralPath $datasetManifest -Algorithm SHA256).Hash.ToLowerInvariant()
$datasetCsvSha = (Get-FileHash -LiteralPath $datasetCsv -Algorithm SHA256).Hash.ToLowerInvariant()
$datasetArchiveSha = if (Test-Path -LiteralPath $datasetArchive -PathType Leaf) { (Get-FileHash -LiteralPath $datasetArchive -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
foreach ($model in $models) {
    $fileName = "formula-realformula-$model-ort-20261007.json"
    $path = Join-Path $verificationFull $fileName
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing per-model result: $path" }
    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($report.schemaVersion -ne 'deploysharp-realformula-six-model-quality-v1') { throw "Unexpected report schema in $path" }
    if ($report.dataset.name -ne 'MathNet realFormula' -or $report.dataset.annotatedSampleCount -ne 121 -or $report.dataset.evaluatedSampleCount -ne 121) {
        throw "Dataset identity/count mismatch in $path"
    }
    if ($report.dataset.csvSha256 -ne $datasetCsvSha) { throw "Dataset CSV hash does not match the current pinned input: $path" }
    if ($report.modelResults.Count -ne 1) { throw "Expected a single-model report in $path" }
    $report | Add-Member -NotePropertyName modelCount -NotePropertyValue 1 -Force
    $report.dataset | Add-Member -NotePropertyName recordUrl -NotePropertyValue 'https://zenodo.org/records/11296815' -Force
    $report.dataset | Add-Member -NotePropertyName attribution -NotePropertyValue 'Schmitt-Koopmann, Felix; Huang, Elaine; Hutter, Hans-Peter; Stadelmann, Thilo; Darvishy, Alireza. MER dataset realFormula. Zenodo, v1, 2024. https://doi.org/10.5281/zenodo.11296815' -Force
    $report.dataset | Add-Member -NotePropertyName archiveSha256 -NotePropertyValue $datasetArchiveSha -Force
    $report.dataset | Add-Member -NotePropertyName manifestSha256 -NotePropertyValue $datasetManifestSha -Force
    $report | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding utf8
    $result = $report.modelResults[0]
    if ($result.model -ne "paddle-formula/$model" -or $result.sampleCount -ne 121 -or $result.results.Count -ne 121) {
        throw "Model identity/result count mismatch in $path"
    }
    $rows.Add([ordered]@{
        model = $result.model
        report = $fileName
        sampleCount = $result.sampleCount
        exactMatches = $result.exactMatches
        exactMatchRate = $result.exactMatchRate
        whitespaceTokenExactMatches = $result.whitespaceTokenExactMatches
        whitespaceTokenExactMatchRate = $result.whitespaceTokenExactMatchRate
        corpusNormalizedCharErrorRate = $result.corpusNormalizedCharErrorRate
        meanNormalizedCharErrorRate = $result.meanNormalizedCharErrorRate
        corpusWhitespaceTokenErrorRate = $result.corpusWhitespaceTokenErrorRate
        reachedEndOfSequenceCount = $result.reachedEndOfSequenceCount
        emptyPredictionCount = $result.emptyPredictionCount
        maxPredictionCharacters = ($result.results | Measure-Object -Property predictionLength -Maximum).Maximum
        modelSha256 = $result.modelSha256
        tokenizerYamlSha256 = $result.tokenizerYamlSha256
    })
}

$summary = [ordered]@{
    schemaVersion = 'deploysharp-realformula-six-model-summary-v1'
    generatedAtUtc = [DateTimeOffset]::UtcNow
    dataset = [ordered]@{
        name = 'MathNet realFormula'
        version = 'Zenodo 11296815 v1'
        doi = '10.5281/zenodo.11296815'
        url = 'https://zenodo.org/records/11296815'
        license = 'CC-BY-4.0'
        annotatedSamples = 121
        archiveSha256 = $datasetArchiveSha
        csvSha256 = $datasetCsvSha
        manifestSha256 = $datasetManifestSha
        attribution = 'Schmitt-Koopmann, Felix; Huang, Elaine; Hutter, Hans-Peter; Stadelmann, Thilo; Darvishy, Alireza. MER dataset realFormula. Zenodo, v1, 2024. https://doi.org/10.5281/zenodo.11296815'
    }
    backend = 'ONNX Runtime CPU'
    machine = [ordered]@{
        operatingSystem = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        logicalProcessorCount = [Environment]::ProcessorCount
        dotnetRuntime = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    }
    metrics = [ordered]@{
        normalizedExactMatch = 'Exact string match after removing Unicode whitespace from canonical dataset LaTeX and decoded LaTeX.'
        charCer = 'Character Levenshtein edit distance divided by reference character count after whitespace removal; diagnostic, not TeX semantic equivalence.'
        tokenError = 'Levenshtein edit distance over whitespace-separated LaTeX tokens divided by reference token count.'
    }
    models = @($rows)
    boundary = 'External 121-sample realFormula v1 set; Paddle checkpoint training-corpus overlap is unknown. ORT CPU quality only; this does not establish universal accuracy, mathematical equivalence, or quality/performance on another backend.'
}

$jsonFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $SummaryJsonPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonFull) | Out-Null
$summary | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $jsonFull -Encoding utf8

$markdownFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $SummaryMarkdownPath))
$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add('# MathNet realFormula — six Paddle formula models (ORT CPU)')
$markdown.Add('')
$markdown.Add('This report evaluates each local ONNX checkpoint on all 121 manually annotated formulas in MathNet realFormula v1. The dataset is from arXiv papers and is published on Zenodo under CC-BY-4.0. Attribution: Schmitt-Koopmann et al., *MER dataset realFormula*, 2024, [DOI](https://doi.org/10.5281/zenodo.11296815). The benchmark data remains outside this repository; only per-sample predictions, hashes, and metrics are stored here.')
$markdown.Add('')
$markdown.Add(('Pinned data: archive SHA-256 `{0}`; annotation CSV SHA-256 `{1}`; image manifest SHA-256 `{2}`.' -f $summary.dataset.archiveSha256, $summary.dataset.csvSha256, $summary.dataset.manifestSha256))
$markdown.Add('')
$markdown.Add(('Environment: `{0}`, OS arch `{1}`, process arch `{2}`, {3} logical processors, `{4}`.' -f $summary.machine.operatingSystem, $summary.machine.osArchitecture, $summary.machine.processArchitecture, $summary.machine.logicalProcessorCount, $summary.machine.dotnetRuntime))
$markdown.Add('')
$markdown.Add('| Model | Exact | Token exact | Character CER | Token error | EOS | Empty |')
$markdown.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($row in $rows) {
    $markdown.Add(('| `{0}` | {1}/121 ({2:P2}) | {3}/121 ({4:P2}) | {5:P2} | {6:P2} | {7}/121 | {8} |' -f $row.model, $row.exactMatches, $row.exactMatchRate, $row.whitespaceTokenExactMatches, $row.whitespaceTokenExactMatchRate, $row.corpusNormalizedCharErrorRate, $row.corpusWhitespaceTokenErrorRate, $row.reachedEndOfSequenceCount, $row.emptyPredictionCount))
}
$markdown.Add('')
$markdown.Add('Character CER is Levenshtein distance after removing Unicode whitespace; token error is computed over whitespace-separated LaTeX tokens. The realFormula labels are canonicalized by their dataset authors, but different LaTeX strings can encode visually equivalent mathematics. These metrics do not parse TeX or establish mathematical equivalence. All six models reached EOS on most/all examples and produced no empty outputs; this only describes decoder completion, not correctness.')
$markdown.Add('')
$markdown.Add('Per-sample JSON reports (including image/model/tokenizer hashes, reference-label hash, prediction, edit distances, EOS and warnings):')
$markdown.Add('')
foreach ($row in $rows) { $markdown.Add(('- [`{0}`]({1})' -f $row.model, $row.report)) }
$markdown.Add('')
$markdown.Add('Reproduce data preparation with `eng/models/paddle-document/scripts/Prepare-RealFormulaDataset.ps1 -Download -Extract`. Then run `PaddleDocumentFormulaVariantIntegrationTests.SixFormulaModelsEvaluateRealFormulaDatasetOnOrtCpu` once per model by setting `DEPLOYSHARP_PADDLE_REAL_FORMULA=1`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS`, and `DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH`; use `Summarize-RealFormulaEvaluation.ps1` to validate and rebuild this summary.')
$markdown.Add('')
$markdown.Add('**Limitations:** training-corpus overlap between the tested checkpoints and realFormula is unknown; do not describe this as a verified held-out split. The evaluation is ORT CPU only and uses current local artifacts. It does not establish OpenVINO, OpenCV DNN, TensorRT accuracy, cross-backend parity, speed, or general PaddleOCR formula accuracy. The poor exact-match/CER values indicate follow-up is needed on canonicalization/model suitability and long outputs; do not hide them with single-image smoke results.')
$markdown -join [Environment]::NewLine | Set-Content -LiteralPath $markdownFull -Encoding utf8
Write-Output "Wrote summary: $jsonFull"
Write-Output "Wrote summary: $markdownFull"
