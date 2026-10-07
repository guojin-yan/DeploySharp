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

The [dynamic REC batch records](verification/paddleocr-recognition-dynamic-batch-ort-openvino-20261002.md) submit ten real batches of four heterogeneous crops for both PP-OCRv5 Mobile and PP-OCRv6 Small. Public `TextCropRequest.WithTargetWidth` aligns valid crop widths for a true batch; ORT/OpenVINO keep batch=4, with zero input/shape/text mismatches for all 40 crops in each model. This does not claim multi-session pool throughput or GPU performance.

The [non-external regression gate](verification/non-external-regression-gate-20261002.md) records the current contract gate separately from model execution: Visual `488 passed/5 skipped`, Visual.OpenCV `99 passed/4 skipped`, and ModelFactory `63 passed/2 skipped`, with zero failures. External skips remain explicit and do not imply backend support.

The broader [PaddleOCR / PP-Structure regression record](verification/paddleocr-ppstructure-regression-20261005.md) records the 2026-10-07 Release snapshot across Visual, ModelFactory, ModelPack.Json, Visual.OpenCV and Visual.TensorRT: `697` passed, `135` explicit environment-gated skips and `0` failures. The machine-readable [JSON result](verification/paddleocr-ppstructure-regression-20261005.json) preserves the exact project counts, command, working-tree caveat and validation boundaries. The newly added UVDoc dynamic Batch integration is intentionally opt-in and passed separately with its gate enabled. Legacy target-framework warnings and the missing source project for the configuration-json test directory are recorded separately, so this summary is not mistaken for complete all-backend or all-asset validation.

The incremental [PP-Structure concurrent-page regression](verification/paddle-document-concurrent-page-regression-20261005.md) records the new bounded `RunManyConcurrentAsync` API at revision `b1c8803`: the focused pipeline contract is `9/9` passed and the current Visual non-external suite is `494 passed/5 skipped/0 failed`. The API preserves page order and provenance but still requires independent or thread-safe stage/session channels; it is not a true tensor Batch or a device throughput result.

Real PP-Structure dynamic Batch coverage has now expanded beyond classifiers, layout and SLANeXt to include UVDoc unwarping: ORT CPU and OpenVINO CPU each decoded two rows, preserved independent corrected-image results and page indexes, and stayed within the existing mean-drift diagnostic threshold. See the [UVDoc evidence](../paddle-document/verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md); this is not visual-quality or speed evidence. Other untested dynamic models/backends remain open in the backend matrix.

The selected v5 Mobile/Server component and pipeline goldens have a separate [ORT/OpenVINO golden parity record](verification/paddleocr-ort-openvino-golden-parity-20260928.md). It records the four passing external-model contract tests, fixed tensor/image hashes, exact recognition tokens and backend-specific scope limits; it is not a replacement for the public dataset evaluation or the unfinished OpenCV DNN/TensorRT matrix.

The [three-image core pipeline record](verification/paddleocr-core-three-image-openvino-ort-20260930.md) runs all seven local PP-OCR v4/v5/v6 DET+CLS+REC combinations on `demo_1.jpg`, `demo_2.jpg` and `demo_3.jpg` with both ORT CPU and OpenVINO CPU. All 21 combinations complete with non-empty recognition, region/recognized counts match 21/21, and the ordered per-region text SHA matches 21/21. The machine-readable report also records each input and model artifact SHA plus one-shot elapsed time. Because these images do not have word-level truth in this record, it is execution/text-parity evidence, not a CER/WER, recall or accuracy claim.

The companion [detector intermediate parity record](verification/paddleocr-core-det-intermediate-parity-20260930.md) compares the raw DB probability maps for all seven DET artifacts on the same three images. ORT CPU and OpenVINO CPU have zero input/shape mismatches across 21 combinations; the maximum element-wise absolute drift is `0.0067548752` against a `0.01` threshold. An expanded [24-page v4/v5/v6 holdout comparison](verification/paddleocr-core-detector-probability-map-parity-20261007.md) now covers all seven models and 168 model-page pairs: all passed the same `0.01` tensor-drift gate, with 11 total pixels crossing the default DB `0.3` threshold. PP-OCRv4 Server is a numeric outlier (`0.00689444` max drift) and remains flagged for follow-up. These are detector tensor-contract diagnostics, not detection quality or recall scores.

