# PP-OCR core seven-model three-image CPU backend check (2026-10-23)

## Result

All 21 model/image pairs completed the full `det → crop → cls/orientation → rec → merge` pipeline on both ONNX Runtime CPU and OpenVINO CPU. The image and model artifact identities match for `21/21`; detected and recognized region counts match for `21/21`; ordered text SHA-256 fingerprints match for `21/21`. The canonical `OcrResult` SHA-256 matches for `0/21`, so this is text/count parity, **not** full result parity.

| Model | demo_1 regions | demo_2 regions | demo_3 regions | Total per backend | Text SHA parity | Canonical result SHA parity |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| v4-mobile | 23 | 57 | 26 | 106 | 3/3 | 0/3 |
| v4-server | 26 | 65 | 31 | 122 | 3/3 | 0/3 |
| v5-mobile | 25 | 52 | 27 | 104 | 3/3 | 0/3 |
| v5-server | 25 | 60 | 36 | 121 | 3/3 | 0/3 |
| v6-medium | 25 | 64 | 44 | 133 | 3/3 | 0/3 |
| v6-small | 24 | 59 | 27 | 110 | 3/3 | 0/3 |
| v6-tiny | 25 | 47 | 27 | 99 | 3/3 | 0/3 |

## Device and runtime

Remote host: Ubuntu 22.04.5 LTS, x64, .NET 10.0.9; host GPU is NVIDIA RTX 2060. Both runs used CPU execution only. ORT native runtime: 1.28.0. OpenVINO runtime: 2026.3.1. No GPU execution, frequency tuning, or GPU performance was tested.

Each model/image pair was run once with zero warmups. Timing starts at `OcrPipeline.Run` after image input preparation and backend Session creation; reported elapsed values are diagnostic only and must not be used as a latency ranking or steady-state benchmark.

Source commit recorded by both runs: `16facb3`; both source trees were dirty. The respective test-source SHA-256 values are recorded in the JSON evidence. Input, detector, classifier, recognizer and dictionary SHA-256 values are also retained per pair.

## Parity boundary

The text fingerprint is SHA-256 over the ordered recognized strings joined with newline separators. Matching text and region counts do not prove polygon, confidence, orientation, token, or result metadata equality; all 21 canonical result fingerprints differ. The demo images have no manually verified transcription annotations, so this run does not calculate CER/WER, detection recall, or accuracy. Follow-up should inspect exact per-field drift on controlled model/image pairs before describing the backends as result-equivalent.

## Reproduction

On a machine with the listed models and images, set `DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL=1` and/or `DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1`, configure `DEPLOYSHARP_PADDLEOCR_ROOT` and the backend-specific `*_IMAGES` semicolon-delimited list, then run the corresponding `PaddleOcrAllCorePipeline*IntegrationTests` opt-in test. Configure `DEPLOYSHARP_PADDLEOCR_ORT_REPORT` or `DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT` to emit raw evidence JSON. Compare a new report pair with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrCorePipelineReports.ps1`.

Machine-readable pair report: [`paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.json`](paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.json). Source reports: [ORT](paddleocr-core-seven-models-ort-three-image-linux-rtx2060-20261023.json) and [OpenVINO](paddleocr-core-seven-models-openvino-three-image-linux-rtx2060-20261023.json).
