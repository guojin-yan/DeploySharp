# PicoDet-L layout (3 classes) OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-l-layout-3cls` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-l-layout-3cls.onnx`, SHA-256 `34d873d7ec7a77448b85ae41db5785bfae0c5dda483c03c177189ebd086b1ce4` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 76 decoded candidates; 22 high-confidence candidates |
| Parity | ORT CPU: 22/22 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **53.0734 ms**, P95 **62.0237 ms**, mean 53.8558 ms, min/max 45.3351/69.1978 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-l-layout-3cls-benchmark-20261018.json`](paddle-document-openvino-picodet-l-layout-3cls-benchmark-20261018.json).
