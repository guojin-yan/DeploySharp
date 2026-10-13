# Original SLANeXt TensorRT Builder probe (2026-10-14)

This probe uses the DeploySharp TensorRT ONNX Builder (API 11), rather than a
prebuilt plan or `trtexec`, for the two original Paddle release graphs:

- `paddle-table/slanext-wired`
- `paddle-table/slanext-wireless`

Both graphs parse far enough for TensorRT to report the network, but
`buildSerializedNetwork` returns no serialized Engine. The native error is:

```text
IRecurrenceLayer must have inputs with same shape
first input ... has shape [] and second input ... has shape [-1,8]
```

The exact API-11 bridge maps this to `DS-TRT-5010`; no TensorRT Session, table
Decoder, HTML output, parity result or performance measurement was produced.
The machine-readable report records the full runtime strings, input profile,
model SHA-256 values and native diagnostics:
[`slanext-tensorrt-builder-probe-20261014.json`](slanext-tensorrt-builder-probe-20261014.json).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_PROBE = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '11'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  -c Release --filter FullyQualifiedName~OriginalSlanextExportsRecordBuilderBoundary
```

The probe was run on Windows with TensorRT `11.0.0.114-cu12`, CUDA `12.9`,
cuDNN `9.22`, and an NVIDIA GeForce RTX 3060 Laptop GPU. The Builder profile
was `x=[1,3,512,512]` minimum, `[2,3,512,512]` optimum and maximum. Both exact
graphs have the following result:

| Model | ONNX SHA-256 | Builder result | Error |
|---|---|---|---|
| `paddle-table/slanext-wired` | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` | unsupported; empty Engine | `DS-TRT-5010`, recurrent input shapes `[]` and `[-1,8]` |
| `paddle-table/slanext-wireless` | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | unsupported; empty Engine | `DS-TRT-5010`, recurrent input shapes `[]` and `[-1,8]` |

This is a precise boundary for the named TensorRT 11 runtime and the two
original ONNX files. It is not a universal TensorRT-version claim. The
alpha-renamed OpenVINO compatibility graphs are separate artifacts; they have
the same recurrence-shape Builder boundary in their own
[API-11 probe](slanext-openvino-compat-tensorrt11-builder-probe-20261012.md).
Neither artifact should be advertised as TensorRT-supported until a non-empty
Engine, table Decoder output, ORT parity and representative table-quality
checks all pass.
