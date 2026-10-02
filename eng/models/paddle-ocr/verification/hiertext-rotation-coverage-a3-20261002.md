# HierText rotation coverage audit (A3) (2026-10-02)

This audit compares the local HierText validation annotation cache with its Open Images rotation metadata. It is a provenance and coverage check, not an OCR accuracy result. No image or annotation is copied into the repository or a Release.

## Inputs

- Metadata rows: 41620 (ed93a0e121fe345effdfc7359b848dbc64a1ff6778c8c73563157cb500b33a17)
- HierText annotation image IDs: 1724 (ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a)
- DeploySharp source revision: e848e405add601c3d630d37cce8f6780fb9afa23

## Findings

- Metadata rows with nonzero source rotation (90.0, 180.0, 270.0): 476.
- Annotation IDs that map to a nonzero rotation row: 0.
- Annotation IDs without a metadata row: 1690.
- A3 coverage status: **blocked-no-annotated-nonzero-rotation**.

All metadata rotation distribution:
- <blank>: 5318
- 0.0: 35826
- 180.0: 30
- 270.0: 333
- 90.0: 113

Rotation distribution among annotation IDs that do map to metadata:
- <blank>: 3
- 0.0: 31

Representative metadata-only nonzero-rotation IDs (not valid OCR ground-truth samples): 0004886b7d043cfd, 0065e1098f7a353b, 0152e9e59ca4897b, 0177ba1593d54279, 02ce70f1b6ff6b00, 02ec05fac2e747a6, 031244297d177089, 0344cade36bbd4f4, 03ff460545b92e53, 043cb18c8b64a83e, 04593c33db4a437b, 05028f7518f7c265, 05f4df7a207d1901, 060c7fa6287df581, 0713109a36fd0303, 087323111be52855, 093e4e110fd3f5c6, 09657edae4f69497, 0ad7884032419621, 0b6f22bf3b586889

## Boundary

The cached annotation file and rotation metadata do not provide a usable intersection for natural nonzero-rotation OCR evaluation. The 476 metadata-only rows cannot be downloaded and scored as HierText samples because they have no matching text annotation in this cache. The existing controlled SROIE transformations and known-quadrilateral rectification therefore remain geometry-contract evidence only; A3 still needs a legally usable natural or explicitly labeled angle dataset before repair rate, CER/WER, rejection rate and coordinate error can be claimed.

The audit is reproducible with:

```powershell
.\eng\models\paddle-ocr\scripts\Audit-HierTextRotationCoverage.ps1 `
  -MetadataPath 'F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation-images-with-rotation.csv' `
  -AnnotationPath 'F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation.jsonl.gz' `
  -OutputJson 'eng\models\paddle-ocr\verification\hiertext-rotation-coverage-a3-20261002.json' `
  -OutputMarkdown 'eng\models\paddle-ocr\verification\hiertext-rotation-coverage-a3-20261002.md'
```
