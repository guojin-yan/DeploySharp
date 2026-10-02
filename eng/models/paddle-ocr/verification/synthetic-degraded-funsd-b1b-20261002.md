# B1b controlled FUNSD degradation extension (2026-10-02)

This is a separate word-crop stress record from the SROIE degradation evidence. It uses 32 parent-linked FUNSD test pages/crops and six deterministic `severe` conditions. The FUNSD source is useful for a larger, more varied crop set, but its annotations remain word/entity-level and are not line/page truth.

## Protocol and provenance

- Dataset: `FUNSD`, pinned source revision `c31735649e4f441bcbb4fd0f379574f7520b42286e80b01d80b445649d54761f`.
- Source annotation manifest SHA-256: `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`.
- Source crop manifest SHA-256: `25a95b2955cf3e58db5ad46f59ba6032dd5bb69525652f24a820f50588661074`.
- Selection: 32 parent pages and 32 word crops from the local `test/full-001` crop manifest. Each row retains the source parent, instance, source crop SHA and degraded image SHA.
- Conditions: `normal`, `low-contrast`, `blur`, `noise`, `shadow` and JPEG quality 15, all generated with `severity=severe`.
- Records: 192 (32 crops x 6 conditions); generated manifest SHA-256 `bd6e38a9d422f0b3ee083a6ce657fa0e250cb3ad6def2adeff42824e8ebff75b`.
- Model and dictionary are the same PP-OCRv6 Small artifacts used in the SROIE extension: detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`, recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`, dictionary SHA-256 `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Backends: ONNX Runtime CPU and OpenVINO CPU.
- The recognizer-only path uses `OcrPipeline.RecognizeOnlyAsync` with a known quadrilateral. The enhancement path separately runs the full detector plus one `ContrastNormalize` candidate with `ConfidenceGain(minimumGain=0)`.

## Recognizer-only results

Both backends completed all 192 records and produced identical recognized text for all 192 corresponding rows. Case-folded quality and empty-output counts were identical; maximum confidence delta was `1.01e-5`.

| Condition | Mean case-folded CER | Mean case-folded WER | Empty outputs / 32 | Mean confidence |
| --- | ---: | ---: | ---: | ---: |
| normal | 21.24% | 33.33% | 2 | 0.8275 |
| low-contrast | 21.24% | 33.33% | 2 | 0.8275 |
| blur | 97.40% | 98.96% | 18 | 0.1510 |
| noise | 24.40% | 42.71% | 0 | 0.8271 |
| shadow | 20.90% | 31.25% | 5 | 0.8100 |
| JPEG q15 | 22.55% | 39.58% | 1 | 0.8220 |

The high error rate is a property of this word-crop/model selection, not a backend mismatch. It also shows why the earlier four-page SROIE sample cannot be used as a general degradation conclusion: the source and annotation population matter. These are word-crop means, not FUNSD page/line accuracy or a robustness score.

## Enhancement candidate results

The full-pipeline run detected no region for `145/192` calls, so those calls cannot evaluate a crop candidate. Of the remaining 47 calls, 45 produced a candidate and 33 were rejected as `InsufficientGain`; 14 were selected by the confidence-only policy.

| Backend | No region | Candidate available | Selected | Insufficient gain | Mean original CER (candidate rows) | Mean candidate CER | Selected CER before -> after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 145 | 45 | 14 | 33 | 20.62% | 18.09% | 36.70% -> 37.43% |
| OpenVINO CPU | 145 | 45 | 14 | 33 | 20.62% | 18.09% | 36.70% -> 37.43% |

The aggregate candidate subset improves numerically, but the rows selected by the confidence policy become worse (`36.70% -> 37.43%`). This is a concrete false-selection boundary: confidence alone is not a quality oracle. The candidate decision and text were consistent across both backends, but the result does not justify enabling enhancement by default. Mean diagnostic elapsed time was about `97.89 ms` (ORT) and `38.33 ms` (OpenVINO), not a P50/P95 benchmark.

B1b remains open for a quality-controlled line/page set and a larger analysis of false corrections. B2/B3 remain open for a selection policy that is evaluated against real labels and business constraints; no automatic enhancement is enabled by this record.

## Reproduction

```powershell
$python = 'E:\Model\PaddleDocument\paddle3\python.exe'
$dataset = 'F:\OCRBenchmarkTesting'
& $python .\eng\models\paddle-ocr\scripts\Generate-SyntheticDegradedOcrCase.py `
  --dataset-root $dataset `
  --source-manifest "$dataset\data\annotations\manifests\funsd-test-full-001.jsonl" `
  --crop-manifest "$dataset\data\crops\funsd\c31735649e4f\test\full-001\manifest.jsonl" `
  --output-root artifacts\synthetic-degraded-ocr-funsd-b1b-20261002 `
  --max-crops 32 --severity severe --id-prefix funsd

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED = '1'
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED_ENHANCEMENT = '1'
$env:DEPLOYSHARP_PADDLEOCR_DEGRADED_ROOT = (Resolve-Path 'artifacts\synthetic-degraded-ocr-funsd-b1b-20261002').Path
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Debug -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrSyntheticDegradedIntegrationTests' `
  --logger 'console;verbosity=normal'
```

The compact [summary JSON](synthetic-degraded-funsd-b1b-20261002-summary.json) and four raw per-row records ([ORT](synthetic-degraded-funsd-b1b-20261002-onnxruntime.json), [OpenVINO](synthetic-degraded-funsd-b1b-20261002-openvino.json), [ORT enhancement](synthetic-degraded-funsd-b2b3-20261002-onnxruntime.json), [OpenVINO enhancement](synthetic-degraded-funsd-b2b3-20261002-openvino.json)) retain the exact source/model/image evidence. Dataset images and labels are not copied into the repository or a Release.
