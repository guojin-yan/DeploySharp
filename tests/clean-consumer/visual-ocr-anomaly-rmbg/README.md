# Visual OCR, anomaly, and RMBG clean consumer

This package-only consumer has no project references. It installs Core, Visual, Visual.OpenCV, the ONNX Runtime backend, and application-selected native runtime packages. External models, dictionaries, and images are never copied into the repository or NuGet packages.

The following variables must point to audited external files:

- `DEPLOYSHARP_STAGE19_IMAGE`
- `DEPLOYSHARP_STAGE19_OCR_DET_MODEL`
- `DEPLOYSHARP_STAGE19_OCR_REC_MODEL`
- `DEPLOYSHARP_STAGE19_OCR_DICT`
- `DEPLOYSHARP_STAGE19_ANOMALIB_PADIM_MODEL`
- `DEPLOYSHARP_STAGE19_BRIA_RMBG14_MODEL`

Missing files or native runtimes print `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_SKIP`. A complete run prints `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_OK`.
