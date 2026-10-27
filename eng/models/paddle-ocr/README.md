# PaddleOCR model maintenance

This directory contains the reproducible model catalog and maintainer tooling for the PP-OCR assets published by DeploySharp. User-facing setup, inference examples, backend status and performance results live in the public documentation rather than in this engineering directory.

## Contents

- [`paddle-ocr-models.json`](paddle-ocr-models.json): 17 pinned DET, REC and CLS model records across the supported v4, v5 and v6 families.
- [`scripts/Acquire-PaddleOcrModels.ps1`](scripts/Acquire-PaddleOcrModels.ps1): downloads official Paddle archives, converts them to ONNX and writes local acquisition records.
- [`scripts/Verify-PaddleOcrOnnx.py`](scripts/Verify-PaddleOcrOnnx.py): performs structural ONNX checks.
- [`scripts/Publish-PaddleOcrModelsToGitHub.ps1`](scripts/Publish-PaddleOcrModelsToGitHub.ps1): stages and optionally publishes individually downloadable Release assets.
- PaddleOCR-VL export, tokenizer and deterministic fixture generators under [`scripts`](scripts) are retained because tests and the published Bundle depend on them.

Raw dated benchmark runs, probes and acceptance reports are development records and are not stored in the public repository. Stable conclusions are maintained in:

- [PaddleOCR guide](../../../docs/articles/visual-ocr.md)
- [PP-Structure guide](../../../docs/articles/visual-paddle-structure.md)
- [backend verification matrix](../../../docs/model-backend-verification-matrix.md)
- [device performance results](../../../docs/articles/device-performance-benchmarks.md)

## Acquire models

Run from the repository root. Select explicit model IDs for routine work; use `-All` only when rebuilding the complete collection.

```powershell
./eng/models/paddle-ocr/scripts/Acquire-PaddleOcrModels.ps1 `
  -ModelRoot .\.cache\paddleocr `
  -ModelId ppocrv6/tiny-det,ppocrv6/tiny-rec `
  -Python <python-with-paddle2onnx>
```

Model IDs, source URLs, output names, opsets and profile mappings are authoritative in `paddle-ocr-models.json`. Generated models and acquisition records belong in the selected external model root, not in this repository.

## Validate and stage a Release

```powershell
python ./eng/models/paddle-ocr/scripts/Verify-PaddleOcrOnnx.py `
  --model-root .\.cache\paddleocr

./eng/models/paddle-ocr/scripts/Publish-PaddleOcrModelsToGitHub.ps1 `
  -ModelRoot .\.cache\paddleocr `
  -PaddleDocumentRoot .\.cache\paddle-document `
  -OutputRoot .\staging\deploysharp-paddleocr-release
```

The publishing script stages assets by default. `-Check` validates an existing stage and `-Publish` mutates the configured GitHub Release; maintainers should review the staged manifest and hashes before publishing.

## Maintenance rules

1. Keep catalogs deterministic and pin every distributable asset by size and SHA-256.
2. Keep generated ONNX files, TensorRT engines, local acquisition records and benchmark output outside Git.
3. Put reusable acquisition, conversion, validation and release automation here; archive one-off analysis scripts with their development records.
4. Update the public guides and backend matrix only from reproducible evidence, without linking users to local or historical raw-report paths.
