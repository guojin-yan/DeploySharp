# Paddle document model acquisition

This directory is the reproducible acquisition entry point for PP-StructureV3 document models. The checked-in manifest contains official Paddle inference archive URLs; the model bytes remain outside the repository.

```powershell
# Convert one small classifier first.
& .\scripts\Acquire-PaddleDocumentModels.ps1 `
  -ModelId pp-lcnet-x1-0-doc-ori `
  -ModelRoot E:\Model\PaddleDocument `
  -Python C:\Users\guoji\.conda\envs\PaddleOCR\python.exe

# After validating the toolchain, acquire all catalogued modules.
& .\scripts\Acquire-PaddleDocumentModels.ps1 `
  -All `
  -ModelRoot E:\Model\PaddleDocument `
  -Python C:\Users\guoji\.conda\envs\PaddleOCR\python.exe
```

The converter requires a Python environment with a PaddlePaddle build compatible with the installed `paddle2onnx`. `paddle2onnx` 2.x uses `python -m paddle2onnx.command`; the script handles that entry point and adds the Paddle DLL directories to the child process search path. Every source archive and converted ONNX file receives a size/SHA-256 record. A failed conversion is retained as `conversion-blocked` and must not be advertised as a runtime-supported model.

The current exact artifact/backend status is summarized in [paddle-document-status-summary-20260929.md](verification/paddle-document-status-summary-20260929.md) and the authoritative machine-readable [backend matrix](verification/paddle-document-backend-matrix-20260929.json). The 14-artifact OpenCV DNN Paddle NMS execution details, including the eight OpenCV 5.0 `MatMul` importer blockers, are retained in [paddle-document-opencv-nms-matrix-20260930.md](verification/paddle-document-opencv-nms-matrix-20260930.md). TensorRT 11 evidence for both RT-DETR table-cell artifacts is retained in [paddle-document-tensorrt-table-cell-matrix-20260930.md](verification/paddle-document-tensorrt-table-cell-matrix-20260930.md). A current-machine TensorRT 10.11 PP-LCNet orientation run is recorded in [paddle-document-tensorrt-orientation-20261004.md](verification/paddle-document-tensorrt-orientation-20261004.md); it is intentionally separate from the TRT 11 records because bridge/API lines are not interchangeable.

The same current-machine runtime also has a separate [TensorRT 10.11 table-classification record](verification/paddle-document-tensorrt-table-classification-20261004.md): one `wired_table` sample passed with P50/P95 `0.9974/1.1903 ms` and ORT/TensorRT score difference `0.0074998` within the `0.01` contract tolerance. This is a model-contract result, not table-structure accuracy or full-matrix evidence.

The FUNSD layout evidence test keeps the original three-page default and accepts `DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT_MAX_PAGES=50` for a larger local execution selection. The 2026-10-04 50-page run completed `50/50` pages on both ONNX Runtime CPU and OpenVINO CPU, with `1332` regions per backend, identical page detection counts and labels, and maximum coordinate/size/score drift of approximately `4.6e-4`. Only `5/50` pages were emitted in top-left model order, so a reading-order policy is still required. FUNSD does not provide aligned PP-DocLayout region labels; this is execution and numeric parity evidence, not layout accuracy. See [the 50-page report](verification/funsd-doclayout-l-50page-multibackend-20261004.md) and its [ONNX Runtime JSON](verification/funsd-doclayout-l-50page-onnxruntime-20261004.json) / [OpenVINO JSON](verification/funsd-doclayout-l-50page-openvino-20261004.json).

The official PP-Structure models are Paddle Inference artifacts. They are converted and distributed individually in the stable [`models-paddleocr`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) Release alongside the core PP-OCR v4/v5/v6 assets; users do not need to download a full bundle. A converted model is admitted to DeploySharp only after its exact input/output names, preprocessing, decoder, backend execution and result parity are recorded.

