# SROIE natural-source crop enhancement diagnostic (2026-10-23)

This bounded experiment evaluates the opt-in `ConsensusConfidenceGain` policy on real SROIE receipt word crops. The source images are natural receipts, but their imaging quality is **not labelled**; this is not a natural-low-quality split. The test bypasses detection and measures recognition on source-labelled quadrilaterals, so the word-region metric must not be read as page OCR accuracy.

## Setup and reproduction

- Dataset: `jsdnrs/ICDAR2019-SROIE`, pinned distributor revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`; two pinned manifests select 10 pages and 542 valid word regions. The report records manifest and image SHA-256 values.
- Model: PP-OCRv6 Small DET/REC artifacts and character dictionary; model and dictionary SHA-256 values are recorded in the JSON evidence.
- Host: `JYPPX`, Microsoft Windows 10.0.26200, .NET 10.0.12, x64, AMD Ryzen 7 5800H, 16 logical processors.
- Backends: ONNX Runtime CPU `1.28.0` and OpenVINO CPU `2026.2.1-21919-ede283a88e3-releases/2026/2`.
- Recognition uses batch 16 and one session per stage. Three candidates run in order: `ContrastNormalize`, `GrayClahe`, `UnsharpMask`; consensus requires two matching candidate texts and uses the diagnostic threshold `minimumGain=0`.
- The measured page timings are one sequential invocation per page. They include all three candidate attempts and are diagnostic wall-clock observations, not paired baseline-vs-enhancement costs or the formal `5 warm-up + 50 measured` protocol.

To reproduce on a machine with the same pinned dataset and model assets:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_OUTPUT = (Join-Path (Get-Location) 'eng\models\paddle-ocr\verification')
$env:DEPLOYSHARP_CPU_MODEL = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim()
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore -f net10.0 -m:1 `
  --filter FullyQualifiedName~PaddleOcrSroieNaturalEnhancementIntegrationTests -c Release
```

## Results

Both CPU backends processed all `542/542` labelled word regions. Original text, all three candidate texts, enhanced final text, decision and selected candidate index matched exactly across ORT and OpenVINO for `542/542` regions.

| Backend | Selected | Selected CER better / worse / equal | Selected WER better / worse / equal | Aggregate CER edits | Aggregate WER edits | Page P50/P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 353/542 | 4 / 5 / 344 | 3 / 4 / 346 | 513/5,857 → 514/5,857 | 232/1,089 → 233/1,089 | 2414.12 / 3595.91 |
| OpenVINO CPU | 353/542 | 4 / 5 / 344 | 3 / 4 / 346 | 513/5,857 → 514/5,857 | 232/1,089 → 233/1,089 | 3485.93 / 4296.28 |

Aggregate CER changed from `8.75875%` to `8.77582%`; aggregate WER changed from `21.30395%` to `21.39578%`. Thus, although backend behavior is exactly aligned, this selected subset has one additional character edit and one additional word edit after consensus selection. The result does not show a quality gain.

Candidate-level comparisons against the original recognition (each recipe is attempted once on all 542 crops):

| Recipe | CER better / worse / equal | WER better / worse / equal |
|---|---:|---:|
| ContrastNormalize | 27 / 32 / 483 | 20 / 30 / 492 |
| GrayClahe | 13 / 42 / 487 | 13 / 36 / 493 |
| UnsharpMask | 6 / 14 / 522 | 6 / 8 / 528 |

These per-recipe counts overlap by region and are not additive. Confidence consensus can still select a text with worse labelled CER/WER; confidence is not a correctness oracle.

## Conclusion and limits

- Do not enable enhancement by default and do not infer that consensus is safe from backend parity.
- Keep B1b/B2/B3 open: image quality has no source labels, this is only a ten-page word-crop selection, and there is no independent natural-low-quality split, business-field false-correction budget, or paired formal latency/cost gate.
- The page P50/P95 values include sequential candidate execution and must not be compared as backend performance rankings; this was a diagnostic quality run, not a benchmark.
- SROIE annotations are word-level. These values are crop-level diagnostics, not official SROIE metrics, line-level/page-level accuracy, detection recall, or a release quality claim.

The machine-readable per-region evidence, provenance, runtime versions and timing samples are in [the JSON report](paddleocr-sroie-natural-source-enhancement-20261023.json). The opt-in integration test is [PaddleOcrSroieNaturalEnhancementIntegrationTests.cs](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrSroieNaturalEnhancementIntegrationTests.cs).
