# Formula realFormula release-quality gate (2026-10-17)

This report is a fail-closed gate over the existing 121-sample MathNet realFormula evidence. It separates execution integrity from parser/render diagnostics and refuses to declare formula quality without an independent mathematical-semantic evaluator.

- Overall gate: **BLOCKED**
- Execution integrity for all models: **TRUE**
- Diagnostic render thresholds for all models: **FALSE**
- Mathematical semantic evidence: **NOT ASSESSED**

| Model | EOS | Empty | CER | Render | Comparable | MathML structure | Parse | Syntax | Semantic | Gate |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| ``paddle-formula/pp-formulanet-plus-s`` | 100.00% | 0 | 57.04% | 90.91% | 88.43% | 14.95% (16/107) | 74.38% | 85.95% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-plus-m`` | 100.00% | 0 | 58.34% | 100.00% | 97.52% | 11.86% (14/118) | 75.21% | 96.69% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-plus-l`` | 100.00% | 0 | 54.60% | 99.17% | 96.69% | 16.24% (19/117) | 78.51% | 96.69% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-s`` | 100.00% | 0 | 61.58% | 91.74% | 89.26% | 14.81% (16/108) | 73.55% | 82.64% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-l`` | 99.17% | 0 | 89.86% | 96.69% | 95.04% | 18.26% (21/115) | 76.86% | 91.74% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/unimernet`` | 100.00% | 0 | 51.59% | 99.17% | 96.69% | 17.95% (21/117) | 74.38% | 95.04% | ``not-assessed`` | ``blocked`` |

## Gate policy

- Each model must have exactly 121 joined image rows, no empty predictions, and EOS rate at least 99% for execution integrity.
- Prediction render rate and comparable reference/prediction pairs are diagnostic thresholds (90% and 90%); they do not establish semantic correctness.
- A release-quality pass requires a separately versioned, independently reviewed semantic evaluator and an independent, legally attributable validation set. Exact string/CER, SymPy/ANTLR acceptance, structure fingerprints and MathJax raster similarity remain diagnostics only.
- Backend quality is not inferred from this ORT CPU report. OpenVINO, OpenCV DNN and TensorRT retain their precise matrix states.

## Exploratory numeric diagnostic

The linked restricted numeric-substitution report assessed 36/726 pairs (4.96% coverage); 690 pairs were not assessable. Of assessed pairs, 36 had no sampled mismatch and 0 differed. This sparse, unreviewed diagnostic is not a proof or release-quality evidence; mathematical semantic status remains **NOT ASSESSED**.

Those numbers are the historical v0.3.0 snapshot. The [v0.4.0 conservative reevaluation](formula-realformula-semantic-reevaluation-20261026.md) reduces assessable pairs to 29/726; the quality gate remains unchanged.

See [formula-realformula-numeric-equivalence-20261018.md](formula-realformula-numeric-equivalence-20261018.md) and the [machine-readable report](formula-realformula-numeric-equivalence-20261018.json) for hashes, protocol and fail-closed boundaries.

## Reproduction

```powershell
.\eng\models\paddle-document\scripts\Evaluate-FormulaQualityGate.ps1
```

The report intentionally remains blocked with the current evidence; this is an expected safety result, not a test failure.
