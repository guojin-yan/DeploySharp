# yolo/v7/detect/base

Model ID: yolo/v7/detect/base
Task: object-detection
Artifact variants: onnx.fp32

## Complete case workflow

Catalog selection and detector package verification; complete image inference requires detector workflow inputs. The case stages are: select the immutable catalog entry, verify the ModelPack and asset identities, prepare the declared input, create the compatible backend session, decode the task result, and write an owned output.

Run:

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id yolo/v7/detect/base
```

See samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj for the catalog-only verification path and tests/clean-consumer for task-specific native/runtime ownership gates.

## Verification record

Audit date: 2026-08-24
Catalog revision: models-visual.1

Reproduce the release and ModelPack checks from the repository root:

~~~powershell
pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -ModelId 'yolo/v7/detect/base' -UpdateReadmes
~~~

| Check | Result | Details |
| --- | --- | --- |
| Official catalog selection | PASS | Exact model ID and artifact filters were selected from the immutable catalog. |
| GitHub Release asset metadata | PASS | Every declared asset is uploaded and its size/SHA256 matches the catalog. |
| ModelPack manifest download | PASS | Manifest HTTP download, byte size, SHA256, model ID, artifact ID, and declared file size/SHA256 identities passed. |
| Full asset download and SHA256 | NOT RUN | Add -DownloadAssets for a local full-payload download; release metadata and the manifest were checked in this audit. |

Published runtime evidence:

- onnx.fp32: local-backend-verified; preprocessing=ultralytics-letterbox-rgb-nchw-v1; postprocessing=deploysharp-yolo-detection-v1

The runtime-evidence value is copied from the published ModelPack extension. It records the backend evidence attached to this release and is separate from the release-asset integrity checks above.

## Backend verification

Local verification date: 2026-08-25; OpenCV follow-up: 2026-09-01. Results are generated from the exact official-catalog artifact identity and SHA-256.

| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN | TensorRT | LLamaSharp |
| --- | :---: | :---: | :---: | :---: | :---: |
| onnx.fp32 | ✓ | ✓ | ✓ | ✓ | — |

`✓` means build/load and real inference passed; `✗` means exact compatibility validation failed on the tested runtime; `—` means no matching local artifact or the artifact format does not apply.

OpenCV 5.0 cannot execute this artifact's graph-internal data-dependent NMS/Gather tail. The verified OpenCV contract instead binds raw head `onnx_node!/model/model.105/Concat_3` (`[1,25200,85]`) and runs DeploySharp managed decode/NMS. On `bus.jpg`, that path and ONNX Runtime's graph-internal end-to-end path both returned seven detections with the same classes and boxes at 0.001 precision; the maximum displayed score delta was approximately `0.000006`. This is an exact-artifact contract and is not inferred for other YOLOv7 exports.

See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the tested machine, failure reasons, and reproduction commands.
