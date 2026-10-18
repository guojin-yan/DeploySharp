# PP-OCRv4 server seal OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-seal/ppocrv4-server` ONNX artifact and `PaddleDocumentSealDecoder` profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c` |
| Model | `ppocrv4-server-seal-det.onnx`, SHA-256 `f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,224,224]`; output 224×224 mask; 1 decoded region on this sample |
| Parity | ORT reference matched mask shape and decoder contract; raw probability-map numerical parity is not claimed |
| Timing | P50 **126.2519 ms**, P95 **170.5854 ms**, mean 131.3420 ms, min/max 103.2995/188.4892 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and the seal decoder; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is execution/shape evidence, not seal recall or accuracy.

Machine-readable samples: [`paddle-document-openvino-ppocrv4-server-seal-det-benchmark-20261018.json`](paddle-document-openvino-ppocrv4-server-seal-det-benchmark-20261018.json).
