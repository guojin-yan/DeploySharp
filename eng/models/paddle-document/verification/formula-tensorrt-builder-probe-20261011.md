# FormulaNet TensorRT builder probe (2026-10-11)

This is a deliberately narrow TensorRT admission probe for `paddle-formula/pp-formulanet-plus-s`. It does not turn a parser crash into a backend support claim.

| Field | Value |
|---|---|
| Host | Windows 10.0.26200, RTX 3060 Laptop, compute capability 8.6 |
| Runtime | Driver 576.02, CUDA 12.9, cuDNN 9.22.0, TensorRT 10.11.0.33-cu12 |
| ONNX | `pp-formulanet-plus-s.onnx`, 231,878,904 bytes |
| ONNX SHA-256 | `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` |
| Input profile | `x:1x1x384x384` for min/opt/max |
| Result | **Blocked before engine creation** |
| Process exit | `0xC0000005` / `-1073741819` |

`trtexec` loaded TensorRT and CUDA, initialized the GPU and builder kernel library, and printed the ONNX IR/opset header (`IR 0.0.8`, opset `18`). The process then terminated while entering ONNX parser startup, without a parser diagnostic, serialized engine, inference, decoder output or timing result.

The exact command and machine-readable details are in [the JSON report](formula-tensorrt-builder-probe-20261011.json). This probe is limited to one graph and one TensorRT distribution; it does not classify the other five formula models. The current formula TensorRT matrix therefore remains **unverified**, and no TensorRT quality/performance claim is added.
