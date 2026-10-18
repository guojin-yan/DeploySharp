# SLANeXt wireless OpenVINO CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact derived OpenVINO compatibility graph `paddle-table/slanext-wireless-openvino-compat`.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `table_recognition.jpg`, SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Official source graph | `slanext-wireless.onnx`, SHA-256 `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` |
| OpenVINO graph | `slanext-wireless-openvino-compat.onnx`, SHA-256 `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` |
| Backend | OpenVINO CPU (`OpenVINO C# API 3.3.1 / runtime 2026.2.1`) |
| Contract / parity | Input `[1,3,512,512]`; 24 structure tokens, 13 cells; exact HTML SHA matches ORT source graph (`53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557`) |
| Timing | P50 **794.1322 ms**, P95 **962.2685 ms**, mean 813.7268 ms, min/max 730.4874/1186.5284 ms |

Timing includes OpenVINO execution and `PaddleDocumentTableStructureDecoder`, excluding image preparation, model/session creation and the one-time ORT reference. This is an exact-graph decoder/parity and latency observation on one host—not table dataset accuracy, cell recall, cross-device ranking, or support for the original graph in the OpenVINO importer.

Machine-readable samples: [`paddle-document-openvino-slanext-wireless-benchmark-20261018.json`](paddle-document-openvino-slanext-wireless-benchmark-20261018.json).
