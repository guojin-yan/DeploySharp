# PicoDet-S layout (17 classes) OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-s-layout-17cls` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-s-layout-17cls.onnx`, SHA-256 `f8a43b8588f1cee80acea3646738a2704c7c7f4cb73f63bc3d325016195fd496` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,480,480]`; 77 decoded candidates; 4 high-confidence candidates |
| Parity | ORT CPU: 4/4 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **10.0184 ms**, P95 **12.7814 ms**, mean 10.2662 ms, min/max 8.4852/14.1028 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-s-layout-17cls-benchmark-20261018.json`](paddle-document-openvino-picodet-s-layout-17cls-benchmark-20261018.json).
