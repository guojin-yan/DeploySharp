# Formula LaTeX parseability diagnostic (2026-10-12)

This diagnostic uses SymPy's ANTLR LaTeX parser to distinguish parser-accepted output from malformed output. Acceptance is not rendering success or mathematical semantic equivalence.

- Samples: **726**; parser accepted: **548**; rejected: **178**; acceptance rate: **75.48%**

| Model | Samples | EOS | Parse accepted | Parse rejected | Acceptance |
|---|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 121 | 90 | 31 | 74.38% |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 121 | 91 | 30 | 75.21% |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 121 | 95 | 26 | 78.51% |
| `paddle-formula/pp-formulanet-s` | 121 | 121 | 89 | 32 | 73.55% |
| `paddle-formula/pp-formulanet-l` | 121 | 120 | 93 | 28 | 76.86% |
| `paddle-formula/unimernet` | 121 | 121 | 90 | 31 | 74.38% |

## Boundary

SymPy/ANTLR parse acceptance is not TeX rendering, visual equivalence, or mathematical semantic correctness. Predictions and source images remain governed by the existing realFormula license and evidence boundaries.
