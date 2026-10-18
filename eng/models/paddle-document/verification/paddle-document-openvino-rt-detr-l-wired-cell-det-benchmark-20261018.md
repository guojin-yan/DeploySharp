# RT-DETR-L wired-cell detector OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-table/rt-detr-l-wired-cell-det` ONNX artifact and Paddle NMS decoder profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Model\PaddleDocument\validation\table_recognition.jpg`, SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Model | `rt-detr-l-wired-cell-det.onnx`, SHA-256 `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 300 decoded candidates; 13 high-confidence detections |
| Parity | ORT CPU high-confidence count matched 13; score tolerance 0.01; coordinate tolerance 1.5 px |
| Timing | P50 **370.1529 ms**, P95 **483.6616 ms**, mean 385.0351 ms, min/max 320.2581/515.4401 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and Paddle NMS decoding; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is exact-artifact execution and bounded parity evidence, not table-structure accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-rt-detr-l-wired-cell-det-benchmark-20261018.json`](paddle-document-openvino-rt-detr-l-wired-cell-det-benchmark-20261018.json).
