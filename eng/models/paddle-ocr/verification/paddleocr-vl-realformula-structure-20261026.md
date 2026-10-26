# PaddleOCR-VL 1.6 realFormula structure diagnostics

Date: 2026-10-26. These diagnostics reuse the [121-image labelled Formula run](paddleocr-vl-realformula-ort-cuda-20261026.md), source-report SHA-256 `c991d56972625ff8688300a0671e9591025c0c9d6103434eab7797add98d9bd7`. Model and backend remain the pinned four-graph PaddleOCR-VL-1.6 ONNX export on Windows ONNX Runtime CUDA. The dataset and selection are unchanged. The existing private [formula evaluation tool](../../paddle-document/tools/formula-render-quality/README.md) now accepts this completed external report as an explicit input; its default six-model path remains unchanged.

The numeric rows below are the historical evaluator v0.3.0 result. [Evaluator v0.4.0 reevaluation](../../paddle-document/verification/formula-realformula-semantic-reevaluation-20261026.md) corrected false agreement for distinct equations: current VL counts are 5 sampled-equivalent, 1 sampled-different and 115 not-assessable. Rendering and structure rows below are unchanged.

| Diagnostic | Result |
| --- | ---: |
| Reference formulas rendered by locked MathJax | 118 / 121 |
| VL predictions rendered after removing outer `\[` / `\]` | 121 / 121 |
| Paired raster comparisons | 118 / 121 |
| Exact normalized MathML structure among comparable pairs | 23 / 118 |
| Foreground IoU P05 / P50 | 0.0239 / 0.2368 |
| Grayscale pixel similarity P05 / P50 | 0.8372 / 0.9094 |
| Restricted numeric substitution: sampled-equivalent | 7 / 121 |
| Restricted numeric substitution: sampled-different | 1 / 121 |
| Restricted numeric substitution: not assessable | 113 / 121 |

The renderer is MathJax 4.1.3 `tex-svg` with local SVG fonts and sharp 0.35.5. It fits each formula without distortion into a 2048x128 grayscale canvas, then compares foreground overlap and pixel similarity. The numeric evaluator uses a restricted presentation-MathML real-arithmetic AST, 24 deterministic substitutions, relative tolerance `1e-9`, and requires at least 12 valid substitutions. The one sampled-different result is image `220312373-25.png`: the prediction places `+1` outside a square root where the label places it inside. That is a concrete expression difference, not merely LaTeX presentation.

These are **diagnostics, not a formula-accuracy approval**. MathJax renderability does not imply a correct expression or full-TeX compatibility; similar raster pixels can hide an important mathematical change. Sampled agreement is not a proof of equivalence, and 113 unassessable rows are never counted as correct. The three non-renderable references also limit paired comparisons. This run does not establish OCR/document quality, any other backend, independent test-set generalization or a release threshold.

Machine reports remain local: `E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026\render-quality.json` has SHA-256 `457cf2fa86835d073042616ca1c1ca87ef3f1d80e182f3e6a1b679ed79565c92`; `semantic-quality.json` has SHA-256 `af9e6736978c524027c6fda77f761107f10b638aff719286b6f60af2ff609b63`. They contain source/image/formula hashes and outcomes, not prediction or reference text. The original source report, images, annotations and predictions remain outside Git. To reproduce, use the `--vl-report` commands in the [evaluation tool guide](../../paddle-document/tools/formula-render-quality/README.md) with the same source report and fixed dataset manifest.
