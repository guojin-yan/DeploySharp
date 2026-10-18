# UVDoc OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/uvdoc` ONNX artifact.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `uvdoc.onnx`, SHA-256 `10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,640,640]`; decoded output `640×640×3`, all values finite |
| ORT parity | Max absolute difference `0.0730591`; mean absolute difference `0.00176066`; within declared `0.2/0.01` diagnostic bounds |
| Timing | P50 **102.5192 ms**, P95 **128.0841 ms**, mean 104.1795 ms, min/max 88.6074/134.6252 ms |

Timing includes OpenVINO execution and `PaddleDocumentUnwarpingDecoder`, excluding image preparation, model/session creation and the one-time ORT reference. The raw-output comparison is bounded numeric parity only: it does not establish pixel equivalence, visual quality or cross-device ranking.

Machine-readable samples: [`paddle-document-openvino-uvdoc-benchmark-20261018.json`](paddle-document-openvino-uvdoc-benchmark-20261018.json).
