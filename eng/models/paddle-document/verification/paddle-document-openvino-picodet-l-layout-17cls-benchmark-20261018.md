# PicoDet-L layout (17 classes) OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-l-layout-17cls` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-l-layout-17cls.onnx`, SHA-256 `b3e1b8deaa9538a76418083274df9acf07232cccb30ba6ab957a77a2d73cecb3` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 63 decoded candidates; 11 high-confidence candidates |
| Parity | ORT CPU: 11/11 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **57.4080 ms**, P95 **125.1531 ms**, mean 66.4762 ms, min/max 50.9248/164.9556 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-l-layout-17cls-benchmark-20261018.json`](paddle-document-openvino-picodet-l-layout-17cls-benchmark-20261018.json).
