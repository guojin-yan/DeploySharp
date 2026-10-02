# PP-OCR Mobile REC dynamic Batch parity (2026-10-02)

This follow-up extends the Stage24 dynamic-batch contract from PP-OCRv5 Mobile to PP-OCRv6 Small without duplicating the test. The same ten SROIE pages and the first four source quadrilaterals per page are prepared with the OpenCV input path, padded to one valid target width per page, and submitted as a real batch of four to ORT CPU and OpenVINO CPU.

## Results

| Model | Pages / batch / crops | Input mismatches | Output-shape mismatches | Text mismatches | Max output abs diff | Max confidence diff |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PP-OCRv5 Mobile | 10 / 4 / 40 | 0 | 0 | 0 | `1.48773193359375e-4` | `7.867813e-6` |
| PP-OCRv6 Small | 10 / 4 / 40 | 0 | 0 | 0 | `4.96469438076019e-5` | `6.4969063e-6` |

Both model variants use the same batch input digest `6536d57b33eb7fb52515fcbeec7ea634fc468de85d0474c47b7e6c38c05e16cc` on the two CPU backends. The v6 Small model SHA is `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`; its dictionary SHA is `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`. The v5 model and dictionary SHAs remain in the original [v5 record](paddleocr-v5-recognition-dynamic-batch-ort-openvino-20260929.md).

## Interpretation

This proves true recognition batch dimension `B=4` and decoder parity for v5 Mobile and v6 Small on this fixed source crop set. It does not measure CER/WER because SROIE labels are word-level while this test is a crop tensor/decoder contract; it also does not establish Session-pool throughput, GPU performance, OpenCV DNN support, TensorRT support, or page-level OCR accuracy.

## Reproduction

The test keeps the v5 defaults and accepts the following overrides for another catalog-compatible REC model:

```powershell
$env:DEPLOYSHARP_STAGE24_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_STAGE24_MODEL_FLAVOR = 'v6-small'
$env:DEPLOYSHARP_STAGE24_MODEL_ROOT = 'E:\Model\paddleocr\PP-OCRv6\small'
$env:DEPLOYSHARP_STAGE24_REC_MODEL = 'E:\Model\paddleocr\PP-OCRv6\small\PP-OCRv6_small_rec_inference.onnx'
$env:DEPLOYSHARP_STAGE24_DICT = 'E:\Model\paddleocr\PP-OCRv6\small\PP-OCRv6_small_rec_dict.txt'
$env:DEPLOYSHARP_STAGE24_REC_SHA256 = '5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634'
$env:DEPLOYSHARP_STAGE24_DICT_SHA256 = '769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6'
$env:DEPLOYSHARP_STAGE24_EVIDENCE_PATH = Join-Path (Get-Location) 'artifacts\paddleocr-v6-small-recognition-dynamic-batch-ort-openvino-20261002.json'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage24PaddleOcrRecognitionBatchParityTests'
```

Machine-readable records:

- [PP-OCRv5 Mobile JSON](paddleocr-v5-mobile-recognition-dynamic-batch-ort-openvino-20261002.json)
- [PP-OCRv6 Small JSON](paddleocr-v6-small-recognition-dynamic-batch-ort-openvino-20261002.json)
