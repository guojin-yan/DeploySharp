# PP-FormulaNet Plus-S TensorRT 11 builder probe

Date: 2026-10-12
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)
Runtime: TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`
Tool: `trtexec --skipInference --noTF32 --builderOptimizationLevel=0`

## Scope

This is a precise builder probe for the official `paddle-formula/pp-formulanet-plus-s` ONNX artifact. It does not modify the graph, run the formula decoder, or claim formula quality. The earlier TensorRT 10.11 probe for the same artifact terminated during parser startup; this run checks the matching local TensorRT 11 distribution independently.

## Result

| Field | Value |
|---|---|
| ONNX | `pp-formulanet-plus-s.onnx` |
| ONNX SHA-256 | `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` |
| Size | `231,878,904` bytes |
| Input profile | `x=[1,1,384,384]` for min/opt/max |
| Parser | blocked at `Loop.0` (`p2o.pd_op.while.0.0`) |
| Error | `onnxOpCheckers.cpp:1060 checkBitwiseAnd` |
| Engine | not created |
| Inference/decoder | not run |

TensorRT 11 loaded the graph and reported ONNX IR `0.0.8`, opset `18`, then rejected the `BitwiseAnd` inside the autoregressive Loop. No serialized Engine or performance sample was produced.

## Boundary

The `unverified` wording in the paragraph below is the state recorded by this 2026-10-12 probe. The subsequent six-model TRT10.11/TRT11 probes now record each remaining formula artifact's precise importer/build blocker in the current formula status index.

For this exact Plus-S ONNX graph, the two tested local TensorRT lines now have independent builder blockers: TensorRT 10.11 terminated with `0xC0000005` during parser startup, while TensorRT 11 rejected `BitwiseAnd` before Engine creation. The matrix marks only this exact artifact as `✗⁴`; the other five formula models remain `△`/unverified and are not inferred from this result. This is not a universal TensorRT-version claim, and it is not a formula accuracy result. A future supported path requires a maintained export with fixed recurrent-state contracts or a dedicated plugin/encoder-decoder split.

The machine-readable record is [`formula-tensorrt-plus-s-builder-probe-20261012.json`](formula-tensorrt-plus-s-builder-probe-20261012.json).
