# PaddleOCR model acquisition and release

This is the reproducible acquisition entry point for the PaddleOCR collection used by DeploySharp. It covers the v4/v5 detector, recognizer and classifier contracts, the v6 tiny/small/medium detector and recognizer models, and the PP-Structure document pipeline models (orientation, unwarping, layout, table, formula and seal tasks). PaddleOCR v6 does not publish a separate versioned classifier; the official PP-LCNet text-line orientation models are shared by the current pipeline and are listed as the v5 classifier entries. PP-Structure acquisition is maintained in [`eng/models/paddle-document`](../paddle-document).

The model files remain outside Git. The checked-in catalog records the official archive URL, expected tensor names, dictionary and the DeploySharp profile factory. `Acquire-PaddleOcrModels.ps1` downloads the official Paddle inference archives, extracts and converts missing graphs with `paddle2onnx`, and writes a source/ONNX/dictionary SHA-256 record for each entry.

To combine complete-pipeline benchmark CSV files from one or more devices into a stable Markdown matrix, use `Export-PaddleOcrBenchmarkMatrix.ps1`. It copies mean/P50/P95 and stage columns from the raw CSV and includes every adjacent `.environment.json` record without inventing missing percentiles:

```powershell
pwsh -NoProfile -File .\scripts\Export-PaddleOcrBenchmarkMatrix.ps1 `
  -ReportPath artifacts/local-model-benchmarks/paddleocr-v5-mobile-3backend-demo1-20260924.csv `
  -OutputPath artifacts/local-model-benchmarks/paddleocr-matrix.md
```

Run the benchmark with `DEPLOYSHARP_BENCHMARK_SOURCE_REVISION` set to the commit or dirty revision you are measuring. GPU clock, power and slowdown fields come from `Invoke-WithGpuTelemetry.ps1` and are kept separate from the pipeline CSV.

```powershell
& .\scripts\Acquire-PaddleOcrModels.ps1 `
  -All `
  -ModelRoot E:\Model\paddleocr `
  -Python E:\Model\PaddleDocument\paddle3\python.exe
```

The existing v4 mobile, v5 mobile and v6 ONNX files are reused when present; source archives are still recorded so the provenance is reproducible. A `conversion-blocked` record is never treated as runtime support. The v6 recognition graphs use the same `CreateRecognition` CTC contract as v4/v5, with their tier-specific dictionaries and class counts.

## Code support versus asset status

All 17 core catalog rows have a corresponding DeploySharp profile factory: `CreateDetection`, `CreateRecognition`, `CreateLegacyClassification`, or `CreateTextLineOrientationClassification`. The PP-Structure catalog has separate document profiles and decoders under `src/DeploySharp.Visual/Models/PaddleOcr/Document`. This is code-level contract support. It does not claim that every row has been run on every backend. The acquisition summaries separately record `onnx-existing`, `onnx-converted`, `download-failed`, or `conversion-blocked`.

The independent release publisher maintains one stable release named **DeploySharp PaddleOCR and PP-Structure Models** with tag `models-paddleocr`. The tag is intentionally not date-based; later uploads are recorded in the release notes and asset manifest. Every core PP-OCR and converted PP-Structure ONNX file is an individual asset, so users can download only the detector, recognizer, classifier, layout, table or formula model they need. Release assets also contain dictionaries, acquisition summaries, the combined catalog and `SHA256SUMS`; Paddle source archives stay in the acquisition cache unless explicitly requested. Chart2Table's original source archive is not a standard Paddle inference export and remains `conversion-blocked` in the source-archive acquisition catalog; its derived four-graph ONNX + tokenizer bundle is separately published under `paddle-chart/pp-chart2table` in the same Release. The unavailable legacy `PP-FormulaNet-M` archive is not the same model ID as the published `PP-FormulaNet-Plus-M` artifact.

## Runtime download and deployment

`JYPPX.DeploySharp.ModelFactory` now includes `PaddleOcrReleaseClient`. It reads `paddleocr-release-catalog.json`, downloads only the selected ONNX asset (and its recognition dictionary when required), verifies the catalog size/SHA-256, and stores the result in an owned cache. The client accepts both the Release IDs and the code-facing aliases such as `paddleocr/ppocrv5/mobile-rec` and `paddle-doc/pp-doclayout-s`:

```csharp
using System.IO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

using var release = new PaddleOcrReleaseClient(
    new PaddleOcrReleaseClientOptions(Path.Combine("models", "cache")));
PaddleOcrReleaseModelMaterialization model =
    await release.GetModelAsync("paddleocr/ppocrv5/mobile-rec");

// Bind the exact downloaded path to the profile used by the backend.
PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(
    new JYPPX.DeploySharp.Models.ModelId("paddleocr/ppocrv5/mobile-rec"));
var contract = new PaddleOcrArtifactContract(
    model.Model.Opset ?? throw new InvalidOperationException("The Release row has no opset."),
    model.Model.Sha256,
    upstreamCommit: "models-paddleocr-release",
    exporterVersion: "paddle2onnx-release-artifact",
    license: "verify-model-source-license",
    preprocessingVersion: "ppocr-official-inference-v1",
    postprocessingVersion: "deploysharp-paddleocr-db-ctc-v1",
    dictionarySha256: null,
    dictionaryLicense: "verify-model-source-license");
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    model.DictionaryPath!, "ppocrv5", "v5", useSpaceCharacter: true);
PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(
    descriptor, contract, characters, maximumBatch: 16);
// Bind profile.CreateArtifact(model.ModelPath, preferredBackend) to a
// VisualProfileRegistry and let the selected Backend create its Session.
```

For PP-Structure, call `GetModelAsync` with the corresponding `paddle-doc/`, `paddle-table/`, `paddle-formula/`, or `paddle-seal/` ID, then pass `model.ModelPath` to the matching `PaddleDocumentProfiles` profile. The Release client does not silently convert Paddle archives and does not claim backend support; backend selection and execution remain the responsibility of the Visual/Backend packages.
