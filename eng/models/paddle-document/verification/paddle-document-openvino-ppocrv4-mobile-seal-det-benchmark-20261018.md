# PP-OCRv4 mobile seal OpenVINO CPU benchmark (2026-10-18)

Formal 5-warm-up/50-measured execution evidence for the exact `paddle-seal/ppocrv4-mobile` ONNX artifact and `PaddleDocumentSealDecoder` profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c` |
| Model | `ppocrv4-mobile-seal-det.onnx`, SHA-256 `e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract | Input `[1,3,224,224]`; output 224×224 mask; 0 decoded regions on this sample |
| Parity | ORT reference matched mask shape and decoder contract; raw probability-map numerical parity is not claimed |
| Timing | P50 **5.8319 ms**, P95 **9.7357 ms**, mean 6.9977 ms, min/max 4.1952/49.4717 ms |

Timing covers synchronous `VisualPipeline.Run`, OpenVINO execution and the seal decoder; image decode/preprocessing, model/session creation and the one-time ORT reference are excluded. This is execution/shape evidence, not seal recall or accuracy.

Machine-readable samples: [`paddle-document-openvino-ppocrv4-mobile-seal-det-benchmark-20261018.json`](paddle-document-openvino-ppocrv4-mobile-seal-det-benchmark-20261018.json).
