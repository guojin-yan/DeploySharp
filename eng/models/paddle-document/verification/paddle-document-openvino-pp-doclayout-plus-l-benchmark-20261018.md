# PP-DocLayout-Plus-L OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-doc/pp-doclayout-plus-l` ONNX artifact and Paddle NMS decoder profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `pp-doclayout-plus-l.onnx`, SHA-256 `d67689f6325ec0d4cc812308dbd1c84bc801e76731c0f183297013d6b59e0048` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,800,800]`; 300 decoded candidates; 1 high-confidence detection |
| Parity | ORT CPU high-confidence count matched 1; score tolerance 0.01; coordinate tolerance 1.5 px |
| Timing | P50 **516.4424 ms**, P95 **670.7747 ms**, mean 543.0852 ms, min/max 460.3701/1012.2509 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and Paddle NMS decoding; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is exact-artifact execution and bounded parity evidence, not a dataset accuracy score or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-pp-doclayout-plus-l-benchmark-20261018.json`](paddle-document-openvino-pp-doclayout-plus-l-benchmark-20261018.json).
