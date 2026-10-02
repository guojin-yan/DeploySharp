# B1b controlled OCR degradation extension (2026-10-02)

This is a larger, source-linked follow-up to the 2026-09-29 B1b smoke record. It uses ten real SROIE word crops from ten receipt pages and six deterministic conditions at the generator's `severe` setting. The generated images remain local test artifacts; only the machine-readable result records and this summary are checked in.

## Protocol and provenance

- Dataset: `jsdnrs/ICDAR2019-SROIE`, pinned revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`.
- Selection: ten parent pages and ten word crops, five selected from each of the two local SROIE train manifests; every row retains parent image, source instance, source crop SHA and degraded image SHA.
- Conditions: `normal`, `low-contrast`, `blur`, `noise`, `shadow` and JPEG quality 15, all generated with `severity=severe`.
- Records: 60 (10 crops x 6 conditions), generated manifest SHA-256 `4871d69d78fda55a7282d8a03eb0eafa6c801e4e49aea692592627701e394325`. The two source page manifests are SHA-256 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1` and `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`.
- Model: PP-OCRv6 Small; detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`, recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`, dictionary SHA-256 `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Backends: ONNX Runtime CPU and OpenVINO CPU.
- Recognition quality path: `OcrPipeline.RecognizeOnlyAsync` with the source crop represented as one known quadrilateral. This isolates REC and controlled pixels from full-page detector recall.
- Enhancement path: full-page `OcrPipeline.RunAsync` with one `ContrastNormalize` candidate and `ConfidenceGain(minimumGain=0)`. This is a routing/cost observation, not a second quality metric for the crop path.

## Recognizer-only results

Both backends completed 60/60 records with no empty recognized text. The decoded text was identical for all 60 corresponding rows; the maximum confidence delta was `4.9e-6`.

| Condition | Mean case-folded CER | Mean case-folded WER | Mean confidence |
| --- | ---: | ---: | ---: |
| normal | 0.00% | 0.00% | 0.9985 |
| low-contrast | 0.00% | 0.00% | 0.9984 |
| blur | 8.00% | 10.00% | 0.9394 |
| noise | 0.00% | 0.00% | 0.9950 |
| shadow | 0.00% | 0.00% | 0.9978 |
| JPEG q15 | 0.00% | 0.00% | 0.9948 |

These numbers are word-crop means, not page or line accuracy. Case-sensitive CER/WER is higher on many rows because the model emits lower-case text for upper-case source labels; the raw and case-folded values are both preserved in the per-row JSON. The severe blur condition is the only condition in this bounded selection that creates a measurable case-folded error, so the sample is useful for exercising degradation and evidence capture but is still too small to establish robustness.

## Enhancement candidate results

The full-pipeline candidate run contains a separate detector boundary: 22/60 calls produced no detected region, so no candidate comparison was possible for those calls. Of the remaining rows, 38 candidates were available, 22 were selected by the confidence-only policy and 16 were rejected as `InsufficientGain`.

| Backend | No region | Candidate available | Selected | Insufficient gain | Mean original CER (candidate rows) | Mean candidate CER | Selected CER before -> after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 22 | 38 | 22 | 16 | 0.9868% | 1.2336% | 0.00% -> 0.00% |
| OpenVINO CPU | 22 | 38 | 22 | 16 | 0.9868% | 1.2336% | 0.00% -> 0.00% |

The candidate decision and text were consistent across the two backends. The aggregate candidate CER is slightly worse because a small number of non-selected candidates degrade an already imperfect original; the confidence policy correctly rejects those rows. The selected rows do not improve CER/WER (`0% -> 0%`). Mean elapsed time was about `120.48 ms` (ORT) and `52.47 ms` (OpenVINO) for this one-call diagnostic and is not a P50/P95 performance result.

This evidence does not justify enabling enhancement by default. B1b remains open for a larger quality-controlled low-quality set, and B2/B3 remain open for false-correction analysis and an explicit accuracy-versus-cost threshold chosen from real labels.

## Reproduction

```powershell
$python = 'E:\Model\PaddleDocument\paddle3\python.exe'
$dataset = 'F:\OCRBenchmarkTesting'
& $python .\eng\models\paddle-ocr\scripts\Generate-SyntheticDegradedOcrCase.py `
  --dataset-root $dataset `
  --source-manifest "$dataset\data\annotations\manifests\sroie-train-20260923T072455614249Z.jsonl" `
  --source-manifest "$dataset\data\annotations\manifests\sroie-train-20260923T072550009854Z.jsonl" `
  --crop-manifest "$dataset\data\crops\sroie\bffe40c26759\train\20260923T072455614249Z\manifest.jsonl" `
  --crop-manifest "$dataset\data\crops\sroie\bffe40c26759\train\20260923T072550009854Z\manifest.jsonl" `
  --output-root artifacts\synthetic-degraded-ocr-b1b-20261002 `
  --max-crops 10 --severity severe

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED = '1'
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED_ENHANCEMENT = '1'
$env:DEPLOYSHARP_PADDLEOCR_DEGRADED_ROOT = (Resolve-Path 'artifacts\synthetic-degraded-ocr-b1b-20261002').Path
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Debug -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrSyntheticDegradedIntegrationTests' `
  --logger 'console;verbosity=normal'
```

The compact summary is [synthetic-degraded-b1b-20261002-summary.json](synthetic-degraded-b1b-20261002-summary.json). Full per-row evidence is available for [ORT](synthetic-degraded-b1b-20261002-onnxruntime.json), [OpenVINO](synthetic-degraded-b1b-20261002-openvino.json), [ORT enhancement](synthetic-degraded-b2b3-20261002-onnxruntime.json) and [OpenVINO enhancement](synthetic-degraded-b2b3-20261002-openvino.json). Dataset images and labels are not copied into the repository or a model Release.
