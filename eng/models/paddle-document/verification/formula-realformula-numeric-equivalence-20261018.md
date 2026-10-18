# realFormula restricted numeric-equivalence diagnostic (2026-10-18)

This is an exploratory, fail-closed diagnostic over the existing 121 MathNet realFormula labels and six checked-in ORT CPU prediction sets. “Sampled-equivalent” is not a proof of mathematical equivalence and is not a release-quality score.

| Model | Samples | Sampled-equivalent | Sampled-different | Not assessable | Assessed coverage | Numeric agreement among assessed |
|---|---:|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 3 | 0 | 118 | 2.48% | 100.00% |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 2 | 0 | 119 | 1.65% | 100.00% |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 2 | 0 | 119 | 1.65% | 100.00% |
| `paddle-formula/pp-formulanet-s` | 121 | 3 | 0 | 118 | 2.48% | 100.00% |
| `paddle-formula/pp-formulanet-l` | 121 | 4 | 0 | 117 | 3.31% | 100.00% |
| `paddle-formula/unimernet` | 121 | 3 | 0 | 118 | 2.48% | 100.00% |

## Protocol

- MathJax 4.1.3 converts both strings to presentation MathML; evaluator version 0.2.0 supports a restricted real-arithmetic AST (identifiers, numeric literals, arithmetic, fractions, powers, square roots, selected elementary functions, simple relations and comma-delimited numeric tuples).
- Each pair is checked on 24 deterministic substitutions with relative tolerance 1e-9; the image hash, model ID and sample ordinal seed the assignments. At least 12 valid points are required.
- The JSON stores hashes and outcomes only; source formula text is not copied into this report.

## Boundary

- Numerical agreement on a finite set of values is not a symbolic proof and can miss counterexamples; real-only evaluation omits complex semantics, binding, units, sets, matrices, summations, integrals, piecewise expressions and many other constructs.
- Unsupported or insufficient-coverage pairs are `not-assessable`, never counted as equivalent.
- The 121-sample dataset is legally attributed as CC-BY-4.0, but overlap with model training is unknown. This diagnostic does not close the independent semantic evaluator or release-quality gate.
- Only the checked-in ONNX Runtime CPU predictions are evaluated; no other backend quality is inferred.
