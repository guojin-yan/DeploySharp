# PP-DocLayout-S OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/pp-doclayout-s` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `pp-doclayout-s.onnx`, SHA-256 `5254cb338d5ffb0b54c6f95b527e6e7f0c8a0cb35919c4d6e420dcb913ba592f` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,480,480]`; 100 decoded candidates; 48 high-confidence candidates |
| Parity | ORT CPU: 48/48 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **9.7144 ms**, P95 **12.9484 ms**, mean 10.1316 ms, min/max 8.6265/14.0102 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-pp-doclayout-s-benchmark-20261018.json`](paddle-document-openvino-pp-doclayout-s-benchmark-20261018.json).
