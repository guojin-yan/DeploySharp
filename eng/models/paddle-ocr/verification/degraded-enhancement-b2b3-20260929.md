# B2/B3 real-crop enhancement retry smoke (2026-09-29)

This is a controlled comparison using the four parent-linked SROIE crops × six B1b conditions (24 variants). It exercises the actual full OCR path for the enhancement candidate, while retaining the original recognizer-only text as the baseline. The candidate is `ContrastNormalize` with `ConfidenceGain`, threshold `1.0` and minimum gain `0`, so the run measures routing behavior rather than proposing production thresholds.

| Backend | Variants | Candidate available | Candidate selected | No detected region | Mean original case-folded CER (candidate rows) | Mean candidate CER |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 24 | 17 | 13 | 7 | 0.4525% | 0.00% |
| OpenVINO CPU | 24 | 17 | 13 | 7 | 0.4525% | 0.00% |

For selected candidates, mean original case-folded CER was `0.5917%` and candidate CER was `0%` on both backends. This is a very small, source-linked word-crop sample; seven variants could not be evaluated because full-page detection found no region. The result is useful for verifying candidate retention, selection decision, source SHA and elapsed-time reporting, but it is not sufficient to enable enhancement by default or claim accuracy improvement.

The machine-readable result JSON is emitted by `PaddleOcrSyntheticDegradedIntegrationTests` when `DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED_ENHANCEMENT=1`; it records original/candidate text, confidence, CER/WER, decision, image/crop SHA and candidate elapsed time. ORT/OpenVINO candidate decisions matched in all 24 rows.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED = '1'
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED_ENHANCEMENT = '1'
$env:DEPLOYSHARP_PADDLEOCR_DEGRADED_ROOT = (Resolve-Path 'artifacts\synthetic-degraded-ocr-b1b-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticDegradedIntegrationTests
```

The B2/B3 plan remains open for a larger, harder low-quality set, false-correction analysis and a cost/benefit threshold chosen from real labels.
