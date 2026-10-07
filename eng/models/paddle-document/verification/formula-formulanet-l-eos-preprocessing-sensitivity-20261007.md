# FormulaNet-L missing-EOS preprocessing sensitivity check

This report follows up the single missing-EOS sample found in the 121-image MathNet realFormula v1 ORT CPU evaluation. The purpose is narrow: determine whether the small numeric difference between PaddleX and DeploySharp preprocessing changes the generated token sequence, and confirm the official PaddleX predictor against the same input tensors.

## Fixed assets and runtime

- Sample: `211110912-2.png`, SHA-256 `2ee24dde726358fe671c4360c5b28d7f22162f7acd0464789aec8c964a6618ad`.
- Local ONNX: `pp-formulanet-l.onnx`, SHA-256 `acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3`.
- Official Paddle inference program SHA-256: `67d356d53daa3df256acc3ac47c34b72399fe3df7debd6990be5129e3fd4b8f3`.
- Official Paddle weights SHA-256: `039b320b0d1b64361a053f7496b05db9f53fabd22d27c80ccfab4c59d3881318`.
- Official processor runtime: PaddleX `3.7.2`, Paddle `3.0.0`, CPU. The .NET run used the repository's ONNX Runtime backend on CPU.

## Results

Both input tensors have shape `[1,1,768,768]` and contain `589,824` Float32 values. Their SHA-256 values differ:

| Input source | SHA-256 | Exact-equal values vs other tensor | Max absolute difference | Mean absolute difference |
| --- | --- | ---: | ---: | ---: |
| DeploySharp preprocessing | `2392730fc7f70da3d6270ac2744c55c6f20f56ddefe491c7ef2fbb735065ea75` | 586,391 / 589,824 | 0.0225636810 | 4.00769835e-8 |
| PaddleX official preprocessing | `5e3f24d90a5306bc6b4a548d450bfcd0e09623babfc2e1e739edce0e654ec1d5` | 586,391 / 589,824 | 0.0225636810 | 4.00769835e-8 |

The official preprocessing tensor was injected into the existing ORT integration path through the opt-in `DEPLOYSHARP_PADDLE_REAL_FORMULA_INPUT_TENSOR` test hook. On both preprocessing tensors:

- ONNX Runtime returned raw shape `[1,1025]`: one BOS token followed by 1,024 generated positions.
- No raw output position contained EOS token ID `2`; the decoder retained `missing-eos:sequence-may-be-truncated`.
- The two ORT raw token arrays were exactly identical (`0` differing token IDs).
- For each exact tensor, the official PaddleX static predictor and ONNX Runtime also returned byte-for-byte identical raw token IDs.

Thus the preprocessing numeric differences do **not** explain the non-EOS output on this sample, and the EOS is not being dropped by DeploySharp's formula decoder. The official Paddle IR and exported ONNX were subsequently inspected directly: both loop conditions include a runtime stop predicate and a 1,024-iteration cap. The ONNX learned positional table has shape `[1026,512]`; derived graph probes at caps 1,025 and 2,048 fail at index 1,026. This confirms an exported-graph ceiling, but does not explain why this image did not emit EOS during an in-range iteration. Three additional selected realFormula images include two whose EOS is emitted at the last returned raw index 1,023, demonstrating that the final in-range position can carry EOS; their long predictions are nevertheless badly mismatched to the reference. See the [generation-limit report](formula-formulanet-l-generation-limit-20261007.md) and [machine-readable probe](formula-formulanet-l-generation-limit-20261007.json). The decoder's `maximumSequenceLength=4096` is an output-safety bound, not a generation request or evidence that the model can emit 4,096 tokens.

The remaining uncertainty is the cause of this one sample's failure to emit EOS **before** the confirmed graph cap; it is not evidence about what the checkpoint would do beyond the supported positional table. Do not synthesize an EOS token or silently strip the warning: callers should continue to see the sequence as potentially truncated. LaTeX strings after whitespace removal matched between PaddleX and DeploySharp; their raw formatted lengths differed (1,777 vs 2,139 characters), while both remained far from the 175-character reference.

## Evidence and reproduction

The machine-readable report includes all model/source/image/tensor hashes, raw shapes, EOS positions, tensor difference statistics and the preprocessing-sensitivity comparison: [`formula-formulanet-l-eos-preprocessing-sensitivity-20261007.json`](formula-formulanet-l-eos-preprocessing-sensitivity-20261007.json).

The test hook only permits a tensor override when exactly one model and one dataset image are selected. Set `DEPLOYSHARP_PADDLE_REAL_FORMULA=1`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_IMAGES=211110912-2.png`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS=pp-formulanet-l`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_TRACE_TOKENS=1`, and `DEPLOYSHARP_PADDLE_REAL_FORMULA_INPUT_TENSOR=<little-endian Float32 dump>` before running `SixFormulaModelsEvaluateRealFormulaDatasetOnOrtCpu`. Set `DEPLOYSHARP_PADDLE_REAL_FORMULA_INPUT_DUMP_DIR` and `DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH` to retain the exact tensor and token trace. `Compare-FormulaNetLOfficialAndOnnxTrace.py` runs the pinned PaddleX preprocessing and predictor comparison and emits the final JSON.

**Boundary:** one image, one PP-FormulaNet-L checkpoint/export pair and CPU execution. This is not a new accuracy score, does not resolve the six-model quality gate, and does not establish behavior on another sample or backend.
