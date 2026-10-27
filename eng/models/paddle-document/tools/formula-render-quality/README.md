# Formula render-quality diagnostics

This maintainer tool renders external MathNet realFormula prediction reports with a locked MathJax SVG renderer and compares reference/prediction rasters. It does not ship in any DeploySharp NuGet package, and raw prediction reports are not stored in the public repository.

From the repository root:

```powershell
npm ci --prefix eng/models/paddle-document/tools/formula-render-quality
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate -- `
  --dataset-root <realFormula-dataset-root> `
  --verification-root .\results\formula-input `
  --output-json .\results\formula-output\render-quality.json `
  --output-markdown .\results\formula-output\render-quality.md
```

The JSON report records source-report and formula hashes, render outcomes, foreground IoU and grayscale pixel similarity. It intentionally does not copy reference or prediction LaTeX into the report.

For a completed external PaddleOCR-VL realFormula quality report, pass the same `--vl-report` to either evaluator and keep outputs outside Git. The VL input must contain the pinned 121-image manifest, exact source labels and image SHA-256 values, 121 EOS results and a completed status. Only outer `\[`/`\]` display delimiters are removed for evaluation; raw-prediction hashes remain in the derived reports. Existing six-model runs are unchanged when this option is omitted:

```powershell
$report = '.\results\paddleocr-vl-realformula\predictions.json'
$output = '.\results\paddleocr-vl-realformula'
$dataset = '<realFormula-dataset-root>'
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate -- `
  --dataset-root $dataset --vl-report $report --output-json "$output\render-quality.json" `
  --output-markdown "$output\render-quality.md"
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate-semantic -- `
  --dataset-root $dataset --vl-report $report --output-json "$output\semantic-quality.json" `
  --output-markdown "$output\semantic-quality.md"
```

MathJax compatibility and raster similarity are visual-structure diagnostics. They do not establish mathematical equivalence, semantic correctness, full-TeX portability, model accuracy or quality on any backend other than the source prediction reports.

## Exploratory numeric-equivalence diagnostic

The separate `evaluate-semantic` command applies deterministic real-number substitutions to a deliberately restricted presentation-MathML AST. It currently recognizes arithmetic, simple relations, selected elementary functions, comma-delimited numeric tuples and supported function applications. Unknown functions and unsupported syntax remain `not-assessable`.

```powershell
npm --prefix eng/models/paddle-document/tools/formula-render-quality run evaluate-semantic -- `
  --dataset-root <realFormula-dataset-root>

npm --prefix eng/models/paddle-document/tools/formula-render-quality test
```

When paths are omitted, inputs are read from `artifacts/formula-render-quality/input` and outputs are written under `artifacts/formula-render-quality`; the repository ignores that generated directory. The report contains hashes and outcomes, not formula text. `sampled-equivalent` only means no mismatch was found on 24 deterministic substitutions; it is neither a proof nor a release-quality score. Relation boundaries require identical ASTs or an exact operand/operator reversal before being called sampled-equivalent. Binders, sets, matrices, summations, integrals and other unsupported constructs remain fail-closed.
