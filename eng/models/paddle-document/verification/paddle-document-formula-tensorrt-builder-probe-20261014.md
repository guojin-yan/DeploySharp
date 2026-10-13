# FormulaNet / UniMERNet TensorRT 10.11 dynamic-Batch=2 builder probe

Date: 2026-10-14  
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)  
Runtime: TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`  
API/bridge: DeploySharp TensorRT API 10 bridge  
Test: `PaddleDocumentTensorRtFormulaBuilderProbeTests.FormulaModelsDynamicBatchTwoHaveExplicitTensorRtBuilderEvidence`

## Scope

This is an exact-asset TensorRT Builder/importer probe for the six FormulaNet/UniMERNet ONNX files used by the `models-paddleocr` release. Each graph was given a dynamic Batch=2 profile and was built through the DeploySharp `TensorRtOnnxEngineBuilder`. The probe intentionally stops at Engine creation: a successful Engine would still require an independent autoregressive execution, official-tokenizer decode and token-level parity contract before the backend could be marked supported.

This TensorRT 10.11 run is independent from the TensorRT 11 Batch=1 probe recorded in [`formula-tensorrt11-builder-probe-20261012.md`](formula-tensorrt11-builder-probe-20261012.md). Runtime/API results must not be combined or generalized across the two installations.

## Results

| Model | ONNX SHA-256 | Input profile | TensorRT 10.11 boundary | Engine |
|---|---|---|---|---|
| `paddle-formula/pp-formulanet-plus-s` | `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` | `x=[2,1,384,384]` | `BitwiseAnd.0` parser diagnostic (`DS-TRT-5009`) | not created |
| `paddle-formula/pp-formulanet-s` | `9fbcf2d7b5d7534e4c596e241f5c60be482a4d24d6a9625f2e79e140c7522877` | `x=[2,1,384,384]` | `BitwiseAnd.0` parser diagnostic (`DS-TRT-5009`) | not created |
| `paddle-formula/pp-formulanet-plus-m` | `10d5b4645ec75b441e83e0a543c07e80d3d1704e66eeba9cb605374828465a19` | `x=[2,1,384,384]` | `BitwiseAnd.0` parser diagnostic (`DS-TRT-5009`) | not created |
| `paddle-formula/pp-formulanet-plus-l` | `ba9ee741c8e95b1f9a026494a2bec9185a710e10298efb857fae6d375c2da2e6` | `x=[2,1,768,768]` | `BitwiseAnd.0` parser diagnostic (`DS-TRT-5009`) | not created |
| `paddle-formula/pp-formulanet-l` | `acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3` | `x=[2,1,768,768]` | `BitwiseAnd.0` parser diagnostic (`DS-TRT-5009`) | not created |
| `paddle-formula/unimernet` | `1d64fafa0161f153dafe40823e97c4b05103030509dd6b286d7c8d4a11b068ab` | `x=[2,1,672,192]` | `ConcatFromSequence.2` parser diagnostic (`DS-TRT-5009`) | not created |

The external test completed `1/1` test with `0` failures and `0` skips. All six assets were present with the registered SHA-256 values; `environmentBlocked=0`. No graph reached Engine serialization, TensorRT execution, Decoder output, tokenizer generation, quality evaluation or timing measurement.

## Boundary and follow-up

- The five FormulaNet graphs are unsupported for this exact TensorRT `10.11.0.33-cu12` importer path because of `BitwiseAnd.0` inside the autoregressive graph.
- UniMERNet is independently unsupported at `ConcatFromSequence.2`; it must not be grouped with the FormulaNet `BitwiseAnd` failure.
- This is not evidence that every TensorRT release, a fixed-state export, or a dedicated plugin would fail. A future supported implementation needs a maintained encoder/decoder export or plugin, then a separate Engine, dynamic-KV, tokenizer and token-parity contract.
- The results do not alter the ORT CPU formula execution evidence or the existing OpenVINO/OpenCV importer boundaries.

The machine-readable source is [`paddle-document-formula-tensorrt-builder-probe-20261014.json`](paddle-document-formula-tensorrt-builder-probe-20261014.json). The probe source is [`PaddleDocumentTensorRtFormulaBuilderProbeTests.cs`](../../../tests/DeploySharp.Visual.TensorRT.Tests/PaddleDocumentTensorRtFormulaBuilderProbeTests.cs).
