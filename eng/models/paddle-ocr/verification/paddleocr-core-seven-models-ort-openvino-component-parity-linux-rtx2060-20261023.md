# PP-OCR core seven-model three-image CPU backend check (2026-10-23)

## Result

All 21 model/image pairs completed the full `det → crop → cls/orientation → rec → merge` pipeline on both ONNX Runtime CPU and OpenVINO CPU. The image and model artifact identities match for `21/21`; detected and recognized region counts match for `21/21`; ordered text SHA-256 fingerprints match for `21/21`.
The canonical `OcrResult` SHA-256 comparison is **not valid for these reports**: matching character-set ID/version/hash metadata is absent. Raw hash values may differ because the hash includes that metadata; they are not counted as result differences, and canonical result parity remains unmeasured.

Exact component fingerprints: geometry `n/a (0 comparable)` match; recognition (confidence, character-set provenance and CTC token trace) `n/a (0 comparable)` match. Exact CTC token decisions: `n/a (0 comparable)`. Numeric tolerances are `0.01` source pixels for coordinates, `0.001` for detector scores and `0.001` for recognition/token confidence; geometry is within tolerance for `n/a (0 comparable)` and recognition for `n/a (0 comparable)`. Maximum absolute deltas: coordinates `n/a`, detector score `n/a`, recognition confidence `n/a`, token confidence `n/a`.

| Model | demo_1 regions | demo_2 regions | demo_3 regions | Total per backend | Text | Geometry tolerance | Token decisions | Recognition tolerance | Canonical SHA |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| v4-mobile | 23 | 57 | 26 | 106 | 3/3 | n/a | n/a | n/a | n/a |
| v4-server | 26 | 65 | 31 | 122 | 3/3 | n/a | n/a | n/a | n/a |
| v5-mobile | 25 | 52 | 27 | 104 | 3/3 | n/a | n/a | n/a | n/a |
| v5-server | 25 | 60 | 36 | 121 | 3/3 | n/a | n/a | n/a | n/a |
| v6-medium | 25 | 64 | 44 | 133 | 3/3 | n/a | n/a | n/a | n/a |
| v6-small | 24 | 59 | 27 | 110 | 3/3 | n/a | n/a | n/a | n/a |
| v6-tiny | 25 | 47 | 27 | 99 | 3/3 | n/a | n/a | n/a | n/a |

## Device and runtime

Host OS: `Ubuntu 22.04.5 LTS`; CPU: `` (`` logical processors); process architecture: `X64`; .NET: `10.0.9`. Both runs used CPU execution only. `ONNX Runtime CPU` native runtime: `1.28.0`; `OpenVINO CPU` runtime: `2026.3.1`. No GPU execution, frequency tuning, or GPU performance was tested.

Each model/image pair was run once with zero warmups. Timing starts at `OcrPipeline.Run` after image input preparation and backend Session creation; reported elapsed values are diagnostic only and must not be used as a latency ranking or steady-state benchmark.

Source commits: ORT `16facb3`; OpenVINO `16facb3`. Source-tree dirty: ORT `true`; OpenVINO `true`. Test-source SHA-256, input and model artifact SHA-256 values, and per-pair component summaries are in the machine-readable JSON evidence.

## Parity boundary

The text fingerprint is SHA-256 over the ordered recognized strings joined with newline separators. Matching text and region counts do not prove polygon, confidence, orientation, or token equality. The historical Linux reports used backend-specific character-set IDs (`external.<model>` for ORT and `external.openvino.<model>` for OpenVINO), which participate in the canonical result SHA; those reports also lack matching character-set provenance fields and cannot be compared canonically. The new Windows reports record matching character-set ID/version/SHA metadata, so their canonical result hashes are comparable. The demo images have no manually verified transcription annotations, so this run does not calculate CER/WER, detection recall, or accuracy.

A canonical result SHA is a bit-exact fingerprint, not a tolerance-based parity verdict: tiny backend floating-point differences change the hash. Read it together with the explicit geometry/confidence tolerances and token-decision comparison above. In particular, a hash mismatch alongside all tolerance checks passing means numerically close component parity, not identical binary outputs.

The historical Linux RTX 2060 pair is a separate run: its three image SHA-256 values differ from this Windows set, and its per-region character-set identity/version/hash fields are absent. The comparator therefore marks those canonical hashes not comparable; do not interpret them as an ORT-versus-OpenVINO output difference or as a cross-device parity result.

## Reproduction

On a machine with the listed models and images, set `DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL=1` and/or `DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1`, configure `DEPLOYSHARP_PADDLEOCR_ROOT` and the backend-specific `*_IMAGES` semicolon-delimited list, then run the corresponding `PaddleOcrAllCorePipeline*IntegrationTests` opt-in test. Configure `DEPLOYSHARP_PADDLEOCR_ORT_REPORT` or `DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT` to emit raw evidence JSON. Compare a new report pair with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrCorePipelineReports.ps1`.

Machine-readable pair report: [paddleocr-core-seven-models-ort-openvino-component-parity-linux-rtx2060-20261023.json](paddleocr-core-seven-models-ort-openvino-component-parity-linux-rtx2060-20261023.json). Source reports: [ORT](paddleocr-core-seven-models-ort-three-image-linux-rtx2060-20261023.json) and [OpenVINO](paddleocr-core-seven-models-openvino-three-image-linux-rtx2060-20261023.json).
