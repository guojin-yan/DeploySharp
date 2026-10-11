# Formula realFormula structural diagnostics (2026-10-11)

This report is derived from the six checked-in MathNet realFormula v1 ORT CPU prediction files. It adds generated-output delimiter balance, generated LaTeX command counts and reference-length strata to the existing exact/CER/EOS metrics. These are diagnostics, not mathematical semantic accuracy.

| Model | Exact | Mean CER | EOS | Prediction balanced | Mean prediction command count |
|---|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 14/121 | 55.29% | 121/121 | 109/121 | 10.91 |
| `paddle-formula/pp-formulanet-plus-m` | 11/121 | 59.05% | 121/121 | 117/121 | 11.31 |
| `paddle-formula/pp-formulanet-plus-l` | 16/121 | 55.34% | 121/121 | 118/121 | 11.31 |
| `paddle-formula/pp-formulanet-s` | 12/121 | 60.2% | 121/121 | 112/121 | 13.23 |
| `paddle-formula/pp-formulanet-l` | 17/121 | 78.39% | 120/121 | 113/121 | 17.88 |
| `paddle-formula/unimernet` | 18/121 | 51.83% | 121/121 | 118/121 | 11.04 |

## Length strata

| Model | Bucket | Samples | Exact | Mean CER | EOS | Prediction balanced | Prediction command count |
|---|---|---:|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 64-127 | 45 | 3 | 49.9% | 45 | 39 | 12 |
| `paddle-formula/pp-formulanet-plus-s` | <64 | 58 | 11 | 57.54% | 58 | 55 | 5.76 |
| `paddle-formula/pp-formulanet-plus-s` | 128-255 | 16 | 0 | 62.37% | 16 | 15 | 22.38 |
| `paddle-formula/pp-formulanet-plus-s` | >=256 | 2 | 0 | 54.98% | 2 | 0 | 44 |
| `paddle-formula/pp-formulanet-plus-m` | 64-127 | 45 | 2 | 52.44% | 45 | 43 | 12.67 |
| `paddle-formula/pp-formulanet-plus-m` | <64 | 58 | 9 | 64.24% | 58 | 57 | 6 |
| `paddle-formula/pp-formulanet-plus-m` | 128-255 | 16 | 0 | 59.63% | 16 | 16 | 22.62 |
| `paddle-formula/pp-formulanet-plus-m` | >=256 | 2 | 0 | 52.63% | 2 | 1 | 44.5 |
| `paddle-formula/pp-formulanet-plus-l` | 64-127 | 45 | 6 | 51.65% | 45 | 43 | 12.87 |
| `paddle-formula/pp-formulanet-plus-l` | <64 | 58 | 10 | 58.9% | 58 | 57 | 5.86 |
| `paddle-formula/pp-formulanet-plus-l` | 128-255 | 16 | 0 | 53.72% | 16 | 16 | 22.62 |
| `paddle-formula/pp-formulanet-plus-l` | >=256 | 2 | 0 | 48.22% | 2 | 2 | 44 |
| `paddle-formula/pp-formulanet-s` | 64-127 | 45 | 3 | 66.48% | 45 | 39 | 19 |
| `paddle-formula/pp-formulanet-s` | <64 | 58 | 9 | 56.63% | 58 | 56 | 5.78 |
| `paddle-formula/pp-formulanet-s` | 128-255 | 16 | 0 | 58.2% | 16 | 16 | 21.62 |
| `paddle-formula/pp-formulanet-s` | >=256 | 2 | 0 | 38.53% | 2 | 1 | 32.5 |
| `paddle-formula/pp-formulanet-l` | 64-127 | 45 | 5 | 85.66% | 45 | 41 | 24.58 |
| `paddle-formula/pp-formulanet-l` | <64 | 58 | 12 | 63.47% | 58 | 56 | 5.91 |
| `paddle-formula/pp-formulanet-l` | 128-255 | 16 | 0 | 114.3% | 15 | 15 | 38.69 |
| `paddle-formula/pp-formulanet-l` | >=256 | 2 | 0 | 60.13% | 2 | 1 | 47.5 |
| `paddle-formula/unimernet` | 64-127 | 45 | 5 | 46.42% | 45 | 43 | 12.29 |
| `paddle-formula/unimernet` | <64 | 58 | 13 | 55.95% | 58 | 58 | 5.69 |
| `paddle-formula/unimernet` | 128-255 | 16 | 0 | 51.83% | 16 | 16 | 22.56 |
| `paddle-formula/unimernet` | >=256 | 2 | 0 | 54.28% | 2 | 1 | 46 |

## Interpretation boundary

- Balanced delimiters and generated-command counts are shallow string diagnostics; they do not parse TeX or prove rendered mathematical equivalence. Reference command precision/recall is intentionally not inferred because the checked-in per-sample records contain reference hashes, not raw labels.
- The input reports are ORT CPU only. No OpenVINO, OpenCV DNN or TensorRT quality claim is added by this derived report.
- The dataset is externally sourced and training overlap is unknown. Exact/CER values remain diagnostic rather than a release-quality gate.
