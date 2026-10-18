# Formula render-quality diagnostics

This private engineering tool renders the checked-in MathNet realFormula predictions with a locked MathJax SVG renderer and compares reference/prediction rasters. It does not ship in any DeploySharp NuGet package.

From the repository root:

```powershell
npm ci --prefix eng/models/paddle-document/tools/formula-render-quality
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate -- `
  --dataset-root F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public `
  --verification-root eng\models\paddle-document\verification `
  --output-json eng\models\paddle-document\verification\formula-realformula-render-quality-20261013.json `
  --output-markdown eng\models\paddle-document\verification\formula-realformula-render-quality-20261013.md
```

The JSON report records source-report and formula hashes, render outcomes, foreground IoU and grayscale pixel similarity. It intentionally does not copy reference or prediction LaTeX into the report.

MathJax compatibility and raster similarity are visual-structure diagnostics. They do not establish mathematical equivalence, semantic correctness, full-TeX portability, model accuracy or quality on any backend other than the source prediction reports.

## Exploratory numeric-equivalence diagnostic

The separate `evaluate-semantic` command applies deterministic real-number substitutions to a deliberately restricted presentation-MathML AST. It currently recognizes arithmetic, simple relations, selected elementary functions, comma-delimited numeric tuples and supported function applications. Unknown functions and unsupported syntax remain `not-assessable`.

```powershell
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate-semantic -- `
  --dataset-root F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public

npm --prefix eng/models/paddle-document/tools/formula-render-quality test
```

The evaluator resolves repository verification/output paths relative to its own location. The report contains hashes and outcomes, not formula text. `sampled-equivalent` only means no mismatch was found on 24 deterministic substitutions; it is neither a proof nor a release-quality score. The 2026-10-18 run assessed only 2–4 of 121 pairs per model, so it does not close the mathematical-semantic quality gate. Binders, sets, matrices, summations, integrals and other unsupported constructs must remain fail-closed.
