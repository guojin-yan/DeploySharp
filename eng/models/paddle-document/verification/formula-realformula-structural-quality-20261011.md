# Formula realFormula structural-token quality diagnostics

- Manifest SHA-256: `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`; rows per model: `121`.
- Tokens are a conservative lexical inventory of commands, identifiers, numeric literals, grouping delimiters, sub/superscripts and common operators.

| Model | Samples | Expected | Actual | Matched | Micro P | Micro R | Micro F1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 6431 | 8203 | 6207 | 75.67% | 96.52% | 84.83% |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 6431 | 8202 | 6239 | 76.07% | 97.01% | 85.27% |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 6431 | 7983 | 6236 | 78.12% | 96.97% | 86.53% |
| `paddle-formula/pp-formulanet-s` | 121 | 6431 | 8726 | 6207 | 71.13% | 96.52% | 81.90% |
| `paddle-formula/pp-formulanet-l` | 121 | 6431 | 9055 | 6180 | 68.25% | 96.10% | 79.81% |
| `paddle-formula/unimernet` | 121 | 6431 | 8031 | 6231 | 77.59% | 96.89% | 86.17% |

This is a lexical/structural diagnostic only; it must not be presented as a mathematical or rendered-quality score. The source predictions are ORT CPU and do not add OpenVINO, OpenCV DNN or TensorRT evidence.
