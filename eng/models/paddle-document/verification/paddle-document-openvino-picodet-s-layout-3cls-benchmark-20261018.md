# PicoDet-S layout (3 classes) OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-s-layout-3cls` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-s-layout-3cls.onnx`, SHA-256 `f2b2c55af08bad9dab4030f86d34336f99bc7e39df9999676ce29db7e60b2fb1` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,480,480]`; 66 decoded candidates; 25 high-confidence candidates |
| Parity | ORT CPU: 25/25 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **9.4186 ms**, P95 **12.3624 ms**, mean 9.7512 ms, min/max 7.8912/14.1658 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-s-layout-3cls-benchmark-20261018.json`](paddle-document-openvino-picodet-s-layout-3cls-benchmark-20261018.json).
