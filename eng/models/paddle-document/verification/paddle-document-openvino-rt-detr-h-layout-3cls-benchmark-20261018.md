# RT-DETR-H layout (3 classes) OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-doc/rt-detr-h-layout-3cls` ONNX artifact and Paddle NMS decoder profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `rt-detr-h-layout-3cls.onnx`, SHA-256 `e003528247ea0324eba25c94127a1ff5ef48f8a83ae725a8f42f8f0a1d551d8b` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 300 decoded candidates; 1 high-confidence detection |
| Parity | ORT CPU high-confidence count matched 1; score tolerance 0.01; coordinate tolerance 1.5 px |
| Timing | P50 **1489.2549 ms**, P95 **2120.9085 ms**, mean 1590.8417 ms, min/max 1267.3380/2426.7075 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and Paddle NMS decoding; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is exact-artifact execution and bounded parity evidence, not a dataset accuracy score or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-rt-detr-h-layout-3cls-benchmark-20261018.json`](paddle-document-openvino-rt-detr-h-layout-3cls-benchmark-20261018.json).
