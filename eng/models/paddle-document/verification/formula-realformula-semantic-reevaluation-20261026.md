# realFormula restricted semantic diagnostic reevaluation

Date: 2026-10-26. Evaluator v0.4.0 reran the same 121 labelled MathNet realFormula images for PaddleOCR-VL-1.6 and the six existing formula-model prediction reports. It changed no labels, predictions, model weights or inference output. The earlier v0.3.0 reports remain historical evidence, but their sampled-equivalent counts are superseded for quality interpretation.

The correction is deliberately conservative: two different equations can both evaluate to `false` at every sampled point. A relation pair is now `sampled-equivalent` only if it has the same parsed boundary or an exact operand/operator reversal and the 24 substitutions find no mismatch. A concrete mismatch remains `sampled-different`; otherwise a nonmatching relation boundary becomes `not-assessable`. This does not establish mathematical equivalence, even for retained sampled-equivalent rows.

| Prediction source | v0.3 sampled-equivalent | v0.4 sampled-equivalent | v0.4 sampled-different | v0.4 not-assessable |
| --- | ---: | ---: | ---: | ---: |
| PaddleOCR-VL-1.6, ORT CUDA TF32 off | 7 | 5 | 1 | 115 |
| PP-FormulaNet Plus-S, ORT CPU | 6 | 5 | 0 | 116 |
| PP-FormulaNet Plus-M, ORT CPU | 6 | 5 | 0 | 116 |
| PP-FormulaNet Plus-L, ORT CPU | 6 | 5 | 0 | 116 |
| PP-FormulaNet-S, ORT CPU | 5 | 4 | 0 | 117 |
| PP-FormulaNet-L, ORT CPU | 7 | 5 | 0 | 116 |
| UniMERNet, ORT CPU | 6 | 5 | 0 | 116 |

For VL, only `220811828-45.png` and `221209826-31.png` changed status, from `sampled-equivalent` to `not-assessable` with reason `relation-boundary-unproven`. The independently observed square-root-scope error in `220312373-25.png` remains `sampled-different`. Across the six traditional models, assessed coverage falls from 36/726 to 29/726; none of these sparse substitutions forms a semantic accuracy metric.

The VL source report SHA-256 is `c991d56972625ff8688300a0671e9591025c0c9d6103434eab7797add98d9bd7`. The old v0.3.0 VL numeric report SHA-256 is `af9e6736978c524027c6fda77f761107f10b638aff719286b6f60af2ff609b63`; the new v0.4.0 report is `c711bdda6d57749958cb0ca7b29b7be1f9a682581a48a1b9758dc6b4ea45b325`. The old six-model report SHA-256 is `ccaa83de89e639fd76a64a4f69c5e6e3583a5b8db4d2552d5fd2768d8e801159`; the new one is `7625a38ee6297987ccb52e824c654a0ea46d2688cfc6e4d0d55eb363287fd443`. New complete reports are retained outside Git at `E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026\semantic-quality-v04.json` and `six-model-semantic-v04.json`. They contain per-image hashes/statuses but no label or prediction text.

Reproduce both evaluations with the [formula diagnostic tool](../tools/formula-render-quality/README.md), the unchanged realFormula dataset and the pinned VL source report. Its 11 regression tests pass, including false-truth-table equations, reversed relations, domain mismatch and unsupported binders. Existing renderability (118/121 references, 121/121 VL predictions) and MathML structure results are unaffected. Semantic quality, domain coverage and independent validation remain open; no backend support or release-quality status is promoted by this reevaluation.
