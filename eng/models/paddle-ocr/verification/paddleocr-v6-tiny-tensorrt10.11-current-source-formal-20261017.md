# PP-OCRv6 Tiny TensorRT 10.11 current-source formal check (2026-10-17)

This is a focused `v6-tiny` rerun from the current source revision after the benchmark harness was rebuilt from a clean copy outside the repository. It is intentionally a single-model evidence record; it does not replace the seven-model matrix.

## Protocol and environment

- Windows 10.0.26200 x64, .NET 10.0.12, RTX 3060 Laptop, driver 576.02.
- TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`; `compute_86` CUDA CTC argmax enabled.
- Input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`.
- `5` warm-ups + `50` measured calls; prepared-input reuse; recognition batch `8`; two independent stage channels; dynamic recognition width; timings exclude model load.
- Engines were rebuilt by the current DeploySharp Builder in an external cache after the old adjacent Engine was rejected as an incompatible/empty deserialization object.

## Result

| Model | Backend | Status | P50 total (ms) | P95 total (ms) | DET (ms) | REC (ms) | Regions | Result contract variants |
|---|---|---|---:|---:|---:|---:|---:|---:|
| PP-OCRv6 Tiny | TensorRT CUDA | ✓ | 15.899 | 17.422 | 7.999 | 7.951 | 16 | 1 |

The run returned 16 detected regions and 16 recognition results on every measured call. Ordered text SHA-256 was `3f48285f745f812c3fe01fb177ff1f5df592440fe418e7af80315574a791b542`; semantic contract SHA-256 was `f138c979425ea98bab8ed2d713c99a5896345dd22087cd47e766745defbf5fdd`.

## Reproduction

Build the benchmark with the CUDA 12 lock graph into an output directory outside the repository. Set `DEPLOYSHARP_PADDLEOCR_VERSIONS=v6-tiny`, `DEPLOYSHARP_PADDLEOCR_TENSORRT_REBUILD_ENGINES=1` and a caller-owned `DEPLOYSHARP_PADDLEOCR_TENSORRT_ENGINE_ROOT` to rebuild only this variant. Then unset the rebuild switch, keep the same Engine root, set `DEPLOYSHARP_CUDA_ARCHITECTURE=compute_86`, and run with:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v6-tiny'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_86'
```

The machine-readable record preserves the exact ONNX/Engine hashes, bridge/runtime paths, assembly hashes and protocol fields: [JSON evidence](paddleocr-v6-tiny-tensorrt10.11-current-source-formal-20261017.json).

## Boundary

This closes only the current-source `v6-tiny` execution/performance evidence gap on the recorded Windows host and profile. GPU clocks were not locked. It is not OCR CER/WER, detection recall, a cross-device ranking, a TensorRT Engine portability claim, or evidence for the other six core combinations or PP-Structure models.
