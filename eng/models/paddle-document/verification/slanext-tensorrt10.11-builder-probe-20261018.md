# SLANeXt TensorRT 10.11 Builder probe (2026-10-18)

## Scope

This is a build-only check using DeploySharp's TensorRT API 10 bridge. It re-runs the four exact SLANeXt ONNX artifacts already used for the ORT/OpenVINO and TensorRT 11 investigations: the official wired/wireless graphs and their alpha-renamed OpenVINO compatibility graphs. Each artifact is SHA-256 checked before the Builder is called. The optimization profile is `x=[1,3,512,512]` minimum, `[2,3,512,512]` optimum, and `[2,3,512,512]` maximum.

## Environment

- Windows 11, build `10.0.26200`; AMD Ryzen 7 5800H; NVIDIA GeForce RTX 3060 Laptop GPU, driver `576.02`.
- TensorRT `10.11.0.33-cu12`, DeploySharp API line `10`, bridge package `JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge` `4.0.0`.
- CUDA Toolkit `12.9.41`; cuDNN `9.22.0.52`.
- TensorRT DLL roots, CUDA/cuDNN roots, bridge path, probe profile, exact model hashes and native Builder diagnostics are in the [machine-readable report](slanext-tensorrt10.11-builder-probe-20261018.json).

## Results

| Artifact | SHA-256 | Result |
|---|---|---|
| `paddle-table/slanext-wired` (official) | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` | Unsupported; empty Engine (`DS-TRT-5010`) |
| `paddle-table/slanext-wireless` (official) | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | Unsupported; empty Engine (`DS-TRT-5010`) |
| `paddle-table/slanext-wired-openvino-compat` (derived compatibility graph) | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` | Unsupported; empty Engine (`DS-TRT-5010`) |
| `paddle-table/slanext-wireless-openvino-compat` (derived compatibility graph) | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | Unsupported; empty Engine (`DS-TRT-5010`) |

All four graphs reached the TensorRT Builder, but `buildSerializedNetwork` returned a null object. TensorRT reports the same recurrence contract error for each: the first recurrence input is scalar (`[]`), while the recurrence-body value is `[-1,8]`. No serialized Engine was produced. The alpha-renaming that makes the derived graphs importable by OpenVINO does not resolve this TensorRT recurrence shape requirement.

## Boundary and next action

This establishes the same exact-artifact blocker on TensorRT `10.11.0.33-cu12` / API 10 that had already been observed on TensorRT `11.0.0.114-cu12` / API 11. It does not establish that every TensorRT 10.x build rejects every SLANeXt export. There was no TensorRT Session, Decoder, HTML parity, table-quality evaluation, or latency measurement; keep these four TensorRT combinations marked unsupported for the tested runtimes. Do not spend more time trying profile changes on these identical recurrence graphs. The next meaningful route would be a model-side re-export/rewrite that gives the recurrence state a fixed compatible shape, followed by a fresh exact-SHA build and full inference/Decoder parity.

After the Builder probe, a single `nvidia-smi` snapshot reported P0, graphics/memory clocks `1425/7000 MHz`, 4% GPU utilization, and 1039 MiB memory use. This was collected after the test, not during it, and does not establish a locked-clock or peak-performance run.

For comparison, see the earlier [original-graph TensorRT 11 probe](slanext-tensorrt-builder-probe-20261014.md) and [OpenVINO-compatibility TensorRT 11 probe](slanext-openvino-compat-tensorrt11-builder-probe-20261012.md).

## Reproduction

From the repository root, configure `JYPPX_NATIVE_BRIDGE_PATH`, `JYPPX_TENSORRT_ROOT`, `JYPPX_CUDA_ROOT`, and `JYPPX_CUDNN_ROOT` for the matching TensorRT 10.11/CUDA 12.9/cuDNN 9.22 installation, then run:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_PROBE = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_INCLUDE_OPENVINO_COMPAT = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '10'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  -c Release --no-restore --filter FullyQualifiedName~OriginalSlanextExportsRecordBuilderBoundary
```

The opt-in test validates expected SHA-256 values before building. Set `DEPLOYSHARP_PADDLE_DOCUMENT_SLANEXT_COMPAT_MODEL_ROOT` if the two derived ONNX files are not under the default sibling directory `onnx-normalized-rerun-20260929`.
