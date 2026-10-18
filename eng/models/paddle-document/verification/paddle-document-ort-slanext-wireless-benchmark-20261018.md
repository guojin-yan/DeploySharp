# Wireless SLANeXt ONNX Runtime CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the official `paddle-table/slanext-wireless` graph.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `table_recognition.jpg`, SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Source model | `slanext-wireless.onnx`, SHA-256 `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` |
| Backend | ONNX Runtime CPU (`Microsoft.ML.OnnxRuntime 1.28.0`) |
| Contract | Input `[1,3,512,512]`; 24 structure tokens, 13 decoded cells, finite scores |
| OpenVINO parity | Official ORT HTML SHA equals the OpenVINO-compatible graph HTML SHA (`53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557`) |
| Timing | P50 **937.5171 ms**, P95 **1024.2289 ms**, mean 944.0354 ms, min/max 890.4338/1137.1277 ms |

The measured path uses the official source graph and includes ONNX Runtime CPU execution plus `PaddleDocumentTableStructureDecoder`. It excludes image preparation, model/session creation and the one-time OpenVINO execution of the derived compatibility graph. The OpenVINO graph is used only as a bounded structure/markup reference because the original Loop graph remains importer-blocked in the current OpenVINO runtime.

This is an exact-artifact execution and markup-parity record, not table-dataset accuracy, cell recall, cross-device ranking or OpenVINO support for the original graph.

Machine-readable samples: [`paddle-document-ort-slanext-wireless-benchmark-20261018.json`](paddle-document-ort-slanext-wireless-benchmark-20261018.json).

## Reproduction

Place the official `slanext-wireless.onnx` and the registered derived compatibility graph under the paths used by the test. Use `E:\Model\PaddleDocument\validation\table_recognition.jpg`. From the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK = '1'
$env:DEPLOYSHARP_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleSlaNextWirelessOrtBenchmarkTests
```

The JSON record contains runtime/device metadata, source and compatibility graph hashes, all 50 measured samples, percentiles and the exact markup fingerprint. It does not promote the derived compatibility graph to support for the original graph in OpenVINO or TensorRT.
