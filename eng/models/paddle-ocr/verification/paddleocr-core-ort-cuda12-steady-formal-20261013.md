# Core PP-OCR ONNX Runtime CUDA steady-state performance (2026-10-13)

## Result

All seven registered core PP-OCR combinations completed the full
`det -> crop -> optional cls -> rec -> merge` pipeline with ONNX Runtime CUDA.
Each row used five warm-up calls followed by 50 measured calls.

| Model | P50 (ms) | P95 (ms) | Regions | CPU/OpenVINO ordered-text and semantic parity |
| --- | ---: | ---: | ---: | --- |
| PP-OCRv4 Mobile | 69.960 | 160.443 | 16 | match |
| PP-OCRv4 Server | 117.107 | 201.558 | 16 | match |
| PP-OCRv5 Mobile | 70.545 | 117.800 | 16 | match |
| PP-OCRv5 Server | 105.702 | 168.476 | 16 | match |
| PP-OCRv6 Tiny | 22.658 | 28.969 | 16 | match |
| PP-OCRv6 Small | 43.704 | 50.460 | 16 | match |
| PP-OCRv6 Medium | 72.872 | 78.681 | 16 | match |

The formal validator accepted all `7/7` rows. Every row kept one strict
result-contract variant across its 50 timed calls with zero observed numeric
drift. For every model, the ordered-text SHA and semantic result-contract SHA
match the corresponding ORT 1.28 CPU and OpenVINO CPU rows from the maintained
CPU matrix. This is an exact result comparison for this input, not a substitute
for multi-page CER/WER or detection-recall evaluation.

## Fixed protocol and environment

- protocol: `deploysharp-paddleocr-full-5-50-v1`, five warm-ups and 50 measured calls
- input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`
- pipeline: prepared-input reuse, recognition batch `8`, two independently created stage Sessions, maximum recognition width `320`, overflow `Clamp`
- host: `JYPPX`, AMD Ryzen 7 5800H, 16 logical processors, Windows `10.0.26200`, x64, .NET `10.0.12`
- GPU: NVIDIA GeForce RTX 3060 Laptop GPU, driver `576.02`
- CUDA/cuDNN: CUDA `12.9` and cuDNN `9.22`; both roots and the explicit Windows DLL search directories are recorded in the environment sidecar
- loaded managed runtimes: Microsoft.ML.OnnxRuntime `1.23.2.0`, JYPPX.OpenCV.CSharp.API `5.0.0.0`
- source revision: `4aa66330143f3ebe9a54a38abab7088c5b31aedd+ort-cuda-formal-dirty`

The benchmark was built in a CUDA-specific isolated `--artifacts-path`. Its
schema 3 environment sidecar records the actual loaded managed assembly paths,
versions and SHA-256 values. TensorRT was not selected, so TensorRT protocol,
runtime and Engine fields remain null.

- [CSV matrix](paddleocr-core-ort-cuda12-steady-formal-20261013.csv)
- [environment, assembly and model hashes](paddleocr-core-ort-cuda12-steady-formal-20261013.csv.environment.json)
- [formal report validation](paddleocr-core-ort-cuda12-steady-formal-20261013.validation.json)
- [raw GPU telemetry](paddleocr-core-ort-cuda12-steady-formal-20261013.gpu.csv)

## GPU telemetry

The benchmark wrapper sampled `nvidia-smi` every 250 ms during the complete
seven-model process. Of 197 total samples, 158 reached at least 10% GPU
utilization. Active samples averaged `51.6%` utilization and `1,800 MHz`
graphics clock, with `84%` and `1,995 MHz` maxima. The GPU stayed in P0;
average active power was `64.6 W`, maximum temperature was `65 C`, and both
thermal-slowdown and hardware-slowdown counts were zero. NVIDIA reported
software power-cap regulation in 97 active samples. This is normal boost/power
management evidence and does not mean the GPU frequency was locked.

## Interpretation and limits

These values are fixed-host steady-state observations for one image. Model and
Session creation, image decode and detector-input preparation are outside the
timed interval. Crop creation, stage inference, decoding, postprocessing and
merge remain inside it. `preprocess_ms=0` means prepared-input reuse, not a
zero-cost cold request.

The relatively broad v4/v5 P95 tails include optional orientation-classifier
work and host scheduling variation. The run did not lock GPU or CPU frequency.
It is not a throughput result, a cross-device/backend ranking, a quality score,
or proof that unlisted PP-Structure models support ORT CUDA. The separately
maintained ORT CPU/OpenVINO and TensorRT matrices use different runtime builds
and must retain their own provenance.

## Reproduction

Build the CUDA 12 dependency graph into a dedicated artifact tree and run that
exact DLL. Do not reuse the default ORT 1.28 artifact tree.

```powershell
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --artifacts-path artifacts/build/paddleocr-cuda12 `
  -p:DeploySharpPaddleOcrCuda12=true -p:RestoreLockedMode=true

$cudaRoot = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$cudnnRoot = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:JYPPX_CUDA_ROOT = $cudaRoot
$env:JYPPX_CUDNN_ROOT = $cudnnRoot
$env:DEPLOYSHARP_ONNXRUNTIME_CUDA_DLL_DIRECTORIES = "$cudaRoot\bin;$cudnnRoot\bin"
$env:PATH = "$cudaRoot\bin;$cudnnRoot\bin;$env:PATH"
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'onnxruntime-cuda'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4,v5,v6'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\Data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '8'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<commit-or-dirty-revision>'

pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/Invoke-WithGpuTelemetry.ps1 `
  -Executable dotnet `
  -ArgumentList @(
    'artifacts/build/paddleocr-cuda12/bin/DeploySharp.PaddleOcrBenchmark/release/DeploySharp.PaddleOcrBenchmark.dll',
    'E:\Model\paddleocr',
    'artifacts/local-model-benchmarks/paddleocr-core-ort-cuda-current.csv') `
  -OutputPath artifacts/local-model-benchmarks/paddleocr-core-ort-cuda-current.gpu.csv
```

Run `Test-PaddleOcrBenchmarkReport.ps1 -RequireSourceRevision` before adding a
new result to the maintained matrix. Schema 3 ORT CUDA reports must include the
loaded ORT assembly evidence, CUDA root, cuDNN root and explicit DLL search
directories.
