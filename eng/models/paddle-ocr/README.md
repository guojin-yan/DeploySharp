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

## Public annotated OCR evaluation

The checked-in runner and scorer evaluate complete-page OCR against the local public-source manifests maintained in `F:\OCRBenchmarkTesting`. The 2026-09-25 HierText CPU run covers 34 validation images with line-level labels on ONNX Runtime, OpenVINO and OpenCV DNN; metrics, SHA-256 provenance, licensing limits and exact reproduction commands are in [the v5 validation record](verification/hiertext-v5-mobile-public-ocr-20260925.md). A follow-up [v4/v6 cross-version record](verification/hiertext-core-models-public-ocr-20260928.md) adds representative v4 Mobile and v6 Tiny/Medium runs plus a geometry-coverage diagnostic; it intentionally keeps detector recall and public-data licensing limitations visible.

The complete local FUNSD test selection has a separate [v6 Tiny ORT/OpenVINO execution and parity record](verification/funsd-test-full-execution-20260928.md). FUNSD uses word-level boxes, so its strict line-detection scores are not valid model-quality measurements; that run is recorded only to show full-split runtime completion, compare the two CPU backends, and expose the annotation-granularity limitation.

The local SROIE receipt selection has a separate [v5 Mobile three-backend smoke record](verification/sroie-v5-mobile-public-ocr-smoke-20260928.md). All 10 pages completed on ORT/OpenVINO/OpenCV DNN CPU with exact region-text and coordinate parity, but SROIE annotations are word-level and this converted train subset remains smoke-only; its CER/IoU values are not official line-level accuracy results.

The crop-level contract has a separate [v5 Mobile REC ORT/OpenVINO parity record](verification/paddleocr-v5-recognition-crop-ort-openvino-parity-20260929.md). It covers 40 real SROIE word crops after OpenCV quadrilateral preparation: input tensor digests, output shapes and decoded text match, with maximum output absolute drift below `4.7e-4`. This is crop/REC backend evidence only, not a line-level accuracy or full DET recall result.

The corresponding [v5 Mobile DET intermediate parity record](verification/paddleocr-v5-detection-intermediate-ort-openvino-parity-20260929.md) compares the raw detector probability map on all 10 SROIE pages. ORT/OpenVINO inputs and `[1,1,1024,512]` output shapes match, with maximum absolute drift below `3.5e-4`; this remains backend-contract evidence, not a detection-quality score.

The same selection also has a [v5 Mobile CLS crop parity record](verification/paddleocr-v5-orientation-crop-ort-openvino-parity-20260929.md). Across 40 real crops, input tensors, class indices, rejection decisions and accepted orientations match between ORT/OpenVINO; maximum output drift is below `2.4e-6`. No direction ground truth is implied by this backend contract.

The [dynamic REC batch record](verification/paddleocr-v5-recognition-dynamic-batch-ort-openvino-20260929.md) submits ten real batches of four heterogeneous crops. Public `TextCropRequest.WithTargetWidth` now lets callers align valid crop widths for a true batch; ORT/OpenVINO keep batch=4 and decode all 40 texts identically. This does not yet claim multi-session pool throughput or GPU performance.

The selected v5 Mobile/Server component and pipeline goldens have a separate [ORT/OpenVINO golden parity record](verification/paddleocr-ort-openvino-golden-parity-20260928.md). It records the four passing external-model contract tests, fixed tensor/image hashes, exact recognition tokens and backend-specific scope limits; it is not a replacement for the public dataset evaluation or the unfinished OpenCV DNN/TensorRT matrix.

The same 24-page HierText long-text selection has now been run with PP-OCRv6 Small and SlidingWindow. The [v6 Small cross-check](verification/hiertext-v6-small-public-ocr-20260929.md) records 24/24 completed pages, detection TP/FP/FN `436/377/584`, matched CER `12.10%`, end-to-end CER `73.65%`, and the ≥128-character bucket `100/520` edits. It remains smoke-only and contains no line over 3,200 characters.

The matching [v6 Small ORT/OpenVINO parity record](verification/hiertext-v6-small-ort-openvino-sliding-parity-20260929.md) repeats those 24 pages with OpenVINO CPU. All page-level text sequences and quality counts match ORT; the record is CPU backend parity for this exact selection, not numeric tensor equality, GPU parity or a release accuracy score.

The [v6 Small OpenCV DNN cross-check](verification/hiertext-v6-small-opencv-public-ocr-20260929.md) also completed all 24 pages with identical detection and CER/WER counts. Ordered text sequences matched ORT on 23/24 pages, with one character difference on one page; this is close backend behavior rather than exact text parity.

The controlled [3,600-character A2 contract](verification/synthetic-longtext-a2-20260929.md) verifies the real recognizer-only SlidingWindow path on ORT/OpenVINO. The repeated-phrase and varied-vocabulary variants use 18/16 bounded windows and produce byte-identical text; the substitution-only seam fallback measures CER/WER `0.0556%/0.4556%` and `0.0833%/0.7194%`. The older `8.6389%` result is retained only as a pre-fallback historical baseline in the verification record. These are implementation-boundary samples, not quality claims or a replacement for real labeled long-text data.

An attributable long-line follow-up composes 102 real HierText text-line crops from 18 parent images into a 3,603-character bounded composition. ORT/OpenVINO both use 32 windows, retain per-window source crop/character mappings and produce identical text; CER/WER are `19.789%/50.72%`. This is not a natural continuous line, but it exercises real glyphs, source provenance and window mapping. See [hiertext-composed-long-a2-20260929.md](verification/hiertext-composed-long-a2-20260929.md).

The [v5 Mobile ORT Session-pool comparison](verification/paddleocr-v5-mobile-ort-session-pool-20260929.md) connects the concurrency contracts to a formal device run: two independently-created stage Sessions reduced total P50/P95 from `662.330/1130.299 ms` to `422.132/548.821 ms` on the same image, with an identical result contract SHA. It remains a one-device steady-state observation; long-running soak and cross-device scaling are still open.

The corresponding [OpenVINO CPU comparison](verification/paddleocr-v5-mobile-openvino-session-pool-20260929.md) shows the opposite result on the same host: two Sessions increased P50/P95 from `313.503/398.880 ms` to `371.550/527.348 ms`, while the contract stayed identical. Session-pool size must therefore be tuned per backend and device.

For investigating low IoU without changing the release metric, `Measure-HierTextGeometryCoverage.ps1` compares each labeled quadrilateral with the union of overlapping predictions. It can distinguish merged/over-expanded boxes from genuinely uncovered text, but its coverage counts are diagnostic only and must not be presented as detection recall.

Dataset images, labels, predictions and per-image reports are not copied into this repository or a model Release. This local HierText selection is marked smoke-only until each image landing page and redistribution terms have been reviewed; it must not be presented as a leaderboard result. This is quality evidence, not a performance benchmark: each page had one measured iteration, so the recorded timings do not establish P50/P95 performance.
