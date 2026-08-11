# PaddleOCR three-model clean consumer

This package-only consumer installs Core, Visual, Visual.OpenCV, one managed backend, and application-selected native runtime packages. It never copies models, dictionaries, or validation images into the repository or NuGet packages.

The following variables must point to authorized external files:

- `DEPLOYSHARP_STAGE20_IMAGE`
- `DEPLOYSHARP_STAGE20_OCR_DET_MODEL`
- `DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL`
- `DEPLOYSHARP_STAGE20_OCR_REC_MODEL`
- `DEPLOYSHARP_STAGE20_OCR_DICT`

Missing files or native runtimes print `DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_SKIP`. A complete run prints `DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_OK`.
