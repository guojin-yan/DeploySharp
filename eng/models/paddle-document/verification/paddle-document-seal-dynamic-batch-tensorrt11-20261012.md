# PP-OCRv4 seal TensorRT dynamic Batch=2 evidence (2026-10-12)

This report records an opt-in Windows host run for the two registered PP-OCRv4 seal detector ONNX artifacts. The test built a TensorRT 11 engine with `min=1,opt=max=2`, submitted two non-overlapping horizontal ROI rows from the same `demo_1.jpg`, compared raw probability-map rows against ORT CPU, and decoded both rows through `PaddleDocumentSealDecoder`.

## Host and protocol

- OS: Windows 10.0.26200 x64, .NET 10.0.12.
- GPU: RTX 3060 Laptop, compute capability target `compute_86`.
- CUDA 12.9, TensorRT `11.0.0.114-cu12`, cuDNN 9.22; the checked-in TRT11 bridge was used.
- Input: `E:\Data\ocr\demo_1.jpg` (SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`), split into `[0,0,500,250]` and `[0,250,500,250]` ROI bands.
- Profile: `x=[1,3,224,224]` / `opt=[2,3,224,224]` / `max=[2,3,224,224]`; output `[2,1,224,224]`.
- Warmup/measurement: `2 + 5`; this is a dynamic-batch execution and bounded parity smoke, not a locked-frequency performance benchmark.

## Results

| Artifact | ONNX SHA-256 | Engine bytes | ORT/TRT decoded rows | Raw mask contract | P50/P95 |
| --- | --- | ---: | ---: | --- | ---: |
| `paddle-seal/ppocrv4-mobile` | `e4b20a5c…f9199` | 7,530,324 | `2/2`, both rows have `0` regions | Both rows finite; max absolute error `≤0.05`, mean absolute error `≤0.005` | `6.5686 / 7.2197 ms` |
| `paddle-seal/ppocrv4-server` | `f9448c3f…34c7` | 153,137,068 | `2/2`; row 0 has `3`, row 1 `0` regions in both backends | Both rows finite; max absolute error `≤0.05`, mean absolute error `≤0.005` | `20.4415 / 21.0686 ms` |

The complete machine-readable records are [`mobile JSON`](paddle-document-seal-mobile-dynamic-batch-tensorrt11-20261012.json) and [`server JSON`](paddle-document-seal-server-dynamic-batch-tensorrt11-20261012.json). Each record includes model/engine/input hashes, row hashes, shapes, timing, and the exact boundary statement.

## Interpretation and boundary

This closes the exact PP-OCRv4 mobile/server **TensorRT 11 dynamic Batch=2 execution, row binding and bounded ORT parity** gap on this Windows host. The ROI bands are execution probes, not seal ground truth; the result is not a seal accuracy score, cross-device engine portability guarantee, or all PP-OCR seal model claim. GPU clocks were not locked (`nvidia-smi` observed approximately 1425 MHz graphics / 7000 MHz memory), so the P50/P95 values must not be compared as a formal performance ranking.

Reproduce with `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1`, matching `JYPPX_NATIVE_BRIDGE_PATH`, `JYPPX_TENSORRT_ROOT`, `JYPPX_CUDA_ROOT`, `JYPPX_CUDNN_ROOT`, and the test filter `SealDetectionRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows`.
