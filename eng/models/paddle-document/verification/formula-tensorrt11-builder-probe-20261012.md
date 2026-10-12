# FormulaNet TensorRT 11 builder probe (five exact graphs)

Date: 2026-10-12
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)
Runtime: TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`
Tool: `trtexec --skipInference --noTF32 --builderOptimizationLevel=0`

## Scope

This report closes the remaining four FormulaNet graphs plus the previously probed Plus-S graph on the exact ONNX files currently used by the `models-paddleocr` release. Each graph was given a fixed batch-one profile matching its exported input. The probe only asks whether TensorRT 11 can parse and build an Engine; it does not run a formula Decoder, compare tokens, or measure quality/performance.

## Results

| Model | ONNX SHA-256 | Bytes | Input profile | Parser boundary | Engine |
|---|---|---:|---|---|---|
| `paddle-formula/pp-formulanet-plus-s` | `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` | 231,878,904 | `x=[1,1,384,384]` | `Loop.0` (node 387), `checkBitwiseAnd` | not created |
| `paddle-formula/pp-formulanet-plus-m` | `10d5b4645ec75b441e83e0a543c07e80d3d1704e66eeba9cb605374828465a19` | 592,372,919 | `x=[1,1,384,384]` | `Loop.0` (node 823), `checkBitwiseAnd` | not created |
| `paddle-formula/pp-formulanet-plus-l` | `ba9ee741c8e95b1f9a026494a2bec9185a710e10298efb857fae6d375c2da2e6` | 733,525,676 | `x=[1,1,768,768]` | `Loop.0` (node 1896), `checkBitwiseAnd` | not created |
| `paddle-formula/pp-formulanet-s` | `9fbcf2d7b5d7534e4c596e241f5c60be482a4d24d6a9625f2e79e140c7522877` | 231,878,904 | `x=[1,1,384,384]` | `Loop.0` (node 387), `checkBitwiseAnd` | not created |
| `paddle-formula/pp-formulanet-l` | `acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3` | 730,379,948 | `x=[1,1,768,768]` | `Loop.0` (node 1896), `checkBitwiseAnd` | not created |

TensorRT 11 loaded all five graphs (ONNX IR `0.0.8`, opset `18`) and reached the autoregressive `Loop`. The FormulaNet graphs then failed the same TensorRT `onnxOpCheckers.cpp:1060 checkBitwiseAnd` importer check. There was no serialized Engine, inference, Decoder output, or timing result for any row. The individual Plus-S report retains the earlier run details; this table is the five-model status roll-up.

## Boundary

For these exact five FormulaNet ONNX files, TensorRT `11.0.0.114-cu12` is now an attempted and unsupported backend (`✗⁴`). The Plus-S graph also has an independent TensorRT 10.11 parser-startup crash; that older result is not generalized to the other four graphs or to every TensorRT release. The common importer message is a precise runtime/graph boundary, not a formula-quality result and not proof that a future fixed-state export or plugin could not work. A supported path would require a maintained encoder/decoder export with fixed recurrent-state contracts or a dedicated TensorRT plugin.

The machine-readable record is [`formula-tensorrt11-builder-probe-20261012.json`](formula-tensorrt11-builder-probe-20261012.json).
