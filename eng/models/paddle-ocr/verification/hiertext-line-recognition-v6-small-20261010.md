# HierText line-level recognizer evidence (PP-OCRv6 Small, refreshed 2026-10-11)

This record isolates the recognizer on source-linked HierText text-line crops. It avoids mixing detector recall or page-level reading order into the recognition score. The original 32-row smoke used a contiguous prefix from only two parent images and included annotation rows marked `ignore=true`; it is superseded below by a reproducible run over all 1,557 eligible non-ignored rows from the two local validation selections. The same crop images and PP-OCRv6 Small ONNX/character dictionary were run on ONNX Runtime CPU and OpenVINO CPU.

## Refreshed results — all eligible rows

| Backend | Rows | Parent images | Exact rows | Exact rate | Reference chars | CER | Reference words | WER | Empty outputs | Text parity vs ORT |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 1,557 | 34 | 713 | 45.7932% | 11,979 | 25.4278% | 2,465 | 46.9777% | 36 | reference |
| OpenVINO CPU | 1,557 | 34 | 713 | 45.7932% | 11,979 | 25.4278% | 2,465 | 46.9777% | 36 | 1,557/1,557 rows identical |

All 34 parent images represented in `sample-002`/`sample-003` are included. There were 2,288 annotation lines in those manifests: 731 source-ignored lines were excluded; the remaining 1,557 non-empty, non-ignored lines all had a matching crop file and matching crop SHA-256. Thus no eligible source-linked line was omitted by the run. Backend parity is exact for every recognized line.

### Character-length strata

| Reference characters | Rows | Exact rows | CER | WER | Empty outputs |
|---|---:|---:|---:|---:|---:|
| 1–4 | 764 | 412 | 38.8920% | 47.0893% | 12 |
| 5–10 | 470 | 176 | 35.1510% | 64.7458% | 20 |
| 11–20 | 229 | 83 | 23.1839% | 52.2989% | 2 |
| 21–40 | 64 | 29 | 18.4211% | 31.0469% | 1 |
| 41+ | 30 | 13 | 6.7352% | 17.4917% | 1 |

Metrics are case-sensitive Unicode-scalar CER/WER with no implicit normalization. The line-length table is diagnostic: HierText has many very short strings, and this aggregate should not be compared directly with page-level or line-oriented benchmarks that use different filtering/normalization policies. The poor aggregate quality remains an open model/preprocessing/recognition-quality issue; backend agreement is not accuracy.

## Provenance and reproduction

- Model: PP-OCRv6 Small detector/recognizer; detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`; recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Character dictionary SHA-256: `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- HierText annotation manifest SHA-256: `sample-002=e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7`; `sample-003=f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- Crop manifest SHA-256: `sample-002=1681e102b1b9c0cf4a3b6eb43e25ef4340526194619a9f441d0ca26543a8780b`; `sample-003=d2cd2abc8d42f93c9939d1c895d139d4448395b8ea88e9c0d7a25d165b045a7d`.
- Source revision for execution: `03d7c20a200aea732c5ab1a09b04461db1fdb189`; evaluator source SHA-256 `0814bd31d4f967671d00b259e9ef3eee2533b897952c0a72c29fe1ed29eb6fbd`; Windows, .NET SDK `10.0.401`, ORT managed `1.28.0`, OpenVINO C# API `3.3.1` / runtime `2026.2.1`. The repository working tree contains unrelated changes; raw crop images/predictions remain outside Git.
- The test excludes source-ignored and empty annotations, validates each crop against the manifest SHA, and evenly samples the ordered eligible manifest only when a cap is set. With `4096`, no cap was applied (`1,557` eligible rows).

```powershell
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC='1'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_MAX='4096'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_OUTPUT='artifacts/hiertext-line-rec-20261011-valid'
dotnet build .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj -f net10.0 -c Release --no-restore
dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj -f net10.0 -c Release --no-build --no-restore --filter 'FullyQualifiedName~PaddleOcrHierTextLineRecognitionIntegrationTests'
```

The checked-in machine-readable summary is [hiertext-line-recognition-v6-small-20261010.json](hiertext-line-recognition-v6-small-20261010.json). It retains the original 32-row file name for compatibility, but its schema and results are refreshed to the full eligible run.

## Boundary

HierText line crops provide line-level recognition truth, but these 34 validation images are a bounded local selection, not a complete dataset accuracy claim, and their image attribution/redistribution status must be reviewed before public benchmark promotion. This evidence does not close the broader P2 quality gate, natural long-text gate, rotation/low-quality robustness gate, or the formal 5-warmup/50-iteration performance matrix. It also does not promote CUDA, TensorRT, OpenCV DNN, or other model variants.
