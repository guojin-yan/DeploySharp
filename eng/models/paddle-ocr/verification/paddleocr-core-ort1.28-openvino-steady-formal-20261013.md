# Core PP-OCR ORT 1.28 / OpenVINO steady-state performance (2026-10-13)

## Result

All seven registered core PP-OCR combinations completed the full
`det -> crop -> optional cls -> rec -> merge` pipeline on both ONNX Runtime CPU
and OpenVINO CPU. Each row used five warm-up calls followed by 50 measured calls.

| Model | ORT P50/P95 (ms) | OpenVINO P50/P95 (ms) | Regions | Ordered text parity |
| --- | ---: | ---: | ---: | --- |
| PP-OCRv4 Mobile | 687.532 / 854.398 | 118.722 / 132.187 | 16 | match |
| PP-OCRv4 Server | 1980.192 / 2726.460 | 1626.508 / 1897.787 | 16 | match |
| PP-OCRv5 Mobile | 950.048 / 1427.229 | 406.964 / 668.320 | 16 | match |
| PP-OCRv5 Server | 1563.311 / 2132.584 | 1457.802 / 2317.060 | 16 | match |
| PP-OCRv6 Tiny | 219.016 / 306.631 | 61.868 / 80.563 | 16 | match |
| PP-OCRv6 Small | 440.760 / 611.163 | 235.707 / 412.141 | 16 | match |
| PP-OCRv6 Medium | 1423.131 / 2153.559 | 773.402 / 1178.176 | 16 | match |

The formal validator accepted all `14/14` rows. Every row kept one strict
result-contract variant across its 50 timed calls. For each model, ORT and
OpenVINO produced the same ordered-text SHA and semantic result-contract SHA.
Backend-specific floating-point values remain in the strict contract and are not
claimed to be bitwise equal.

## Fixed protocol and environment

- protocol: `deploysharp-paddleocr-full-5-50-v1`, five warm-ups and 50 measured calls
- input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`
- pipeline: prepared-input reuse, recognition batch `8`, two independent stage channels, maximum recognition width `320`, overflow `Clamp`
- host: `JYPPX`, AMD Ryzen 7 5800H (8 cores / 16 logical processors), Windows `10.0.26200`, x64, .NET `10.0.12`
- loaded managed runtimes: Microsoft.ML.OnnxRuntime `1.28.0.0`, OpenVINO.CSharp.API `3.3.1.0`, JYPPX.OpenCV.CSharp.API `5.0.0.0`
- OpenVINO native runtime package: `2026.2.1`
- source revision: `8f30dd826e76d8cac9924484d353885a0c7f646b+ort-build-isolation-dirty`

The benchmark was built in an isolated `--artifacts-path`; its schema 3
environment sidecar records the actual loaded assembly versions, paths and
SHA-256 values. Because TensorRT was not selected, TensorRT protocol/runtime and
Engine fields are null rather than describing unrelated adjacent Engine files.

- [CSV matrix](paddleocr-core-ort1.28-openvino-steady-formal-20261013.csv)
- [environment, assembly and model hashes](paddleocr-core-ort1.28-openvino-steady-formal-20261013.csv.environment.json)
- [formal report validation](paddleocr-core-ort1.28-openvino-steady-formal-20261013.validation.json)

## Interpretation and limits

These values are fixed-host steady-state observations for one image. Model and
session creation, image decode and detector-input preparation are outside the
timed interval. Crop creation, stage inference, decoding, postprocessing and
merge remain inside it. `preprocess_ms=0` means prepared-input reuse, not a
zero-cost cold request.

CPU frequency and system load were not controlled. A diagnostic run earlier in
the same work session produced different tails, so this report does not infer a
speed improvement from run-to-run variation. It is not a cross-device ranking,
throughput result, CER/WER score, detection-recall measurement or proof that
unlisted PP-Structure models support either backend. The formal TensorRT 10.11
matrix is maintained separately because it uses device-local Engines and a
different runtime contract.

## Reproduction

Build the default ORT graph into an isolated artifact tree, then run that exact
DLL. Do not reuse the CUDA 12 artifact tree, which intentionally carries ORT
1.23.2.

```powershell
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --artifacts-path artifacts/build/paddleocr-default `
  -p:DeploySharpPaddleOcrCuda12=false -p:RestoreLockedMode=true

$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'onnxruntime,openvino'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\Data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH = '320'
$env:DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE = 'Clamp'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<commit-or-dirty-revision>'

dotnet artifacts/build/paddleocr-default/bin/DeploySharp.PaddleOcrBenchmark/release/DeploySharp.PaddleOcrBenchmark.dll `
  E:\Model\paddleocr `
  artifacts/local-model-benchmarks/paddleocr-core-ort-openvino-current.csv
```

Run `Test-PaddleOcrBenchmarkReport.ps1 -RequireSourceRevision` before adding a
new result to the maintained matrix.
