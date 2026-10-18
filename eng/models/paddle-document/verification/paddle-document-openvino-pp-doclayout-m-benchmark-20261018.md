# PP-DocLayout-M OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/pp-doclayout-m` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `pp-doclayout-m.onnx`, SHA-256 `a65002c871af5c3d4415524a813f55cda4e8c5d59fe8cebd5af16a84b48e80f3` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 100 decoded candidates; 28 high-confidence candidates |
| Parity | ORT CPU: 28/28 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **57.0756 ms**, P95 **82.5714 ms**, mean 61.2301 ms, min/max 51.1577/89.8721 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-pp-doclayout-m-benchmark-20261018.json`](paddle-document-openvino-pp-doclayout-m-benchmark-20261018.json).
