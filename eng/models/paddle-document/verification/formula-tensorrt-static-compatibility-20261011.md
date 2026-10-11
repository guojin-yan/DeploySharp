# Formula TensorRT static compatibility audit (2026-10-11)

This report inspects the six formula ONNX graphs without building engines. It is a parser-risk inventory, not a support verdict.

| Model | Nodes | Loop body nodes | Input | Output | Risk flags |
|---|---:|---:|---|---|---|
| `paddle-formula/pp-formulanet-plus-s` | 388 | 717 | `['DynamicDimension.0', 1, 384, 384]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |
| `paddle-formula/pp-formulanet-plus-m` | 824 | 1743 | `['DynamicDimension.0', 1, 384, 384]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |
| `paddle-formula/pp-formulanet-plus-l` | 1897 | 2265 | `['DynamicDimension.0', 1, 768, 768]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |
| `paddle-formula/pp-formulanet-s` | 388 | 717 | `['DynamicDimension.0', 1, 384, 384]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |
| `paddle-formula/pp-formulanet-l` | 1897 | 2265 | `['DynamicDimension.0', 1, 768, 768]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |
| `paddle-formula/unimernet` | 11036 | 6035 | `['DynamicDimension.0', 1, 192, 672]` | `['DynamicDimension.1', 'DynamicDimension.2']` | autoregressiveLoop, bitwiseOpsInLoop, controlFlowInLoop, dynamicInputOrOutput, dynamicShapeOpsInLoop |

## Boundary

All six graphs contain an autoregressive ONNX `Loop`; each loop body uses control flow and dynamic-shape operations. The Plus-S live TensorRT probe separately terminated with `0xC0000005` during parser startup. This audit does not prove that every TensorRT version fails, and it does not admit any formula TensorRT backend.

The machine-readable report preserves model SHA-256, opset, top-level operator counts and loop-body operator counts so a future compatibility export can be compared without rebuilding the original graph first.
