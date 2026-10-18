# PicoDet layout-1x-table OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/picodet-layout-1x-table` export.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `picodet-layout-1x-table.onnx`, SHA-256 `7cf5b8c3f7e00353bdb231fec73dd73b66ea0cbf598daaf1f113bf7e6dc4af31` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,800,608]`; 3 decoded candidates; no candidate reached the 0.05 parity threshold |
| Parity | ORT CPU and OpenVINO both returned zero candidates at score ≥0.05 |
| Timing | P50 **23.1889 ms**, P95 **26.3338 ms**, mean 23.4822 ms, min/max 20.5149/27.0593 ms |

Timing includes OpenVINO execution and Paddle NMS decoding, excluding image preparation, model/session creation and the one-time ORT reference. This is exact-artifact execution evidence, not table detection accuracy or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-picodet-layout-1x-table-benchmark-20261018.json`](paddle-document-openvino-picodet-layout-1x-table-benchmark-20261018.json).