The checked-in model-release catalog keeps `pp-chart2table` as `conversion-blocked` only for a **single downloadable inference artifact**: the official archive is generative source weights, not `inference.json`/`inference.pdiparams`. Its separate four-graph ONNX generation bundle and three tokenizer assets are published in the [`models-paddleocr` Release](https://github.com/guojin-yan/DeploySharp/releases/tag=models-paddleocr) under bundle ID `paddle-chart/pp-chart2table`. Retrieve only those seven files through `PaddleOcrReleaseClient.GetBundleAsync`; the source-only row remains rejected by `PaddleDocumentProfile.CreateArtifact`, while consumers use `PaddleChart2TableOnnxBundle` and a backend-specific session.

### Real ONNX batch-axis audit

The external model root was audited on 2026-10-05 with [`Audit-PaddleDocumentBatchShapes.py`](scripts/Audit-PaddleDocumentBatchShapes.py). The report parsed 53 of 54 discovered ONNX files; the remaining file is an intentionally retained zero-byte `chart-text-prefill-fixed-distprim.onnx` export and is recorded as `load-failed`, not silently treated as a model. Thirty-two graphs expose a dynamic first input axis and eight standard PP-Structure graphs are static `batch=1`. The dynamic group includes PP-DocLayout-L/Plus-L, PP-DocBlockLayout, RT-DETR layout/cell models, FormulaNet/UniMERNet, PP-LCNet classifiers, seal detection, SLANeXt and UVDoc. PicoDet and PP-DocLayout-M/S remain static. Chart2Table vision, embedding, prefill and decode graphs keep request batch `1`; their sequence/KV axes can be dynamic, but generation state is per image and must not be advertised as model Batch.

The machine-readable [batch-axis JSON](verification/paddle-document-onnx-batch-axis-audit-20261005.json) and [Markdown report](verification/paddle-document-onnx-batch-axis-audit-20261005.md) record every input/output shape and SHA-256. A dynamic graph axis is only a prerequisite for a true Batch attempt: each backend still needs a real input binding, decoder, memory and parity run. Static exports should use independent Sessions or the page-concurrency scheduler.

The first real dynamic Batch evidence covers the two official PP-LCNet classifiers. ORT CPU and OpenVINO CPU both ran two identical `bus.jpg` rows through `[2,3,224,224]` and returned two ordered classification results with identical labels and scores. See [the execution report](verification/paddle-document-dynamic-batch-ort-openvino-20261005.md) and its [JSON](verification/paddle-document-dynamic-batch-ort-openvino-20261005.json). This does not extend to static layout exports or Chart2Table generation.

The follow-up [PP-DocLayout-L dynamic Batch report](verification/paddle-document-layout-dynamic-batch-ort-openvino-20261005.md) covers the official dynamic Paddle NMS layout graph on the same host. ORT CPU and OpenVINO CPU both bound two identical rows with `im_shape`/`scale_factor` auxiliary tensors, partitioned the flattened output with `bbox_num`, and returned `300/300` detections with identical row results. This is graph/decoder execution evidence only; the 300 rows are the export's post-NMS candidate count, not layout ground truth.

The [three-backend distinct-row follow-up](verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.md) then tested two different `bus.jpg` regions at batch=2. ORT CPU and OpenVINO CPU each completed the full decoder with 300 candidates per row and distinct input/result digests. OpenCV DNN session creation succeeded, but forward returned `DS-OCV-8004` / `Requested blob not found`; only this exact dynamic-Batch combination is marked unsupported, without changing the existing single-image OpenCV evidence. This is not an accuracy, numerical-parity or throughput result; TensorRT Batch remains unverified.

The cross-architecture [RT-DETR-H layout report](verification/paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.md) applies the same protocol to `rt-detr-h-layout-3cls.onnx`. Both ORT CPU and OpenVINO CPU passed two-row execution, auxiliary binding and flattened-output partitioning with `300/300` identical candidates. This remains a single-model, single-host execution contract rather than a quality or performance claim.

The same true-Batch contract now covers [wired SLANeXt table decoding](verification/paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md). ORT CPU ran the official `slanext-wired.onnx`; OpenVINO CPU ran the separately hashed alpha-renamed compatibility graph because the original Loop graph remains importer-blocked. Both bound `[2,3,512,512]`, returned two rows with `24` tokens and `13` cells each, and produced identical HTML markup hashes. This proves dynamic input binding and decoder row isolation for the two exact artifacts only; it is not table accuracy, throughput, or wireless/TensorRT/OpenCV evidence.

The complete single-page [table task pipeline](verification/paddle-document-table-pipeline-ort-openvino-20261011.md) now has a real ORT/OpenVINO execution record: table classification → wired cell detection → SLANeXt HTML. Both backends returned `wired_table`, `300` threshold-zero detector candidates, `13` decoded structure cells, `24` structure tokens and HTML length `165`, with no decoder warnings; total observations were `2001.241 ms` (ORT CPU) and `2127.139 ms` (OpenVINO CPU). OpenVINO uses the independently hashed compatibility graph. This is a one-image execution/decoder contract, not cell recall, table accuracy or a P50/P95 benchmark; OpenCV DNN and TensorRT remain separate status cells.

The [wireless SLANeXt dynamic Batch report](verification/paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md) now verifies the companion model under the same protocol. ORT CPU uses the official wireless ONNX and OpenVINO CPU uses its separately hashed Loop-compatible graph; both returned two rows with 24 tokens, 13 cells and matching markup hashes. Both SLANeXt variants therefore have real two-row decoder evidence on these two CPU backends, while the original OpenVINO graphs remain importer-blocked and accuracy/performance claims remain out of scope.

The [UVDoc dynamic Batch report](verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md) adds the official unwarping model to this set. ORT CPU and OpenVINO CPU each decoded the left and right non-overlapping `bus.jpg` regions from `[2,3,640,640]` into independent `640×640×3` results with page indexes `[0,1]`; within-backend row mean difference was `72.4329`. Cross-backend row mean differences were `0.002078` and `0.002263` (combined mean/max `0.002171/0.103020`). This is bounded numeric parity, not pixel equivalence or visual-quality evidence. The known OpenCV DNN importer failure and unverified TensorRT status are unchanged.

The [RT-DETR-L table-cell dynamic Batch report](verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.md) now covers both wired and wireless exports. ORT CPU and OpenVINO CPU each ran `[2,3,640,640]` with `im_shape=[2,2]` and `scale_factor=[2,2]`; the batch rows are distinct top/bottom `551×66` regions from `table_recognition.jpg`. All four model/backend combinations returned two rows with 300 exported candidates per row, and each pair has different prepared-input and decoded-result SHA-256 digests. The candidate count is not ground truth, and this run makes no accuracy, cross-backend parity or throughput claim. Exact model/input hashes and the machine-readable result are in the linked report.

The same two table-cell models were then attempted with batch=2 on OpenCV DNN CPU. Both Sessions were created, but `forward_many` returned `DS-OCV-8004 / Requested blob not found`; this exact OpenCV dynamic-batch contract is unsupported for both wired and wireless exports. Do not generalize the result to their existing single-image OpenCV execution or to ORT/OpenVINO, which remain verified for batch=2. No importer investigation was performed. See the [OpenCV boundary report](verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.md) and [JSON](verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.json).

The [seal-detector dynamic Batch report](verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md) extends the same check to official PP-OCRv4 mobile/server exports on ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU. All six exact model/backend combinations bound two distinct source images as `[2,3,224,224]`, returned `[2,1,224,224]` masks and preserved row/page/source mapping through the seal decoder. Backend mask digests differ, so the evidence is execution and row isolation only—not numeric parity, seal quality or performance. The inputs have no seal labels. Full per-row hashes and output shapes are in the [JSON report](verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json).

The [PP-LCNet classifier distinct-row Batch report](verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.md) additionally runs document orientation and table classification on two different regions through ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU. All six model/backend combinations bind `[2,3,224,224]`, produce distinct input and raw-logit rows, and return two decoded results. Raw outputs are compared per row to ORT with an enforced max-absolute-error tolerance of `1e-4`; observed maxima are below `1.2e-7`. Hashes differ because outputs are not bit-identical. The regions are execution probes, not labeled quality samples, so this is not an accuracy result.

Dynamic Batch row-isolation evidence now covers all six official formula exports on ORT CPU: PP-FormulaNet Plus-S/M/L, FormulaNet-S/L and UniMERNet. Each uses two distinct top/bottom bands from the official formula image; both rows reached EOS and token IDs/LaTeX exactly match their independent batch-one runs. The runs verify dynamic binding and decoder row isolation—not formula accuracy or throughput—and do not imply OpenVINO/OpenCV/TensorRT support. Shapes, per-model token counts, reproduction command and exact model/input/tokenizer SHA-256 values are in the [six-model report](verification/formula-dynamic-batch-six-models-ort-20261007.md) and its six linked JSON records.

The same six formula graphs were attempted on OpenCV DNN CPU with dynamic batch=2. All six failed before inference with `DS-OCV-8002` during OpenCV 5.0 ONNX import/input specialization: five hit a `ConstantOfShape` zero-dimension error, and UniMERNet hit a `GatherND` importer error. The exact model/input hashes and native messages are in the [OpenCV dynamic Batch report](verification/paddle-document-formula-dynamic-batch-opencv-20261007.md) and [JSON](verification/paddle-document-formula-dynamic-batch-opencv-20261007.json). This records only these artifacts on this adapter/runtime path; it does not change ORT batch support, classify batch=1, or justify deeper importer investigation.

The official RT-DETR-H 17-class layout export is verified at batch=2 on ORT CPU and OpenVINO CPU using distinct regions, both `[2,2]` geometry inputs and the complete NMS decoder; both backends returned two rows of 300 candidates. OpenCV DNN created its session but failed on forward with `DS-OCV-8004` / `Requested blob not found`, so dynamic batch is unsupported for this tested OpenCV combination; this does not overturn existing single-batch OpenCV evidence. This is execution/row-isolation evidence only, not a labeled layout-quality result or performance benchmark. See the [17-class dynamic Batch report](verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-20261007.md) and [JSON](verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json).

The same OpenCV DNN dynamic-batch attempt was extended to official `PP-DocLayout_plus-L` and `PP-DocBlockLayout`. Both session creations succeeded, but `forward_many` returned `DS-OCV-8004 / Requested blob not found`; only these exact batch=2 combinations are marked unsupported, and this does not overturn their existing single-image OpenCV evidence or their successful ORT/OpenVINO batch results. No importer investigation was performed. See the [OpenCV dynamic Batch report](verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.md) and [JSON](verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.json).

### Dynamic Batch external regression snapshot (2026-10-11)

The complete `PaddleDocumentDynamicBatchIntegrationTests` run was repeated with the external model gates enabled: **22/22 test cases passed, 0 failed, 0 skipped**. At the exact model/backend level this is **59 checks: 46 executed successfully and 13 explicitly unsupported**, all 13 being OpenCV DNN dynamic-Batch boundaries. ORT CPU accounts for 24 successful checks, OpenVINO CPU for 18, and OpenCV DNN CPU for 4 successful plus 13 unsupported checks. TensorRT was not run in this snapshot and is not counted as supported. The aggregate report lists all 22 source JSON files, model IDs, backend coverage and the precise boundary wording: [Markdown](verification/paddle-document-dynamic-batch-external-regression-20261011.md) / [JSON](verification/paddle-document-dynamic-batch-external-regression-20261011.json). This is execution/row-isolation evidence only; it is not an accuracy, throughput, P50/P95, cross-device or TensorRT claim.

The companion official RT-DETR-H **3-class** layout export has now been tested with two different `bus.jpg` regions through batch=2 on the same three CPU backends. ORT and OpenVINO each completed the full decoder with 300 candidates per row and distinct input/result rows; OpenCV DNN session creation succeeded, but `forward_many` returned `DS-OCV-8004 / Requested blob not found`. This marks only the exact dynamic Batch=2 OpenCV combination unsupported; the existing single-image OpenCV status is unchanged. The regions are not labeled, so this is not a layout-quality or performance result. See the [3-class dynamic Batch report](verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.md) and [JSON](verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json).

A TensorRT vendor-tool probe established that this table-classifier ONNX graph can build and execute a dynamic batch=2 TensorRT engine. It did not pass through DeploySharp: the current managed TensorRT bridge stopped during runtime creation (TRT11 external engine) or builder creation (TRT10.11 baseline retry) with structured exception `3228369022 (0xC06D007E)`. The managed batch=2 parity test remains unverified and the backend matrix is unchanged. Vendor `trtexec` GPU-only latency is explicitly not a DeploySharp benchmark; see the [probe report](verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md) and [JSON](verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.json).

## Current local evidence

The real two-page [bounded concurrency record](verification/paddle-document-multipage-concurrent-ort-20261005.md) exercises orientation -> layout on ORT CPU with the same `bus.jpg` input duplicated as page 0/1. `RunManyAsync` measured `882.267 ms` wall time, while `RunManyConcurrentAsync(maxDegreeOfParallelism: 2)` measured `791.9943 ms`; both pages preserved source SHA, page order and 300 layout regions. The follow-up [5-warm-up/50-measurement benchmark](verification/paddle-document-multipage-concurrent-ort-benchmark-20261005.md) reports ORT CPU P50/P95 of `841.0031/918.2798 ms` sequential and `708.4469/873.7787 ms` concurrent on the same host. A separate [OpenVINO CPU two-page record](verification/paddle-document-multipage-concurrent-openvino-20261005.md) preserves both pages and 300 regions, with one-run walls of `898.9205/644.0613 ms`; the [OpenVINO 5/50 benchmark](verification/paddle-document-multipage-concurrent-openvino-benchmark-20261005.md) reports P50/P95 `563.6314/647.9142 ms` sequential and `631.2580/664.9269 ms` concurrent, so page concurrency was slower on this host. The [four-configuration tuning report](verification/paddle-document-multipage-openvino-concurrency-tuning-20261005.md) keeps the same protocol while varying Session/page concurrency and identifies `session=1/page=2` as a candidate only. A new [TensorRT CUDA 5/50 record](verification/paddle-document-tensorrt-multipage-benchmark-20261005.md) reports P50/P95 `103.2937/109.6768 ms` sequential and `66.7278/69.0992 ms` concurrent with the exact built Engines and `session=2/page=2`. These are one-host execution observations, not a tensor Batch result, quality score or general speedup promise; different GPU/runtime combinations require fresh Engine builds and measurements.

For task-specific regression inputs, use `scripts/Acquire-PaddleDocumentValidation.ps1`. It downloads and verifies the SHA-256 of the official direction, table and formula examples. `scripts/Generate-FormulaReference.py` executes only the unchanged preprocessing classes from a pinned PaddleX revision to generate Float32 reference tensors. Requirements: NumPy, Pillow, opencv-python-headless. Use `--processor-source` for a locally downloaded copy if Python's network access is unavailable; the same source hash is required.

### Chart2Table four-graph bundle and runtime evidence

`Export-PaddleChartVision.py` exports the vision encoder and projector from the legacy BF16 checkpoint. The text, embedding and end-to-end probes used the same already-acquired checkpoint and pinned PaddleX source; they did not download the official package again. The result is an executable four-graph image-to-text bundle, published with the official tokenizer sidecars as a seven-asset bundle in the existing `models-paddleocr` Release.

```powershell
# 复用已经取得的 pinned wheel；验证过程不会再次下载官方包。
$wheel = 'E:\Model\PaddleDocument\chart-export-deps\paddlex-3.7.2-py3-none-any.whl'
if (-not (Test-Path -LiteralPath $wheel)) { throw "Missing existing pinned wheel: $wheel" }
python scripts/Export-PaddleChartVision.py `
  --wheel $wheel `
  --checkpoint E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table\model_state.pdparams `
  --image E:\Model\PaddleDocument\validation\chart_parsing_02.png `
  --output E:\Model\PaddleDocument\chart-export
```

Obtain `chart_parsing_02.png` from the [official chart tutorial](https://github.com/PaddlePaddle/PaddleX/blob/c50f5da858020db473a2285f089bb8c7bbd6afdc/docs/module_usage/tutorials/vlm_modules/chart_parsing.en.md). The exporter validates every vision weight name/shape, converts BF16 weights to FP32, exports opset 17, checks ONNX and compares ORT CPU features against Paddle on the same input. The rerun on 2026-09-23 mapped 181 tensors, emitted `[1,256,1024]`, and had maximum absolute error `0.0001490414`; checkpoint SHA-256 is `84a6990436ddd55e03acdb44048ff6484aa53619f41412eda2e277df79ea2de6`, image SHA-256 is `d29ea952a5a182c18416dcf2bf17b606ead7ebf20100618ece87f6f288f856c3`, and the component ONNX SHA-256 is `dea9eb175e46bb77b0c93dae08730530cd075500e6d514b15e0bc0cdc6b9a88d`. Original weights remain unchanged.

The text side was checked from the same 472 mapped tensors. The production bundle contains four opset-17 ONNX graphs: vision/projector, token embedding, fixed 286-token prompt prefill with `lm_head`, and one-token dynamic-past decode with `lm_head`. The official Qwen `qwen.tiktoken` plus `tokenizer_config.json` and `added_tokens.json` produces the PaddleX prompt exactly: 286 token IDs and 256 contiguous image placeholders.

To regenerate the three text graphs from the already acquired source directory, use the same Paddle/Paddle2ONNX environment and the pinned wheel:

```powershell
& E:\Model\PaddleDocument\paddle3\python.exe `
  .\scripts\Export-PaddleChart2TableTextGraphs.py `
  --model-root E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table `
  --paddlex-wheel E:\Model\PaddleDocument\chart-export-deps\paddlex-3.7.2-py3-none-any.whl `
  --output-root E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923
```

Run `Export-PaddleChartVision.py` alongside it to regenerate the first graph. The exporter checks the pinned PaddleX source, every mapped checkpoint key/shape, ONNX schema, opset, graph size and SHA-256. It does not download the checkpoint or tokenizer.

ORT CPU executed image → feature merge → prefill → two dynamic KV decode steps on `chart_parsing_02.png`. It returned `[7948, 69442, 760]` (`年份 |`) and KV lengths 286, 287, 288; maximum per-step Paddle/ORT logit errors were `4.29e-5`, `4.82e-5`, and `6.77e-5`. OpenVINO CPU and TensorRT CUDA returned the same token IDs. Exact graph hashes, tensor contracts and stage timings are in [chart2table-component-validation.json](verification/chart2table-component-validation.json).

The .NET sessions were run to completion with a 256-token limit on the same official image. All backends emitted 141 tokens including EOS (`151645`) and returned the same complete six-row 2018–2023 table. ORT CPU took 39.76 s and OpenVINO CPU 46.40 s. The original TensorRT host-KV route took 83.42 s; after geometric buffer reuse, device-resident KV ping-pong, removing redundant binding/shape work, and enabling TensorRT TF32 tactics on the FP32 language plans, the TensorRT device path's best observed run was 10.12 s (other runs: 12.12/12.64 s). Its best run's 140 decode steps measured P50/P95 66.47/73.48 ms, versus 94.69/123.92 ms for ORT CPU. The output text remained byte-identical. No GPU clock lock was applied and no sustained clock trace was captured; report all three TensorRT runs as same-device sample observations, not a controlled benchmark or dataset accuracy claim.

For the accelerated TensorRT route, use `PaddleChart2TableTensorRtDeviceSession` from `DeploySharp.Visual.TensorRT`; unlike the backend-neutral `PaddleChart2TableOnnxSession`, it keeps the 48 KV buffers in CUDA memory and returns logits to the host for greedy selection. Strict FP32 plans remain available. On the RTX 3060 sample, optional TF32 tactics also returned the exact same table; this is still a single-sample numerical check, not a dataset-wide precision qualification.

To build optional FP32/TF32 text plans without rebuilding the existing Vision/Embedding plans:

```powershell
& .\scripts\Build-PaddleChart2TableTensorRtPlans.ps1 `
  -OnnxRoot E:\Model\PaddleDocument\chart-export `
  -TextGraphRoot E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923 `
  -TensorRtRoot 'D:\Program Files\TensorRT-10.11.0.33-cu12' `
  -CudaRoot 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' `
  -CudnnRoot 'D:\Program Files\cuDNN-9.22.0-cuda12.9' `
  -OutputRoot D:\Model\PaddleDocument\chart2table-tensorrt-tf32-verify-20260923 `
  -TextOnly -EnableTextTf32
```

The external test accepts `DEPLOYSHARP_CHART2TABLE_TRT_PREFILL_PLAN` and `DEPLOYSHARP_CHART2TABLE_TRT_DECODE_PLAN` to select those plans; leave both unset to use the strict-FP32 plans under `DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT`.

The release catalog lists role, filename, size, format, and SHA-256 for each of the four ONNX graphs and three official tokenizer files. Applications can request only this bundle:

```csharp
using var release = new PaddleOcrReleaseClient(
    new PaddleOcrReleaseClientOptions(Path.Combine("models", "cache")));
PaddleOcrReleaseBundleMaterialization downloaded = await release.GetBundleAsync(
    "paddle-chart/pp-chart2table");

var bundle = new PaddleChart2TableOnnxBundle(
    downloaded.CreateOnnxArtifact("vision-projector"),
    downloaded.CreateOnnxArtifact("token-embedding"),
    downloaded.CreateOnnxArtifact("text-prefill"),
    downloaded.CreateOnnxArtifact("text-decode-with-past"));
var tokenizer = new PaddleChart2TableTokenizer(downloaded.TokenizerDirectoryPath);
```

`GetBundleAsync` downloads and verifies the seven required files independently; no unrelated PP-OCR or PP-Structure model is required.

The external integration tests default to three tokens. To reproduce full generation, set the corresponding backend gate (`DEPLOYSHARP_CHART2TABLE_ORT_RUN_EXTERNAL`, `DEPLOYSHARP_CHART2TABLE_OPENVINO_RUN_EXTERNAL`, or `DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL`) to `1`, set its matching `*_MAX_NEW_TOKENS` variable to `256`, and provide `DEPLOYSHARP_CHART2TABLE_MODEL_ROOT`, `DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT`, and `DEPLOYSHARP_CHART2TABLE_TEXT_ROOT`. Then run the matching `PaddleChart2Table*ExternalIntegrationTests` filter under `tests/DeploySharp.Visual.OpenCV.Tests` (ORT/OpenVINO) or `tests/DeploySharp.Visual.TensorRT.Tests` (TensorRT). TensorRT additionally needs its runtime/bridge paths configured and `DEPLOYSHARP_CHART2TABLE_TRT_USE_EXISTING_PLANS=1`; choose either strict-FP32 plans or explicitly set both TF32 plan override variables.

The extra TensorRT task check uses four image files and their rows from the official [ChartQA `test_human` split](https://github.com/vis-nlp/ChartQA): `png_41699051005347.png`, `png_41810321001157.png`, `png_8127.png`, and `png_166.png`. The dataset repository declares GPL-3.0; these inputs are deliberately not included in this repository or in the model Release. Download them and `test_human.json` from the dataset's official distribution into an external directory, then point the test at that directory. This test requires the four TensorRT plans built by DeploySharp's `TensorRtOnnxEngineBuilder` and takes a long time with strict-FP32 text plans; its elapsed time is not a performance benchmark.

```powershell
$env:DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT = 'E:\Model\PaddleDocument\chart2table-builder-e2e-20260924'
$env:DEPLOYSHARP_CHARTQA_SAMPLE_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-20260924'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  --no-restore `
  --filter FullyQualifiedName~ChartQaHumanTestChartsGenerateCompleteTablesThroughTensorRt
```

Export requires three explicit workarounds: `--enable_dist_prim_all True` for the Qwen2 SwiGLU graph, stateless export-time rotary calculation to avoid static-tracer aliasing, and an explicit `1e-6` RMSNorm epsilon add because the converter otherwise emits a zero epsilon. These operate on exported graphs; the source checkpoint remains unchanged.

TensorRT 10.11 on the local RTX 3060 Laptop requires FP16 vision/projector and token-embedding plans, and FP32 language prefill/decode plans; TF32 tactics may be allowed for those FP32 tensors as an opt-in measured variant. The FP16 text graph generated all-zero `lm_head` logits; strict FP32 and sample-verified TF32 plans restored the matching full table. The reported null Engine was a test-input/profile mismatch, not a general `TensorRtOnnxEngineBuilder` limitation: the test selected the non-epsilon decoder graph and used an attention-mask optimum of 512 although the profile's KV optimum is 512 and the mask must be `past + 1` (513). TensorRT's native log identified the attempted 512-to-513 reshape. Aligning the test with the epsilon graph used by the release plan and setting the mask profile to 513 let the library Builder serialize the 51-input dynamic-KV graph successfully; Builder-created plans for all four graphs then completed exact full-table EOS generation. The ONNX opset probe was also changed to read only protobuf metadata for modern opsets rather than materializing the multi-gigabyte graph in memory. Use `scripts/Build-PaddleChart2TableTensorRtPlans.ps1` to reproduce the strict plans, or pass `-TextOnly -EnableTextTf32` to build alternate FP32/TF32 text plans without overwriting the existing vision/embedding assets.

```powershell
& .\scripts\Build-PaddleChart2TableTensorRtPlans.ps1 `
  -OnnxRoot E:\Model\PaddleDocument\chart-export `
  -TextGraphRoot E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923 `
  -TensorRtRoot 'D:\Program Files\TensorRT-10.11.0.33-cu12' `
  -CudaRoot 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' `
  -CudnnRoot 'D:\Program Files\cuDNN-9.22.0-cuda12.9' `
  -OutputRoot D:\Model\PaddleDocument\chart2table-tensorrt-verify-20260923
```

The published bundle is executable on ORT CPU, OpenVINO CPU and TensorRT CUDA; all three have full EOS-terminated output evidence for the same official sample. Additional ChartQA human test charts are being used for task-level qualitative validation; OpenCV DNN autoregressive execution remains unsupported by the current adapter contract: Prefill requires rank-3 `inputs_embeds`, while Decode requires rank-4 dynamic KV tensors, and the current OpenCV C# `Mat` bridge exposes only 2-D auxiliary allocation/reshape. The isolated probe therefore fails closed before inference rather than claiming partial generation support. A labeled dataset accuracy metric also remains unverified.

The four curated ChartQA human images now have separate ORT CPU, OpenVINO CPU and TensorRT CUDA full-generation regressions: each backend produced all four EOS-terminated tables with exact expected-text SHA matches. See [`chart2table-ort-multi-image-20260929.md`](verification/chart2table-ort-multi-image-20260929.md), [`chart2table-openvino-multi-image-20260929.md`](verification/chart2table-openvino-multi-image-20260929.md) and [`chart2table-tensorrt-multi-image-20260930.md`](verification/chart2table-tensorrt-multi-image-20260930.md). These are qualitative four-image checks, not full-split ChartQA accuracy or a controlled performance benchmark.

The bounded [ChartQA `val` quality selection](verification/chart2table-extended-quality-20261002.md) adds six official image/CSV pairs from a pinned upstream revision. ORT CPU and OpenVINO CPU both reached EOS for `6/6`, produced equal text SHA values for `6/6`, and exposed the same multi-column structure boundary; the aggregate `44/116` matching cells is task-quality evidence for this small grouped selection only, not split-level accuracy. The acquisition script keeps the GPL-3.0 source data outside the repository and records image/table SHA-256 values in the local manifest.

On 2026-10-04 the same revision was expanded to twelve samples for an ORT CPU task-quality run. All `12/12` generations reached EOS, `9/12` matched row/column dimensions, `3/12` matched the CSV-derived text exactly, and aggregate cell matching was `140/293` (`47.78%`); total latency P50/P95 was `26.866/72.933 s`. This is a grouped qualitative extension, not split accuracy or a controlled benchmark. The [machine-readable report](verification/chart2table-extended-quality-ort-20261004.json) retains per-sample source/output hashes and stage timings. An OpenVINO run at the same `1024` token cap was stopped after more than 13 minutes without a report; the six-sample OpenVINO record above remains the canonical complete evidence.

The same six-image selection now has a separate TensorRT CUDA record: all `6/6` generations reached EOS, `5/6` had matching table dimensions, and aggregate cell matching was `44/116` (`37.93%`). On the local RTX 3060 Laptop with TensorRT `10.11.0.33-cu12`, CUDA `12.9` and cuDNN `9.22`, total latency ranged from `1.18` to `13.25 s`; Decode P50/P95 ranged from `33.32–40.39` / `34.32–48.60 ms`. The run used the existing four plans and was not clock-locked, so it is bounded quality and stage-timing evidence rather than split accuracy or a controlled benchmark. See [the TensorRT report](verification/chart2table-tensorrt-extended-quality-20261004.md) and [JSON](verification/chart2table-tensorrt-extended-quality-20261004.json).

The aligned 12-image `ChartQA/val` selection was also rerun on the same TensorRT plans. All `12/12` generations reached EOS, `9/12` matched row/column dimensions, and aggregate cell matching was `140/293` (`47.78%`). Total latency P50/P95 was `4,398.95/12,209.76 ms`; decode-step P50/P95 ranges were `33.37–39.48/35.85–48.81 ms`. This remains a bounded qualitative diagnostic, not split accuracy or a controlled performance benchmark. See [the 12-image TensorRT report](verification/chart2table-extended-quality-tensorrt-20261005.md) and [JSON](verification/chart2table-extended-quality-tensorrt-20261005.json).

The same twelve rows can be compared without rerunning inference by using [the ORT/TensorRT alignment report](verification/chart2table-extended-quality-backend-alignment-20261005.md) and its [machine-readable JSON](verification/chart2table-extended-quality-backend-alignment-20261005.json). It verifies that the two files use the same ChartQA revision, split and input-image SHA values, then reports finish reasons, structure flags, expected cell counts and exact-cell totals side by side. This is a bounded backend-consistency diagnostic; it does not turn the selection into split-level accuracy or a controlled speed benchmark.

For a compact current-state view, see the [Chart2Table backend and quality status index](verification/chart2table-backend-quality-status-20261011.md) and [machine-readable JSON](verification/chart2table-backend-quality-status-20261011.json). It keeps full-generation support, bounded ChartQA quality diagnostics and the OpenCV rank-limited adapter boundary separate. The index does not promote the small selections to split-level accuracy or generalize TensorRT plans across devices.

The latest relative-link audit covers this README, the OCR README, the main PP-Structure article, the backend matrix, the formula EOS trace and the Chart2Table quality records. Its machine-readable result is [document-link-audit-20261011-chart2table.json](verification/document-link-audit-20261011-chart2table.json); it checked `283` local Markdown links with `0` broken links. The count and broken-link total are generated by `scripts/Test-PaddleDocumentLinks.ps1` and recorded in that JSON.

The six formula models also have a controlled five-variant ORT CPU regression: all 30 runs produced non-empty, non-truncated sequences, while the Plus-S/M/L models matched the normalized reference on 4/5 variants. Mean whitespace-stripped LaTeX CER was 1.18%/0.47%/0.71% for Plus-S/M/L, 7.10%/2.37% for FormulaNet-S/L and 6.51% for UniMERNet; the complete outputs are retained for review. See [`formula-six-models-variants-20260930.md`](verification/formula-six-models-variants-20260930.md) and the [machine-readable report](verification/formula-six-models-variants-20260930.json). This remains a one-equation controlled regression, not a formula dataset accuracy score; OpenVINO is still blocked by its isolated importer/native failures.

The same six-model/30-variant test was rerun on 2026-10-02 with the current worktree and passed `2/2`; all 30 row-level model/variant outputs matched the checked-in report's LaTeX hashes, normalized edit distances and EOS state. This is a reproducibility check, not new accuracy evidence; the multi-equation labeled-set and OpenVINO gates remain open.

On 2026-10-04, six derived FormulaNet/UniMERNet graphs were generated with the Loop-body parameter alpha-renaming helper and then tested in isolated OpenVINO CPU processes. All six passed `onnx.checker`, but none was admitted: Plus-S and FormulaNet-S reached execution with token sequences that did not match ORT (3 vs 197 tokens, and 1023 truncated tokens vs 213), Plus-M/Plus-L/FormulaNet-L failed on an OpenVINO Loop-body reshape shape conflict, and UniMERNet still caused a native reader access violation. The compatibility root is therefore not a publishable OpenVINO workaround. See [`formula-openvino-loop-compat-20261004.md`](verification/formula-openvino-loop-compat-20261004.md) and its [machine-readable report](verification/formula-openvino-loop-compat-20261004.json).

The 2026-10-08 isolated recheck on the current Windows runtime reproduced the exact-artifact boundary: all five FormulaNet exports fail in `Loop-18` canonical-input validation and UniMERNet aborts the isolated model-reader process. See the [current six-model report](verification/formula-openvino-isolated-20261008.md) and [machine-readable result](verification/formula-openvino-isolated-20261008.json). These rows remain unsupported; no missing-DLL or decoder workaround is implied.

For a compact, backend-by-backend view of the six formula models, including the 121-sample ORT CPU quality numbers, the OpenVINO/OpenCV exact blockers and the still-unverified TensorRT gate, see the [formula backend quality/status index](verification/formula-backend-quality-status-20261011.md) and [JSON](verification/formula-backend-quality-status-20261011.json). This index deliberately keeps execution evidence separate from quality acceptance: the realFormula exact-match/CER values are diagnostic and currently do not pass a release-quality gate.

The same realFormula predictions have a derived [structural diagnostic report](verification/formula-realformula-structure-quality-20261011.md) and [JSON](verification/formula-realformula-structure-quality-20261011.json), generated by `scripts/Summarize-FormulaStructureQuality.ps1`. It reports length strata, generated delimiter/environment balance and command counts without claiming TeX or mathematical equivalence.

The [command-token diagnostic](verification/formula-realformula-command-quality-20261011.md) joins the same six ORT CPU prediction files to the external MathNet manifest by image SHA and computes lexical command-token precision/recall/F1 plus shallow reference/prediction `\\begin`/`\\end` environment pairing without copying raw reference LaTeX into the repository. Micro-F1 ranges from `45.15%` to `63.01%` across the six models; prediction environments are balanced for `117/121` to `121/121` rows. This is a structure-oriented string metric only; it does not prove valid TeX rendering or mathematical equivalence. Re-run it with `scripts/Summarize-FormulaCommandQuality.ps1` when the pinned external dataset is available.

The companion [structural-token diagnostic](verification/formula-realformula-structural-quality-20261011.md) broadens the same six-model ORT CPU comparison to a conservative multiset of commands, identifiers, numeric literals, grouping delimiters, sub/superscripts and common operators. Micro-F1 ranges from `79.81%` to `86.53%`; the score is useful for locating structural over-generation or truncation, but is still lexical only and does not establish TeX parsing, rendered equality or mathematical semantics. Re-run it with `scripts/Summarize-FormulaStructuralQuality.py`; raw reference LaTeX remains outside the repository.

The reusable `PaddleDocumentFormulaQualityEvaluator` now exposes the same conservative diagnostics to applications and tests. `Compare(expected, actual)` keeps raw trimmed equality separate from whitespace-normalized equality, computes Unicode-scalar CER, command-token multiset precision/recall/F1, and checks brace, `\\left`/`\\right`, and `\\begin{...}`/`\\end{...}` balance. `NormalizeLatex` deliberately performs whitespace and line-ending normalization only; it does not rewrite commands or claim semantic equivalence. These metrics are suitable for regression gates and dataset reports, but a rendered/semantic evaluator is still required before declaring formula accuracy.

```csharp
PaddleDocumentFormulaQualityMetrics quality =
    PaddleDocumentFormulaQualityEvaluator.Compare(referenceLatex, result.Latex);

Console.WriteLine($"normalized={quality.NormalizedTextMatch}, " +
    $"CER={quality.NormalizedTextAccuracy.CharacterErrorRate:P2}, " +
    $"command-F1={quality.CommandF1:P2}, " +
    $"balanced={quality.ActualBalancedDelimiters}");
```

A focused [TensorRT builder probe](verification/formula-tensorrt-builder-probe-20261011.md) now records the first real CUDA boundary: the Plus-S graph loads TensorRT 10.11/CUDA 12.9 and initializes the GPU, then terminates with `0xC0000005` during ONNX parser startup before an Engine is created. This is one-graph builder evidence only; the six-model TensorRT quality/performance matrix remains unverified and no other formula model is inferred from it.

The follow-up [derived-graph probe](verification/formula-tensorrt-derived-graph-probe-20261011.md) keeps four non-publishable Plus-S copies separate from the official ONNX. Polygraphy cleanup preserves the parser `BitwiseAnd` blocker; a diagnostic bool-mask rewrite reaches TensorRT 11 parsing but returns an empty Engine because the autoregressive Loop contains shape-changing recurrence (`[3]` to `[1]`, then `[-1,3]` to `[-1,6]`). No derived copy reached Decoder or received a TensorRT support mark. Reproduce the copies with [`Prepare-FormulaTensorRtCompatibilityVariants.py`](scripts/Prepare-FormulaTensorRtCompatibilityVariants.py); generated files stay outside Git.

Before attempting a future compatibility export, use the [six-model static TensorRT audit](verification/formula-tensorrt-static-compatibility-20261011.md). It records each model's SHA, node counts, dynamic axes and loop-body risk operators without rebuilding the original graphs. All six have an autoregressive `Loop` with control flow and dynamic-shape operations; this explains the special boundary but is not itself a support verdict.

The 2026-10-05 local inventory remains available in [the formula evaluation data audit](verification/formula-data-availability-audit-20261005.md) and its [JSON inventory](verification/formula-data-availability-audit-20261005.json); it described only the then-present local roots. We subsequently acquired the pinned [MathNet realFormula v1 dataset](https://doi.org/10.5281/zenodo.11296815), verified its Zenodo MD5 and all 121 image/label pairs, and evaluated the six local formula ONNX models on ORT CPU. See the [six-model summary](verification/formula-realformula-six-models-ort-20261007.md) and [machine-readable summary](verification/formula-realformula-six-models-ort-20261007-summary.json), with per-sample model reports linked there. The [error-pattern diagnostic](verification/formula-realformula-error-patterns-20261007.md) adds label-string strata, bounded edit rates and an explicitly non-authoritative sensitivity analysis that strips selected flat font wrappers; it does not relabel string matches as mathematical correctness. Exact-match rates are low and character CER is high across this corpus; training overlap is unknown, and these measurements do not prove TeX semantic equivalence or other-backend quality. A follow-up on FormulaNet-L sample `211110912-2.png` replayed both DeploySharp- and PaddleX-preprocessed tensors: despite 3,433/589,824 unequal values, ORT produced identical raw token IDs for both; official PaddleX and ORT also matched exactly on each same tensor, and neither emitted EOS. A separate derived-graph probe confirmed the official Paddle IR and ONNX Loop both cap generation at 1,024 iterations; raising the ONNX cap fails at positional embedding index 1,026 because the learned table has only 1,026 rows. Three additional realFormula images reached EOS at raw output index 219, 1,023 and 1,023 respectively; the two boundary-length predictions still had very high CER, so EOS completion is not a quality pass. The graph probe identifies the export ceiling but does not explain why the original sample failed to emit EOS before it. See the [preprocessing/EOS trace](verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md), [generation-limit report](verification/formula-formulanet-l-generation-limit-20261007.md) and the [sensitivity JSON](verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.json). The decoder's 4,096-token bound is a safety limit, not a generation setting. This advances—but does not close—the formula quality gate: model suitability/canonicalization, understanding the one-sample non-EOS behavior, and OpenVINO/OpenCV/TensorRT quality remain open.

The consolidated [dynamic-batch coverage index](verification/paddle-document-dynamic-batch-coverage-20261010.md) now maps every audited dynamic PP-Structure family with a real profile/decoder to exact ORT/OpenVINO evidence and records the separate OpenCV/TensorRT boundaries. It intentionally keeps static batch=1 exports and Chart2Table's serialized autoregressive path out of the model-Batch support count.

On 2026-09-18 the repository manifest was acquired into `E:\Model\PaddleDocument` with Python 3.11, PaddlePaddle `3.0.0.dev20250613`, Paddle2ONNX `2.0.2rc3`, ONNX `1.17`, and ONNX Runtime CPU. Twenty-eight standard inference archives converted successfully and passed the structural smoke tool. The generated report is outside Git at `E:\Model\PaddleDocument\onnx-smoke.json`.

To repeat the graph check with a real image (the script uses a stride-friendly 640x640 canvas for dynamic graphs), run:

```powershell
& E:\Model\PaddleDocument\paddle3\python.exe .\scripts\Verify-PaddleDocumentOnnx.py `
  --onnx-root E:\Model\PaddleDocument\onnx `
  --image E:\Data\ocr\demo_1.jpg `
  --output E:\Model\PaddleDocument\semantic-smoke.json
```

Use one or more `--model <onnx-file-stem>` arguments to avoid running the heavyweight FormulaNet/UniMERNet graphs while iterating.

The general converter report proves graph loading on its selected smoke inputs, not semantic parity. Chart2Table has separate real-image generation evidence in `chart2table-component-validation.json`. Its source archive remains `conversion-blocked` in the single-file acquisition catalog because it is generative weights/tokenizer sidecars rather than `inference.json`/`inference.pdiparams`; the derived four-graph ONNX bundle and tokenizer sidecars are available through the `paddle-chart/pp-chart2table` entry in the public Release.

For a converted model, inspect the ONNX graph before constructing a profile. Layout, cell and seal exports use Paddle post-NMS rows `[class, score, x1, y1, x2, y2]`; use `PaddleDocumentProfiles.CreatePaddleNmsRegions`. SLANeXt uses two outputs and `CreateTableStructure`. UVDoc uses `CreateUnwarping`. FormulaNet/UniMERNet return integer token IDs and require an explicit `PaddleDocumentFormulaSchema`. A custom raw-candidate export can use `CreateRegionDetection(..., maximumBatch: N)` when its input and output really expose a dynamic batch axis; the default remains batch one, and the decoder preserves per-row geometry plus deterministic NMS. This option does not convert a static export into a batched model. Chart2Table remains an autoregressive batch-one workflow because its KV generation state is per image.

The six-model ORT CPU semantic regression is recorded in
[formula-ort-six-models-20260930.md](verification/formula-ort-six-models-20260930.md)
and its machine-readable [evidence JSON](verification/formula-ort-six-models-20260930.json).
The report uses the official formula image and each model's own tokenizer,
records all asset hashes and EOS/warning state, and keeps FormulaNet-S/L and
UniMERNet outside the Plus reference-match claim until independently labeled
formula samples are available.

## OpenVINO SLANeXt compatibility artifact

The two original SLANeXt ONNX files contain Loop-body formal parameters whose names collide with captured outer values. The OpenVINO importer rejects those graphs even though the operators are standard ONNX. Generate separate compatibility artifacts by alpha-renaming only the Loop-body parameters. The wrapper generates both variants and writes a machine-readable source/derived SHA-256 manifest:

```powershell
& .\scripts\Build-PaddleDocumentOpenVinoCompatibility.ps1 `
  -ModelRoot E:\Model\PaddleDocument\onnx `
  -OutputRoot E:\Model\PaddleDocument\onnx-normalized `
  -Python E:\Model\PaddleDocument\paddle3\python.exe `
  -ManifestPath E:\Model\PaddleDocument\onnx-normalized\slanext-openvino-compatibility.json
```

The wrapper checks the resulting graphs with `onnx.checker`, never overwrites the source, and records both SHA-256 values, sizes, compatibility IDs and future Release asset names. The checked-in evidence is [`slanext-openvino-compatibility.json`](verification/slanext-openvino-compatibility.json). The verified derived hashes are wired `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` and wireless `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6`. Both derived graphs match the original ONNX Runtime outputs element by element within `0.001` and produce the same decoded table token sequence. The Release policy reserves `slanext-wired-openvino-compat.onnx` and `slanext-wireless-openvino-compat.onnx` as separate assets; until those assets are uploaded, the manifest status remains `planned-separate-assets` and consumers must generate them locally.
