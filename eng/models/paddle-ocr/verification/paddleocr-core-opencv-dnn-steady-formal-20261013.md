# Core PP-OCR OpenCV DNN steady-state performance (2026-10-13)

## Result

All seven registered core PP-OCR combinations completed the full
`det -> crop -> optional cls -> rec -> merge` pipeline with OpenCV DNN CPU.
Each row used five warm-up calls followed by 50 measured calls.

| Model | P50 (ms) | P95 (ms) | Regions | Ordered text vs Batch=8 CPU formal row | Semantic contract vs Batch=8 CPU formal row |
| --- | ---: | ---: | ---: | --- | --- |
| PP-OCRv4 Mobile | 1285.438 | 1394.693 | 16 | match | differ |
| PP-OCRv4 Server | 4135.023 | 4656.340 | 16 | match | differ |
| PP-OCRv5 Mobile | 1379.577 | 1665.937 | 16 | differ | differ |
| PP-OCRv5 Server | 4582.283 | 5803.217 | 16 | differ | differ |
| PP-OCRv6 Tiny | 338.130 | 369.176 | 16 | match | differ |
| PP-OCRv6 Small | 992.798 | 1152.157 | 16 | match | differ |
| PP-OCRv6 Medium | 2723.843 | 2859.905 | 16 | match | differ |

The formal report validator accepted all `7/7` execution rows. Every row kept
one strict result-contract variant across its 50 timed calls with zero observed
intra-backend numeric drift. The table deliberately compares this static
Batch=1 run with the separately maintained Batch=8 CPU formal matrix. The v5
text differences and all seven semantic differences are therefore
configuration differences, not isolated evidence of an OpenCV backend defect.

A current-revision matched-contract follow-up runs ORT 1.28 CPU and OpenCV DNN
at Batch=1 across all seven models and `demo_1.jpg`, `demo_2.jpg`, `demo_3.jpg`.
All `21/21` image/model pairs match exact text; recognition confidence differs
by at most `0.01` and polygon vertices by at most `0.5 px`. The schema 2
[matched-contract report](paddleocr-core-three-image-opencv-ort-20261013.md)
records the protocol, input/model hashes and loaded runtime assembly hashes.

## Fixed protocol and environment

- protocol: `deploysharp-paddleocr-full-5-50-v1`, five warm-ups and 50 measured calls
- input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`
- pipeline: prepared-input reuse, static recognition batch `1`, two independently created stage Sessions, maximum recognition width `320`, overflow `Clamp`
- recognition schedule: 16 regions became 16 inference batches; the two Sessions process independent batches concurrently
- host: `JYPPX`, AMD Ryzen 7 5800H, 16 logical processors, Windows `10.0.26200`, x64, .NET `10.0.12`
- loaded runtime: JYPPX.OpenCV.CSharp.API `5.0.0.0`, assembly SHA-256 `9c5744f2b1b680e5a9a80236f72110126061450ec7d076aea9799c4e7e365e48`
- source revision: `0f37f6b1d32a0333d7807a1c67f740733f84a4f2`

The benchmark used the default isolated `--artifacts-path`. The schema 3
sidecar records the actual loaded OpenCV managed assembly path, version and
SHA-256. TensorRT was not selected, so TensorRT protocol/runtime and Engine
fields remain null.

- [CSV matrix](paddleocr-core-opencv-dnn-steady-formal-20261013.csv)
- [environment, assembly and model hashes](paddleocr-core-opencv-dnn-steady-formal-20261013.csv.environment.json)
- [formal report validation](paddleocr-core-opencv-dnn-steady-formal-20261013.validation.json)

## Performance and allocation observations

These OpenCV graphs currently retain static `batch=1`; setting the pipeline
batch option above one does not turn them into real batched models. The report
therefore uses the supported static contract and exposes `recognition_batches=16`
rather than presenting the run as Batch=8. This protocol difference prevents a
direct backend ranking against the Batch=8 ORT/OpenVINO/TensorRT matrices.

Recognition inference dominates every row. The benchmark also reports large
per-call managed allocation totals: approximately `0.45 GiB` for v6 Tiny,
`0.81 GiB` for v4 Mobile, `1.46 GiB` for v5 Mobile, `1.98 GiB` for v6 Small,
`6.79 GiB` for v6 Medium, `7.05 GiB` for v4 Server and `7.71 GiB` for v5
Server. These counters identify the current OpenCV static recognition path as a
memory/copy optimization target; they do not measure peak working set or native
OpenCV allocation.

The subsequent [fixed-width 320 formal matrix](paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.md)
avoids repeated dynamic-width graph specialization. It reduces P50 by `26.0%`
to `49.0%` and reported managed allocation by `96.04%` to `99.76%`, while
retaining fixed-contract ORT/OpenCV ordered-text and semantic parity. Dynamic
width remains the quality-first default because fixed and dynamic text are not
universally identical.

## Interpretation and limits

These values are fixed-host steady-state observations for one image. Model and
Session creation, image decode and detector-input preparation are outside the
timed interval. Crop creation, stage inference, decoding, postprocessing and
merge remain inside it. `preprocess_ms=0` means prepared-input reuse, not a
zero-cost cold request.

CPU frequency and system load were not controlled. The report is not a
throughput result, a quality score, a cross-device/backend ranking, or proof
that unlisted PP-Structure models support OpenCV DNN. Batch-dependent dynamic
width/CTC output remains a real configuration behavior, so parity claims must
always compare identical Batch and crop-width contracts.

## Reproduction

```powershell
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --artifacts-path artifacts/build/paddleocr-default `
  -p:DeploySharpPaddleOcrCuda12=false -p:RestoreLockedMode=true

$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'opencv-dnn'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4,v5,v6'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\Data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '1'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<commit-or-dirty-revision>'

dotnet artifacts/build/paddleocr-default/bin/DeploySharp.PaddleOcrBenchmark/release/DeploySharp.PaddleOcrBenchmark.dll `
  E:\Model\paddleocr `
  artifacts/local-model-benchmarks/paddleocr-core-opencv-dnn-current.csv
```

Run `Test-PaddleOcrBenchmarkReport.ps1 -RequireSourceRevision` before adding a
new result to the maintained matrix. Schema 3 OpenCV DNN reports must include
the loaded JYPPX.OpenCV.CSharp.API assembly evidence.
