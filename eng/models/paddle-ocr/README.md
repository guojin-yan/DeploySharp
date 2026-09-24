# PaddleOCR model acquisition and release

This is the reproducible acquisition entry point for the core PP-OCR model families used by DeploySharp. It covers the v4/v5 detector, recognizer and classifier contracts and the v6 tiny/small/medium detector and recognizer models. PaddleOCR v6 does not publish a separate versioned classifier; the official PP-LCNet text-line orientation models are shared by the current pipeline and are listed as the v5 classifier entries.

The model files remain outside Git. The checked-in catalog records the official archive URL, expected tensor names, dictionary and the DeploySharp profile factory. `Acquire-PaddleOcrModels.ps1` downloads the official Paddle inference archives, extracts and converts missing graphs with `paddle2onnx`, and writes a source/ONNX/dictionary SHA-256 record for each entry.

```powershell
& .\scripts\Acquire-PaddleOcrModels.ps1 `
  -All `
  -ModelRoot E:\Model\paddleocr `
  -Python E:\Model\PaddleDocument\paddle3\python.exe
```

The existing v4 mobile, v5 mobile and v6 ONNX files are reused when present; source archives are still recorded so the provenance is reproducible. A `conversion-blocked` record is never treated as runtime support. The v6 recognition graphs use the same `CreateRecognition` CTC contract as v4/v5, with their tier-specific dictionaries and class counts.

## Code support versus asset status

All 15 catalog rows have a corresponding DeploySharp profile factory: `CreateDetection`, `CreateRecognition`, `CreateLegacyClassification`, or `CreateTextLineOrientationClassification`. This is code-level contract support. It does not claim that every row has been run on every backend. The acquisition summary separately records `onnx-existing`, `onnx-converted`, `download-failed`, or `conversion-blocked`.

The independent release publisher creates one stable release named **DeploySharp PaddleOCR Models** with tag `models-paddleocr`. The tag is intentionally not date-based; later uploads are recorded in the release notes and asset manifest. Release assets contain ONNX files, dictionaries, ModelPack-style manifests and `SHA256SUMS`; Paddle source archives stay in the acquisition cache unless explicitly requested.
