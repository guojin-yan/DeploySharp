# FormulaNet-L exported generation-limit probe

## Scope

This is a bounded follow-up to the single missing-EOS case in the 121-image MathNet realFormula v1 ORT CPU evaluation. It inspects the matching official Paddle inference program and ONNX export, then changes only a derived ONNX copy stored outside the repository. It is not a formula-quality result and does not infer behavior for another image or backend.

## Evidence

- Sample: `211110912-2.png`, SHA-256 `2ee24dde726358fe671c4360c5b28d7f22162f7acd0464789aec8c964a6618ad`.
- Input: `[1,1,768,768]`, DeploySharp-preprocessed Float32 tensor SHA-256 `2392730fc7f70da3d6270ac2744c55c6f20f56ddefe491c7ef2fbb735065ea75`.
- ONNX: `pp-formulanet-l.onnx`, SHA-256 `acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3`.
- Official Paddle PIR program: `inference.json`, SHA-256 `67d356d53daa3df256acc3ac47c34b72399fe3df7debd6990be5129e3fd4b8f3`.
- Probe runtime: Windows x64, 16 logical processors, Python 3.12.14, ONNX 1.22.0, ONNX Runtime 1.30.0 CPU.

The official Paddle IR loop condition combines a sequence-length comparison with a runtime stop predicate; the length comparison is against an assigned constant `1024`. The exported ONNX has the same `Loop` condition combining `Less` and `Not`, and uses body initializer `auto.cast.1898 = 1024`. Its positional embedding table is `m_bart_learned_positional_embedding_3.w_0` with shape `[1026,512]`, so its greatest valid position index is 1025.

| Derived graph loop cap | Result for this fixed input |
| ---: | --- |
| 1024 (original graph) | Completes with raw shape `[1,1025]` (BOS + 1,024 generated positions); no EOS, PAD, or UNK token. |
| 1025 | Fails in `Gather.1`: requested index 1026, but valid range is `[-1026,1025]`. |
| 2048 | Same positional-table out-of-bounds failure at index 1026. |

To compare the boundary with shorter and near-boundary outputs, the same model and ORT CPU path was then run on three additional labeled realFormula images:

| Image | Raw output | EOS | Decoded tokens | Prediction/reference chars | Character CER |
| --- | ---: | ---: | ---: | ---: | ---: |
| `220808471-63.png` | 220 IDs | index 219 | 109 | 197 / 124 | 58.87% |
| `220800300-41.png` | 1,024 IDs | index 1,023 (last output position) | 341 | 682 / 70 | 971.43% |
| `211200471-3.png` | 1,024 IDs | index 1,023 (last output position) | 402 | 1,351 / 121 | 1,047.93% |

All three emitted exactly one EOS and no PAD. The first is a shorter in-sample decode; the latter two reach the last returned raw position and still emit EOS there. This demonstrates that the runtime EOS stop can be reached on the final observed position for these examples, while the original case consumes one more raw output position and lacks EOS. It does not show whether those two final-position EOS outputs are always reproducible across export/runtime changes, nor explain why the original image never emits EOS. The extremely high CER for the two long predictions also shows that EOS completion is not a correctness signal. Exact per-image, tensor and token hashes are in the machine-readable report.

Thus the official/exported path has a 1,024-iteration hard ceiling, and simply raising its ONNX Loop constant cannot extend this graph because the positional table is too short. The longer derived graphs cannot execute the next position, so this does **not** reveal whether the checkpoint would emit EOS after the ceiling. The original and official-PaddleX same-input token parity, preprocessing sensitivity and hashes remain in the [input/EOS sensitivity report](formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md). The decoder's 4,096 limit remains only an output safety check.

Keep `missing-eos:sequence-may-be-truncated`; do not append EOS, suppress the warning, or claim the checkpoint itself has a 1,024-token semantic limit. Any attempt to support longer generation would require an officially valid export/checkpoint path with compatible positional state, followed by token-level parity and quality evaluation—not a one-constant patch.

## Reproduction

Use an environment with `onnx`, `onnxruntime` and `numpy`, plus the local ONNX, matching official `inference.json`, and captured tensor named in the machine-readable report. The script never edits the supplied model/program; it writes derived ONNX graphs and its report only under the explicitly supplied output directory:

```powershell
python eng/models/paddle-document/scripts/Probe-FormulaNetLGenerationLimit.py `
  --onnx E:\Model\PaddleDocument\onnx\pp-formulanet-l.onnx `
  --paddle-program E:\Model\PaddleDocument\source\pp-formulanet-l\PP-FormulaNet-L_infer\inference.json `
  --input-tensor E:\Model\PaddleDocument\validation\formula-eos-input-reference-20261007\211110912-2.deploysharp.f32 `
  --output-dir E:\Model\PaddleDocument\validation\formula-eos-limit-probe-20261007 `
  --caps 1024,1025,2048
```

The checked probe output (including exact graph/input hashes and runtime errors) is [machine-readable JSON](formula-formulanet-l-generation-limit-20261007.json). Re-run the three additional formula rows with the existing opt-in test by setting `DEPLOYSHARP_PADDLE_REAL_FORMULA=1`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS=pp-formulanet-l`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_IMAGES=220808471-63.png,220800300-41.png,211200471-3.png`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_TRACE_TOKENS=1`, and `DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH=<external-json-path>`. Derived ONNX files and validation data remain outside Git.

**Boundary:** one PP-FormulaNet-L export, four selected realFormula samples, ONNX Runtime CPU. The 121-sample quality findings and all non-CPU/backend quality gates remain unchanged; the three added images are a targeted stop-position probe, not a broader accuracy estimate.
