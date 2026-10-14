# SLANeXt TensorRT carry-shape diagnostic (2026-10-14)

The original SLANeXt Builder failure reported a scalar recurrence carry and a
Loop-body output with shape `[-1,8]`. To determine whether that first error was
the only issue, the two official ONNX files were copied to diagnostic files and
only these initializers were changed:

| Initializer | Diagnostic shape |
|---|---:|
| `p2o.pd_op.full.641.0` | `float32[1,8]` |
| `p2o.pd_op.full.642.0` | `float32[1,50]` |

The source exports were not modified. The transformation is reproducible with
[`Prepare-SlanextTensorRtCarryShapeProbe.py`](../scripts/Prepare-SlanextTensorRtCarryShapeProbe.py).
The generated graph hashes and full Builder diagnostics are in the
[machine-readable report](slanext-tensorrt-carry-shape1-probe-20261014.json).

## Result

DeploySharp TensorRT API 11 was run on both derived graphs. The original
recurrence-shape error was no longer emitted, but TensorRT 11 returned an empty
Engine from `buildSerializedNetwork` with an internal graph-scope assertion:

```text
Error Code 2: Internal Error
Assertion newNodeOrder.size() == g.nodes.size() failed
in graphScope.makeScopeNodesContiguous
```

The same failure was reproduced with both the dynamic `[1,2,2]` profile used by
the original probe and a static batch-1 profile (`minimum=optimum=maximum
=[1,3,512,512]`). Both wired and wireless derived graphs therefore remain
`unsupported` for this exact TensorRT `11.0.0.114-cu12` runtime.

| Derived graph | SHA-256 | Builder result |
|---|---|---|
| `slanext-wired-trt-carry-shape1.onnx` | `8c376fc050c2b8e926fe5ca38848d04efece6fe1a426c2ede35b5d436e11445d` | `DS-TRT-5010`, empty Engine; `graphScope.makeScopeNodesContiguous` |
| `slanext-wireless-trt-carry-shape1.onnx` | `2ce080e1693ff2042824494254a959640ab5eab38a8908e196c5a2566b49e749` | `DS-TRT-5010`, empty Engine; `graphScope.makeScopeNodesContiguous` |

No TensorRT Session, table Decoder, HTML output, ORT parity, quality or
performance result was produced. This diagnostic does not change the status of
the original or OpenVINO-compatible Release artifacts and is not a candidate
for publication. A real fix still requires a recurrence-preserving export or
plugin/graph decomposition, followed by Engine, Decoder and parity evidence.
