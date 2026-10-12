# SLANeXt OpenVINO-compatible graph TensorRT 11 builder probe

Date: 2026-10-12
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)
Runtime: TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`
Tool: `trtexec --skipInference --verbose --memPoolSize=workspace:4096M`

## Scope

This is a build-only probe for the two locally generated alpha-renamed SLANeXt graphs used by the OpenVINO compatibility path. It does not modify the source ONNX files, create a DeploySharp Session, run a decoder, or claim table-structure support. The probe was repeated for both wired and wireless graphs with the same TensorRT 11 runtime used by the other PP-Structure evidence.

## Results

| Graph | ONNX SHA-256 | Size (bytes) | Builder result | Failure boundary |
|---|---|---:|---|---|
| `slanext-wired-openvino-compat.onnx` | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` | 367,743,498 | blocked, empty Engine | `IRecurrenceLayer` requires equal shapes; first input `[]`, recurrence input `[-1,8]` |
| `slanext-wireless-openvino-compat.onnx` | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | 367,743,498 | blocked, empty Engine | `IRecurrenceLayer` requires equal shapes; first input `[]`, recurrence input `[-1,8]` |

Both graphs parsed successfully. TensorRT then reported:

```text
IRecurrenceLayer must have inputs with same shape.
First input p2o.pd_op.full.641.0_output has shape [] and second input
p2o.sub_block.pd_op.sigmoid.2.0 has shape [-1,8].
Created engine with size: 0 MiB.
Attempting to access an empty engine!
```

The parser therefore accepts the alpha-renamed graph, but the TensorRT 11 builder cannot lower the recurrent state contract. This is the same structural family as the original SLANeXt probe, although the exact artifact SHA-256 values are different.

## Boundary and next design

For TensorRT `11.0.0.114-cu12` on this host, both derived OpenVINO-compatible graphs are now an explicit builder blocker (`✗³`), not an unverified `△`. The status is scoped to these exact files and runtime; it is not a claim that every TensorRT release fails. A production TensorRT path would require a fixed-state export, an encoder/decoder split with an explicit recurrent-state tensor contract, or a maintained TensorRT plugin. No graph surgery was promoted to a release asset, and the OpenVINO compatibility graphs remain valid for their existing OpenVINO CPU evidence.

Machine-readable details are in [`slanext-openvino-compat-tensorrt11-builder-probe-20261012.json`](slanext-openvino-compat-tensorrt11-builder-probe-20261012.json).
