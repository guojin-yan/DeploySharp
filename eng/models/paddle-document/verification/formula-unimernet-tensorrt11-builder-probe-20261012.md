# UniMERNet TensorRT 11 builder probe

Date: 2026-10-12
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)
Runtime: TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`
Tool: `trtexec --skipInference --noTF32 --builderOptimizationLevel=0`

## Scope and result

This is a precise builder probe for the official `paddle-formula/unimernet` ONNX artifact. The exported input was fixed to `x=[1,1,672,192]` for min/opt/max. TensorRT loaded ONNX IR `0.0.8`/opset `18` and reached `If.0` (`p2o.pd_op.set_value_.0.0`), then failed at `onnxOpCheckers.cpp:1090 checkConcatFromSequence`. No Engine was created and no inference, formula Decoder, quality result, or timing sample exists.

| Field | Value |
|---|---|
| ONNX SHA-256 | `1d64fafa0161f153dafe40823e97c4b05103030509dd6b286d7c8d4a11b068ab` |
| Size | `1,842,024,100` bytes |
| Input profile | `x=[1,1,672,192]` for min/opt/max |
| Parser boundary | `If.0`, `checkConcatFromSequence` |
| Engine | not created |
| Inference/decoder | not run |

## Boundary

This exact UniMERNet graph is independently marked unsupported for TensorRT `11.0.0.114-cu12` (`✗⁵`). Its `ConcatFromSequence` importer boundary is different from the FormulaNet `BitwiseAnd` boundary and must not be merged into the FormulaNet cause. The result is limited to this ONNX SHA and runtime; it is not a universal TensorRT-version claim or a formula-quality result. A future supported path needs a maintained export that removes or lowers the sequence construction to TensorRT-supported tensor operations, followed by token/Decoder parity validation.

The machine-readable record is [`formula-unimernet-tensorrt11-builder-probe-20261012.json`](formula-unimernet-tensorrt11-builder-probe-20261012.json).
