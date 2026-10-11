# Formula realFormula command-token quality diagnostics (2026-10-11)

This report joins the checked-in ORT CPU predictions to the locally cached MathNet realFormula v1 manifest by image SHA, then compares LaTeX command-token multisets and shallow environment pairing. Raw reference LaTeX is not copied into the report.

- Manifest entries: **121**; manifest SHA-256: `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`
- The six model reports each contain 121 prediction rows with image SHA validation.

| Model | Samples | Ref commands | Pred commands | Matched | Micro P | Micro R | Micro F1 | Macro F1 | Ref env balanced | Pred env balanced |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 778 | 1320 | 617 | 46.74% | 79.31% | 58.82% | 60.46% | 121/121 | 119/121 |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 778 | 1369 | 654 | 47.77% | 84.06% | 60.92% | 61.59% | 121/121 | 121/121 |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 778 | 1369 | 673 | 49.16% | 86.5% | 62.69% | 62.6% | 121/121 | 121/121 |
| `paddle-formula/pp-formulanet-s` | 121 | 778 | 1601 | 641 | 40.04% | 82.39% | 53.89% | 61.48% | 121/121 | 117/121 |
| `paddle-formula/pp-formulanet-l` | 121 | 778 | 2163 | 664 | 30.7% | 85.35% | 45.15% | 61% | 121/121 | 119/121 |
| `paddle-formula/unimernet` | 121 | 778 | 1336 | 666 | 49.85% | 85.6% | 63.01% | 62.74% | 121/121 | 121/121 |

## Interpretation boundary

- The score is exact lexical command-token overlap, and environment pairing is a shallow structural check; neither is a TeX parser or renderer score. Equivalent expressions written with different but valid LaTeX forms can score lower, while syntactically invalid output can still share tokens.
- Image SHA and manifest joins were validated for every model row; raw reference formulas remain outside Git and are not redistributed by this report.
- The source predictions are ORT CPU only. This report does not add OpenVINO, OpenCV DNN or TensorRT quality evidence, and it does not close the formula quality gate.
