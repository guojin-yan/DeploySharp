# Core PP-OCR TensorRT 10.11 steady-state performance (2026-10-13)

## Result

This is the first current-revision formal performance matrix for all seven core
PP-OCR combinations on the dedicated Windows host. The benchmark used the
complete `det -> crop -> optional cls -> rec -> merge` path, reused the decoded
image and detector input, and used device-local TensorRT engines rebuilt by
DeploySharp's managed `TensorRtOnnxEngineBuilder`.

| Model | Status | Batch | Channels | Regions | P50 total (ms) | P95 total (ms) | Detector inference (ms) |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PP-OCRv4 Mobile | pass | 8 | 2 | 16 | 191.256 | 200.739 | 50.423 |
| PP-OCRv4 Server | pass | 8 | 2 | 16 | 243.536 | 258.356 | 104.424 |
| PP-OCRv5 Mobile | pass | 8 | 2 | 16 | 196.894 | 210.507 | 53.818 |
| PP-OCRv5 Server | pass | 8 | 2 | 16 | 488.820 | 522.446 | 175.219 |
| PP-OCRv6 Tiny | pass | 8 | 2 | 16 | 80.169 | 91.488 | 36.703 |
| PP-OCRv6 Small | pass | 8 | 2 | 16 | 143.523 | 152.854 | 52.478 |
| PP-OCRv6 Medium | pass | 8 | 2 | 16 | 326.212 | 369.506 | 122.501 |

All seven rows passed the report validator. The timed calls also kept one strict
result-contract SHA per row (`result_contract_variants=1`) and recorded a stable
semantic result contract across all 50 measurements.

## Fixed protocol and environment

- protocol: `5` warm-up + `50` measured calls, `deploysharp-paddleocr-full-5-50-v1`
- input: `E:\Data\ocr\demo_1.jpg`
- input SHA-256: `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`
- pipeline: reuse prepared input, `batch=8`, independent stage channels `2`, max recognition width `320`, overflow `Clamp`
- device: `JYPPX`, Windows `10.0.26200`, x64, .NET `10.0.12`, 16 logical CPUs
- GPU: NVIDIA GeForce RTX 3060 Laptop GPU, driver `576.02`, compute capability `8.6`
- runtime: TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, bridge package `4.0.0`
- source revision: `77a7ef5b15e68934659eed0e1a11837edce38abb+working-tree`
- Engine build: DeploySharp managed Builder, `TensorRtApiVersion.TensorRt10`, `optimizationLevel=0`, runtime-default precision

The CSV and environment sidecar preserve every row's stage columns, text and
contract hashes, source revision, model ONNX hashes, generated Engine hashes,
and runtime paths:

- [CSV matrix](paddleocr-core-tensorrt10.11-steady-formal-20261013.csv)
- [environment and model hashes](paddleocr-core-tensorrt10.11-steady-formal-20261013.csv.environment.json)
- [formal report validation](paddleocr-core-tensorrt10.11-steady-formal-20261013.validation.json)

## Interpretation and limits

The P50/P95 values are percentiles over the 50 repeated calls for one fixed
image. Model loading, Engine construction, and initial image preparation are
outside the measured interval; crop preparation, stage inference, postprocess
and merge remain inside it. `preprocess_ms=0` means prepared-input reuse, not
zero cost for a cold end-to-end call.

This is a fixed-host steady-state observation, not a cross-device ranking. GPU
clocks were not locked, the run has one input image, and no CER/WER or annotated
quality score is implied. It does not establish TensorRT Engine portability to a
different GPU/driver/runtime, nor does it promote other PP-Structure models or
unmeasured backends.

## Reproduction

Build the benchmark once, configure the matching Windows TensorRT bridge/runtime,
and set the following environment variables. The rebuild switch creates a
device-local Engine cache and `.build.json` sidecars; omit it on later runs to
reuse that cache.

```powershell
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4,v5,v6'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\Data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH = '320'
$env:DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE = 'Clamp'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_REBUILD_ENGINES = '1'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_ENGINE_ROOT = 'artifacts\local-model-benchmarks\paddleocr-managed-trt10.11-engines-opt0-20261013'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BUILD_OPTIMIZATION_LEVEL = '0'
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_TENSORRT_API_VERSION = '10'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_86'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'

dotnet tools\DeploySharp.PaddleOcrBenchmark\bin\Release\net10.0\DeploySharp.PaddleOcrBenchmark.dll `
  E:\Model\paddleocr `
  artifacts\local-model-benchmarks\paddleocr-core-tensorrt10.11-steady-formal-current.csv
```

Run `Test-PaddleOcrBenchmarkReport.ps1` with `-RequireSourceRevision`
and `-RequireTensorRtRuntime` before adding a new device record to the
performance matrix.
