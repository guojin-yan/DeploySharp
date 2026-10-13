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
