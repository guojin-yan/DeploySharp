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

The current exact artifact/backend status is summarized in [paddle-document-status-summary-20260929.md](verification/paddle-document-status-summary-20260929.md) and the authoritative machine-readable [backend matrix](verification/paddle-document-backend-matrix-20260929.json).

The official PP-Structure models are Paddle Inference artifacts. They are converted and distributed individually in the stable [`models-paddleocr`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) Release alongside the core PP-OCR v4/v5/v6 assets; users do not need to download a full bundle. A converted model is admitted to DeploySharp only after its exact input/output names, preprocessing, decoder, backend execution and result parity are recorded.

The checked-in model-release catalog keeps `pp-chart2table` as `conversion-blocked` only for a **single downloadable inference artifact**: the official archive is generative source weights, not `inference.json`/`inference.pdiparams`. Its separate four-graph ONNX generation bundle and three tokenizer assets are published in the [`models-paddleocr` Release](https://github.com/guojin-yan/DeploySharp/releases/tag=models-paddleocr) under bundle ID `paddle-chart/pp-chart2table`. Retrieve only those seven files through `PaddleOcrReleaseClient.GetBundleAsync`; the source-only row remains rejected by `PaddleDocumentProfile.CreateArtifact`, while consumers use `PaddleChart2TableOnnxBundle` and a backend-specific session.

## Current local evidence

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

The six formula models also have a controlled five-variant ORT CPU regression: all 30 runs produced non-empty, non-truncated sequences, while the Plus-S/M/L models matched the normalized reference on 4/5 variants and FormulaNet-S/L plus UniMERNet retained their distinct output differences for review. See [`formula-six-models-variants-20260930.md`](verification/formula-six-models-variants-20260930.md) and the [machine-readable report](verification/formula-six-models-variants-20260930.json). This remains a one-equation controlled regression, not a formula dataset accuracy score; OpenVINO is still blocked by its isolated importer/native failures.

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

For a converted model, inspect the ONNX graph before constructing a profile. Layout, cell and seal exports use Paddle post-NMS rows `[class, score, x1, y1, x2, y2]`; use `PaddleDocumentProfiles.CreatePaddleNmsRegions`. SLANeXt uses two outputs and `CreateTableStructure`. UVDoc uses `CreateUnwarping`. FormulaNet/UniMERNet return integer token IDs and require an explicit `PaddleDocumentFormulaSchema`.

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
