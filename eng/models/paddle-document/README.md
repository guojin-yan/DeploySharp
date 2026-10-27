# PP-Structure model maintenance

This directory contains the pinned PP-Structure model catalog and the reusable tooling needed to acquire, convert and validate document-analysis assets. It is a maintainer entry point, not a chronological validation journal.

## Contents

- [`paddle-document-models.json`](paddle-document-models.json): 29 official model records covering orientation, unwarping, layout, table, formula, seal and chart tasks.
- [`scripts/Acquire-PaddleDocumentModels.ps1`](scripts/Acquire-PaddleDocumentModels.ps1): downloads official Paddle archives and converts standard inference exports to ONNX.
- [`scripts/Verify-PaddleDocumentOnnx.py`](scripts/Verify-PaddleDocumentOnnx.py): validates converted graphs and their declared inputs/outputs.
- [`scripts/Build-PaddleDocumentOpenVinoCompatibility.ps1`](scripts/Build-PaddleDocumentOpenVinoCompatibility.ps1): creates separately named SLANeXt OpenVINO compatibility graphs without modifying the source ONNX files.
- Chart2Table export and TensorRT plan tooling under [`scripts`](scripts) is retained for its multi-graph Bundle and backend-specific artifacts.

Raw dated benchmarks, probes, comparison outputs and acceptance reports are development records and are kept outside the public checkout. Current user-facing status is maintained in:

- [PP-Structure guide](../../../docs/articles/visual-paddle-structure.md)
- [PaddleOCR guide](../../../docs/articles/visual-ocr.md)
- [backend verification matrix](../../../docs/model-backend-verification-matrix.md)
- [device performance results](../../../docs/articles/device-performance-benchmarks.md)

## Acquire standard models

Run from the repository root and select only the modules required for the current task.

```powershell
./eng/models/paddle-document/scripts/Acquire-PaddleDocumentModels.ps1 `
  -ModelRoot .\.cache\paddle-document `
  -ModelId pp-doclayout-s,slanext-wired `
  -Python <python-with-paddle2onnx>
```

The catalog is authoritative for model IDs and source archives. Generated source archives, extracted Paddle files, ONNX graphs and acquisition records belong in the external model root.

## Validate converted graphs

```powershell
python ./eng/models/paddle-document/scripts/Verify-PaddleDocumentOnnx.py `
  --onnx-root .\.cache\paddle-document\onnx `
  --image .\images\document-sample.jpg `
  --output .\results\paddle-document-smoke.json
```

SLANeXt compatibility graphs are derived artifacts and must remain distinct from their official source graphs:

```powershell
./eng/models/paddle-document/scripts/Build-PaddleDocumentOpenVinoCompatibility.ps1 `
  -ModelRoot .\.cache\paddle-document\onnx `
  -OutputRoot .\.cache\paddle-document\onnx-openvino `
  -Python <python-with-onnx>
```

Chart2Table is a generative multi-graph Bundle, not a normal single-file Paddle export. Use the dedicated vision/text export and Bundle tooling; do not represent the official weight package as a directly converted single ONNX model.

## Maintenance rules

1. Keep model source, conversion and compatibility metadata reproducible and hash-pinned.
2. Do not commit generated models, engines, per-run output or machine-specific paths.
3. Keep reusable acquisition, conversion, compatibility and release tooling here; archive one-off audits and report summarizers with their outputs.
4. Record supported backend claims only in the public matrix and guides, with exact model and runtime boundaries.
