# PicoDet layout-1x OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-layout-1x` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-layout-1x.onnx`, SHA-256 `61efac8c8d3eeee09d7d16db249eb919c1432e22d8235bd408ddffa3477cb8fd` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,800,608]`; 18 decoded candidates; 5 high-confidence candidates |
| Parity | ORT CPU: 5/5 high-confidence candidates matched by label, score ≤0.05 and IoU ≥0.80 |
| Timing | P50 **26.1031 ms**, P95 **69.0613 ms**, mean 32.7499 ms, min/max 20.8536/74.9278 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution/parity evidence, not layout accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-layout-1x-benchmark-20261018.json`](paddle-document-openvino-picodet-layout-1x-benchmark-20261018.json).
