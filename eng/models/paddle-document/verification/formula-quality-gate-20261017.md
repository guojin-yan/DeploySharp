# Formula realFormula release-quality gate (2026-10-17)

This report is a fail-closed gate over the existing 121-sample MathNet realFormula evidence. It separates execution integrity from parser/render diagnostics and refuses to declare formula quality without an independent mathematical-semantic evaluator.

- Overall gate: **BLOCKED**
- Execution integrity for all models: **TRUE**
- Diagnostic render thresholds for all models: **FALSE**
- Mathematical semantic evidence: **NOT ASSESSED**

| Model | EOS | Empty | CER | Render | Comparable | Parse | Syntax | Semantic | Gate |
|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| ``paddle-formula/pp-formulanet-plus-s`` | 100.00% | 0 | 57.04% | 90.91% | 88.43% | 74.38% | 85.95% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-plus-m`` | 100.00% | 0 | 58.34% | 100.00% | 97.52% | 75.21% | 96.69% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-plus-l`` | 100.00% | 0 | 54.60% | 99.17% | 96.69% | 78.51% | 96.69% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-s`` | 100.00% | 0 | 61.58% | 91.74% | 89.26% | 73.55% | 82.64% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/pp-formulanet-l`` | 99.17% | 0 | 89.86% | 96.69% | 95.04% | 76.86% | 91.74% | ``not-assessed`` | ``blocked`` |
| ``paddle-formula/unimernet`` | 100.00% | 0 | 51.59% | 99.17% | 96.69% | 74.38% | 95.04% | ``not-assessed`` | ``blocked`` |

## Gate policy

- Each model must have exactly 121 joined image rows, no empty predictions, and EOS rate at least 99% for execution integrity.
- Prediction render rate and comparable reference/prediction pairs are diagnostic thresholds (90% and 90%); they do not establish semantic correctness.
- A release-quality pass requires a separately versioned semantic evaluator and an independent, legally attributable validation set. Exact string/CER, SymPy/ANTLR acceptance, structure fingerprints and MathJax raster similarity remain diagnostics only.
- Backend quality is not inferred from this ORT CPU report. OpenVINO, OpenCV DNN and TensorRT retain their precise matrix states.

## Reproduction

```powershell
.\eng\models\paddle-document\scripts\Evaluate-FormulaQualityGate.ps1
```

The report intentionally remains blocked with the current evidence; this is an expected safety result, not a test failure.
