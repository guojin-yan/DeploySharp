# HierText line-level recognizer evidence (PP-OCRv6 Small, 2026-10-10)

This record isolates the recognizer on source-linked HierText text-line crops. It avoids mixing detector recall or page-level reading order into the recognition score. The test used 32 crops from the local `sample-002`/`sample-003` validation selections, the same crop images and the same PP-OCRv6 Small ONNX/character dictionary on ONNX Runtime CPU and OpenVINO CPU.

## Results

| Backend | Rows | Exact rows | Exact rate | Reference chars | CER | Reference words | WER | Text parity vs ORT |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 32 | 19 | 59.375% | 162 | 10.4938% | 36 | 41.6667% | reference |
| OpenVINO CPU | 32 | 19 | 59.375% | 162 | 10.4938% | 36 | 41.6667% | 32/32 rows identical |

The two backends produced identical recognized text for all 32 source crops. This is a recognizer-only quality result, not a complete-page OCR accuracy claim. The sample is still a smoke selection and is not a release leaderboard.

## Provenance and reproduction

- Model: PP-OCRv6 Small detector/recognizer; detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`; recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Character dictionary SHA-256: `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- HierText annotation manifest SHA-256: `sample-002=e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7`; `sample-003=f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- Source revision: `ca0016c1338d2145045c10896cd3efa46e0bf555`; the working tree was dirty and the raw crop images/predictions remain outside Git.
- Test command:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC='1'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_MAX='32'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_OUTPUT='artifacts/hiertext-line-rec-20261010'
dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PaddleOcrHierTextLineRecognitionIntegrationTests'
```

The opt-in test is `PaddleOcrHierTextLineRecognitionIntegrationTests`; it writes backend-specific raw summaries to the configured local output directory. The checked-in machine-readable summary is [hiertext-line-recognition-v6-small-20261010.json](hiertext-line-recognition-v6-small-20261010.json).

## Boundary

HierText line crops provide line-level recognition truth, but this 32-row selection does not close the broader P2 quality gate, natural long-text gate, rotation/low-quality robustness gate, or the formal 5-warmup/50-iteration performance matrix. It also does not promote CUDA, TensorRT, OpenCV DNN, or other model variants.
