# B1b controlled OCR degradation evidence (2026-09-29)

This record uses four real, parent-linked SROIE word crops from `F:\OCRBenchmarkTesting` and creates six deterministic variants per crop: `normal`, `low-contrast`, `blur`, `noise`, `shadow` and JPEG quality 35. The source text, source crop SHA, parent image/instance and pinned SROIE revision remain in every output row. The generated images are local test artifacts and are not committed or uploaded.

## Protocol

- Generator: `eng/models/paddle-ocr/scripts/Generate-SyntheticDegradedOcrCase.py`.
- Source: SROIE distributor revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`, four word crops, 24 variants.
- Model: PP-OCRv6 Small detector/recognizer; dictionary SHA-256 `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Backends: ONNX Runtime CPU and OpenVINO CPU.
- Each variant is run through `OcrPipeline.RecognizeOnlyAsync`; detector execution is disabled by the API path, while a real detector session is retained by the pipeline contract.
- Evidence records source and degraded image SHA, input tensor SHA, condition, text, confidence, width diagnostics and exact/case-folded CER/WER.
- The source selection is SROIE word-level and already marked smoke-only by the benchmark repository. Results are not line-level or full-page quality claims.

## Results

Both backends completed `24/24` records with no empty outputs and byte-identical text per condition/crop. Mean case-folded metrics (four crops; exact source text is also retained) were:

| Condition | Mean CER | Mean WER |
| --- | ---: | ---: |
| normal | 80.63% | 100.00% |
| low-contrast | 80.63% | 100.00% |
| blur | 83.75% | 125.00% |
| noise | 80.63% | 100.00% |
| shadow | 80.63% | 100.00% |
| JPEG q35 | 81.46% | 104.17% |

These high errors are a diagnostic finding: the selected v6 Small recognizer and this small word-crop setup do not form a quality baseline suitable for claiming degradation gains. They also show that confidence remains high while text error is high, so confidence alone must not drive B2/B3 candidate acceptance. The controlled variants prove provenance, deterministic corruption, backend execution and evidence capture only.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SyntheticDegradedOcrCase.py `
  --dataset-root F:\OCRBenchmarkTesting `
  --source-manifest F:\OCRBenchmarkTesting\data\annotations\manifests\sroie-train-20260923T072455614249Z.jsonl `
  --crop-manifest F:\OCRBenchmarkTesting\data\crops\sroie\bffe40c26759\train\20260923T072455614249Z\manifest.jsonl `
  --output-root artifacts\synthetic-degraded-ocr-b1b-20260929 --max-crops 4

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED = '1'
$env:DEPLOYSHARP_PADDLEOCR_DEGRADED_ROOT = (Resolve-Path 'artifacts\synthetic-degraded-ocr-b1b-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticDegradedIntegrationTests `
  --logger 'console;verbosity=normal'
```

B1b remains open for a larger, quality-controlled low-quality set and for deciding whether any enhancement/retry strategy improves case-folded CER/WER without increasing false corrections. This 24-record smoke is deliberately not sufficient for that decision.
