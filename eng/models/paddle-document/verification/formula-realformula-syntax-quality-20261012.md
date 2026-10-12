# Formula realFormula conservative syntax/structure diagnostics (2026-10-12)

This report parses the 121 MathNet references and six ORT CPU prediction files with a deliberately conservative subset parser. It detects unclosed groups/environments and missing arguments, then compares atom-abstracted structure fingerprints. It is not a TeX renderer or a mathematical-semantic evaluator.

| Model | Samples | Prediction syntax valid | Structure fingerprint match |
|---|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 104/121 (85.95%) | 14/121 (11.57%) |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 117/121 (96.69%) | 11/121 (9.09%) |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 117/121 (96.69%) | 16/121 (13.22%) |
| `paddle-formula/pp-formulanet-s` | 121 | 100/121 (82.64%) | 12/121 (9.92%) |
| `paddle-formula/pp-formulanet-l` | 121 | 111/121 (91.74%) | 17/121 (14.05%) |
| `paddle-formula/unimernet` | 121 | 115/121 (95.04%) | 18/121 (14.88%) |

## Boundary

- The parser is intentionally conservative and covers only common commands; a valid result does not prove that a full TeX engine can render it.
- Fingerprints abstract letters and digits but preserve command names, grouping, scripts and environment shape. Matching is a structure diagnostic, not mathematical equivalence.
- Predictions are the checked-in ONNX Runtime CPU reports. OpenVINO, OpenCV DNN and TensorRT quality are not represented.
