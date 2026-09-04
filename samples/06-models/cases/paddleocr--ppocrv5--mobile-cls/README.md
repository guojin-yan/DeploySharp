# paddleocr/ppocrv5/mobile-cls

Model ID: paddleocr/ppocrv5/mobile-cls
Task: text-orientation-classification
Artifact variants: onnx.fp32

## Complete case workflow

Catalog selection and OCR classification package verification. The case stages are: select the immutable catalog entry, verify the ModelPack and asset identities, prepare the declared input, create the compatible backend session, decode the task result, and write an owned output.

Run:

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id paddleocr/ppocrv5/mobile-cls
```

See samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj for the catalog-only verification path and tests/clean-consumer for task-specific native/runtime ownership gates.

## Verification record

Audit date: 2026-08-24
Catalog revision: models-20260903.visual.1

Reproduce the release and ModelPack checks from the repository root:

~~~powershell
pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -ModelId 'paddleocr/ppocrv5/mobile-cls' -UpdateReadmes -DownloadAssets
~~~

| Check | Result | Details |
| --- | --- | --- |
| Official catalog selection | PASS | Exact model ID and artifact filters were selected from the immutable catalog. |
| GitHub Release asset metadata | PASS | Every declared asset is uploaded and its size/SHA256 matches the catalog. |
| ModelPack manifest download | PASS | Manifest HTTP download, byte size, SHA256, model ID, artifact ID, and declared file size/SHA256 identities passed. |
| Full asset download and SHA256 | PASS | All manifest, model, license, and auxiliary assets were downloaded and verified. |

Published runtime evidence:

- onnx.fp32: local-ort-openvino-official-semantics-golden-verified

The runtime-evidence value is copied from the published ModelPack extension. It records the backend evidence attached to this release and is separate from the release-asset integrity checks above.

## Backend verification

Local verification date: 2026-08-25. Results are generated from the exact official-catalog artifact identity and SHA-256.

| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN | TensorRT | LLamaSharp |
| --- | :---: | :---: | :---: | :---: | :---: |
| onnx.fp32 | ✓ | ✓ | ✓ | ✓ | — |

`✓` means build/load and real inference passed; `✗` means exact compatibility validation failed on the tested runtime; `—` means no matching local artifact or the artifact format does not apply.

See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the tested machine, failure reasons, and reproduction commands.