The [OpenCV DNN three-image record](verification/paddleocr-core-three-image-opencv-ort-20260930.md) now covers the same seven DET+CLS+REC combinations end to end. OpenCV and ORT both pass all 21 image/model pairs with exact region text and geometry within the documented tolerance. The record also documents the v4 tensor-name and legacy classifier shape contracts that were required for the matrix; its single-shot OpenCV timings are compatibility observations, not a 5/50 performance ranking.

For video and camera callers, `OcrPipeline.RunPrefetchedAsync` overlaps bounded next-frame input preparation with independent frame inference and returns results in source order. Callback-created inputs are owned and released by the method on success, cancellation or failure. This is frame-level pipeline overlap, not a true model batch; the full contract and example are in [visual-ocr.md](../../../docs/articles/visual-ocr.md#连续帧异步预取).

The same 24-page HierText long-text selection has now been run with PP-OCRv6 Small and SlidingWindow. The [v6 Small cross-check](verification/hiertext-v6-small-public-ocr-20260929.md) records 24/24 completed pages, detection TP/FP/FN `436/377/584`, matched CER `12.10%`, end-to-end CER `73.65%`, and the ≥128-character bucket `100/520` edits. It remains smoke-only and contains no line over 3,200 characters.

The follow-up [v6 Medium public quality record](verification/hiertext-v6-medium-public-ocr-20261002.md) uses the same 24-page selection with ORT CPU and SlidingWindow. All pages completed with zero failures or empty outputs. Detection TP/FP/FN at IoU 0.5 are `459/478/561` (F1 `46.91%`), matched-region CER/WER are `12.12%/32.58%`, and end-to-end CER/WER are `71.76%/98.42%`. Total latency P50/P95 is `1928.53/4687.44 ms` under the one-warm-up/one-measured-iteration smoke protocol. The selection has no natural line ≥3,200 characters, so it does not close A2 or establish release accuracy.

The opt-in [PP-OCRv6 Small DB threshold sensitivity record](verification/paddleocr-v6-small-db-threshold-sensitivity-20261007.md) compares seven decoder parameter choices on a disjoint 10-page tuning and 24-page holdout selection, reusing one detector probability map per page. `unclipRatio=1.30` improves holdout IoU@0.5 F1 from `0.4757` to `0.5554` on this bounded smoke selection, but does not change the library default because the result is limited to one model and two fixed subsets. The follow-up [raw DB probability-map parity record](verification/paddleocr-v6-small-db-probability-map-parity-20261007.md) measures ORT/OpenVINO drift on the same 24-page holdout: max/mean absolute difference `8.54e-5/9.70e-8`, with only 3 of 19,398,656 pixels crossing the default `0.3` probability threshold. The output tensors are not bitwise equal, but this host/sample shows near-identical threshold masks; this is not a detection-accuracy or default-tuning claim.

The [ORT/OpenVINO quality parity record](verification/hiertext-v6-medium-ort-openvino-quality-parity-20261002.md) repeats this exact selection with OpenVINO CPU. The two backends have identical page text sequences, detection counts and CER/WER; all 937 corresponding regions have identical text and polygon coordinates, with maximum confidence drift `2.57e-5`. OpenVINO total P50/P95 is `1325.58/2646.71 ms` under the same one-iteration protocol. This is exact-host parity evidence, not a general performance ranking or accuracy claim.

The [v6 Medium `sample-002` parity record](verification/hiertext-v6-medium-sample-002-ort-openvino-quality-parity-20261002.md) adds a separate ten-page selection under the same contract. ORT/OpenVINO both complete `10/10` pages with identical detection counts and CER/WER; ordered page text and region counts match `10/10`, polygon drift is `0`, and confidence drift is at most `2.185e-5`. Total P50/P95 is `1258.12/2948.36 ms` for ORT and `835.75/1900.75 ms` for OpenVINO. It remains smoke evidence: no natural line reaches 3,200 characters and the one-iteration protocol is not a formal performance ranking.

The [34-page aggregate](verification/hiertext-v6-medium-34page-ort-openvino-quality-20261002.md) combines these two disjoint selections without redistributing their source data. The aggregate has TP/FP/FN `646/722/911`, matched CER/WER `13.04%/35.63%`, end-to-end CER/WER `80.56%/105.48%`, and identical ORT/OpenVINO page text on `34/34` pages. Aggregate total P50/P95 is `1687.40/3572.89 ms` for ORT and `1131.57/2255.66 ms` for OpenVINO. It is still smoke evidence and does not close A2 or the formal performance gate.

The [34-page three-backend summary](verification/hiertext-v6-medium-34page-three-backend-quality-20261003.md) adds OpenCV DNN on the same selections. All three backends complete `34/34` pages with identical detection TP/FP/FN `646/722/911`. OpenCV matched CER/WER are `12.85%/33.91%`, end-to-end CER/WER `80.08%/104.34%`, and total P50/P95 `9646.35/24180.50 ms`; ORT/OpenVINO text remains exactly equal, while OpenCV matches ORT page text on `11/34` pages and differs in 178/1368 corresponding region texts. This is a measured backend boundary, not an importer failure or a release-accuracy claim.

The [v6 Medium OpenCV DNN quality record](verification/hiertext-v6-medium-opencv-quality-20261002.md) completes the same 24 pages with identical detection geometry and counts, but backend-specific recognition differences: 23/24 page text sequences and 174/937 corresponding region texts differ from ORT. OpenCV matched CER/WER are `11.89%/30.48%`, end-to-end CER/WER `71.14%/96.89%`, and total P50/P95 `11971.02/26997.76 ms`. This is supported execution with a measurable quality boundary, not an importer failure or a performance pass.

The matching [v6 Small ORT/OpenVINO parity record](verification/hiertext-v6-small-ort-openvino-sliding-parity-20260929.md) repeats those 24 pages with OpenVINO CPU. All page-level text sequences and quality counts match ORT; the record is CPU backend parity for this exact selection, not numeric tensor equality, GPU parity or a release accuracy score.

The [v6 Small OpenCV DNN cross-check](verification/hiertext-v6-small-opencv-public-ocr-20260929.md) also completed all 24 pages with identical detection and CER/WER counts. Ordered text sequences matched ORT on 23/24 pages, with one character difference on one page; this is close backend behavior rather than exact text parity.

The controlled [3,600-character A2 contract](verification/synthetic-longtext-a2-20260929.md) verifies the real recognizer-only SlidingWindow path on ORT/OpenVINO. The repeated-phrase and varied-vocabulary variants use 18/16 bounded windows and produce byte-identical text; the substitution-only seam fallback measures CER/WER `0.0556%/0.4556%` and `0.0833%/0.7194%`. The older `8.6389%` result is retained only as a pre-fallback historical baseline in the verification record. These are implementation-boundary samples, not quality claims or a replacement for real labeled long-text data.

An attributable long-line follow-up composes 102 real HierText text-line crops from 18 parent images into a 3,603-character bounded composition. ORT/OpenVINO both use 32 windows, retain per-window source crop/character mappings and produce identical text; CER/WER are `19.789%/50.72%`. This is not a natural continuous line, but it exercises real glyphs, source provenance and window mapping. See [hiertext-composed-long-a2-20260929.md](verification/hiertext-composed-long-a2-20260929.md).

The [v5 Mobile ORT Session-pool comparison](verification/paddleocr-v5-mobile-ort-session-pool-20260929.md) connects the concurrency contracts to a formal device run: two independently-created stage Sessions reduced total P50/P95 from `662.330/1130.299 ms` to `422.132/548.821 ms` on the same image, with an identical result contract SHA. It remains a one-device steady-state observation; long-running soak and cross-device scaling are still open.

The corresponding [OpenVINO CPU comparison](verification/paddleocr-v5-mobile-openvino-session-pool-20260929.md) shows the opposite result on the same host: two Sessions increased P50/P95 from `313.503/398.880 ms` to `371.550/527.348 ms`, while the contract stayed identical. Session-pool size must therefore be tuned per backend and device.

The [enhancement retry boundary](verification/degraded-enhancement-b2b3-20260929.md) records both mild and severe real-crop candidate runs. ORT/OpenVINO decisions match, but the available labeled crops show no stable CER gain; automatic enhancement remains disabled by default. The [2026-10-02 B1b SROIE extension](verification/synthetic-degraded-b1b-20261002.md) expands the controlled severe set to ten parent-linked crops and 60 rows: recognizer-only text is identical across ORT/OpenVINO, blur averages `8%` case-folded CER, and selected enhancement rows remain `0% -> 0%`. A separate [FUNSD 32-crop extension](verification/synthetic-degraded-funsd-b1b-20261002.md) shows a harder word-crop boundary: blur averages `97.40%` CER with 18/32 empty outputs, while confidence-selected enhancement rows worsen from `36.70%` to `37.43%`. Both are word-crop smoke evidence, not robustness or default-policy approval. The [FUNSD reading-order and layout evidence](verification/funsd-reading-order-c3-3pages-20260929.md) and [`funsd-doclayout-l-multibackend-20260929.json`](verification/funsd-doclayout-l-multibackend-20260929.json) separately show why text-line OCR ordering must not be treated as a replacement for PP-DocLayout semantics.

The new [full-page degradation reports](verification/sroie-page-degraded-b1b-20261004-ort.md) add ten annotated SROIE pages under six deterministic severe conditions (`60` full-page runs per backend). ORT and OpenVINO both complete `60/60` without empty output; the condition-level metrics are identical, and the [backend parity report](verification/sroie-page-degraded-b1b-20261004-ort-openvino-parity.md) shows `60/60` page status and region-count matches, zero text mismatches across `3169` corresponding regions, polygon max difference `0`, and confidence drift at most `4.83e-5`. Severe blur is the visible boundary (IoU0.5 F1 `79.42%`, matched CER `58.94%`, versus normal F1 `90.49%`, CER `32.04%`). The generated pages preserve public SROIE polygons but are controlled transformations, not a natural low-quality split; B1b and automatic enhancement remain open.

For investigating low IoU without changing the release metric, `Measure-HierTextGeometryCoverage.ps1` compares each labeled quadrilateral with the union of overlapping predictions. It can distinguish merged/over-expanded boxes from genuinely uncovered text, but its coverage counts are diagnostic only and must not be presented as detection recall.

The [HierText rotation coverage audit](verification/hiertext-rotation-coverage-a3-20261002.md) closes an important data question for A3: the cached Open Images metadata contains 476 nonzero-rotation rows, but none of the 1,724 cached HierText annotation IDs maps to one of them. The unmatched metadata-only images therefore cannot be scored as natural angle OCR samples; A3 still needs a legally usable natural or explicitly labeled angle dataset. The audit script records both input SHA-256 values and is safe to rerun against the same external cache.

The [natural vertical-line A3 record](verification/hiertext-vertical-a3-20261002.md) adds the 18 non-empty `vertical=true` HierText lines that do have source orientation labels. ORT/OpenVINO execute `0°`, clockwise `90°` and counter-clockwise `90°` candidates with identical text on all 18 rows; the ground-truth oracle selects 6/18 exact rows with mean CER `46.3356%`. This is useful for verifying the real vertical crop contract and candidate behavior, but the oracle selection is not an automatic classifier and A3 remains open.

Dataset images, labels, predictions and per-image reports are not copied into this repository or a model Release. This local HierText selection is marked smoke-only until each image landing page and redistribution terms have been reviewed; it must not be presented as a leaderboard result. This is quality evidence, not a performance benchmark: each page had one measured iteration, so the recorded timings do not establish P50/P95 performance.
