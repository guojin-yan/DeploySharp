# RT-DETR-H layout (17 classes) OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-doc/rt-detr-h-layout-17cls` ONNX artifact and Paddle NMS decoder profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `rt-detr-h-layout-17cls.onnx`, SHA-256 `556ba39ce91419069135e8446e155edda3d5cd475632d6ea5cec0f4dc072b41a` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; 300 decoded candidates; 1 high-confidence detection |
| Parity | ORT CPU high-confidence count matched 1; score tolerance 0.01; coordinate tolerance 1.5 px |
| Timing | P50 **1434.2061 ms**, P95 **1974.4991 ms**, mean 1482.5807 ms, min/max 1266.3960/2087.2202 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and Paddle NMS decoding; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is exact-artifact execution and bounded parity evidence, not a dataset accuracy score or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-rt-detr-h-layout-17cls-benchmark-20261018.json`](paddle-document-openvino-rt-detr-h-layout-17cls-benchmark-20261018.json).
