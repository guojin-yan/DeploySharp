# PP-DocLayout-L OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-doc/pp-doclayout-l` ONNX artifact and Paddle NMS decoder profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `pp-doclayout-l.onnx`, SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 300 decoded candidates; 1 high-confidence detection |
| Parity | ORT CPU high-confidence count matched 1; score tolerance 0.01; coordinate tolerance 1.5 px |
| Timing | P50 **348.3297 ms**, P95 **772.7077 ms**, mean 409.1420 ms, min/max 297.5360/1329.4623 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and Paddle NMS decoding; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is exact-artifact execution and bounded parity evidence, not a dataset accuracy score or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-pp-doclayout-l-benchmark-20261018.json`](paddle-document-openvino-pp-doclayout-l-benchmark-20261018.json).
