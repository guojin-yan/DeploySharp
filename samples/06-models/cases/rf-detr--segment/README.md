# rf-detr/segment

Model ID: rf-detr/segment
Task: instance-segmentation
Artifact variants: onnx.fp32

## Complete case workflow

This published artifact has a fixed 432×432 input, 200 queries and mask port `4245`. The current-source RF-DETR project loads the local ONNX, verifies its size/SHA-256, prepares a real image, runs the built-in segmentation decoder and saves JSON, an overlay and each instance mask. It uses the original 90 foreground-column/no-object contract with numeric class labels.

Run:

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract eng/models/rf-detr/legacy-segment.json --model ../models/rf-detr-segment.model.onnx --backend ort --output ../results/rf-detr/legacy-segment
```

Use `--image <path>` to replace the default integrity-checked `bus.jpg`. The local file must match SHA-256 `6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35` (131,311,092 bytes). The sample performs that check automatically. This exact local workflow was run on ORT CPU and saved six instances on the default image; it is distinct from the new 312×312 [Seg Nano 1.11.2 local case](../../local-cases/rf-detr--segment--nano/README.md).

To obtain the existing public artifact through ModelFactory, install it into a verified cache and pass the returned package's `bundle/model.onnx` path to `--model`:

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install --model-id rf-detr/segment --backend onnxruntime --format onnx --preview --cache ../models/model-factory
```

Catalog inspection remains available independently:

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id rf-detr/segment
```

See the [shared RF-DETR sample](../../rf-detr/README.md) for native runtime setup and saved result formats. Runtime support is tracked separately below.

## Verification record

Audit date: 2026-08-24
Catalog revision: models-visual.1

Reproduce the release and ModelPack checks from the repository root:

~~~powershell
pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -ModelId 'rf-detr/segment' -UpdateReadmes
~~~

| Check | Result | Details |
| --- | --- | --- |
| Official catalog selection | PASS | Exact model ID and artifact filters were selected from the immutable catalog. |
| GitHub Release asset metadata | PASS | Every declared asset is uploaded and its size/SHA256 matches the catalog. |
| ModelPack manifest download | PASS | Manifest HTTP download, byte size, SHA256, model ID, artifact ID, and declared file size/SHA256 identities passed. |
| Full asset download and SHA256 | NOT RUN | Add -DownloadAssets for a local full-payload download; release metadata and the manifest were checked in this audit. |

Published runtime evidence:

- onnx.fp32: local-ort-openvino-verified; preprocessing=rfdetr-direct-resize-imagenet-v1; postprocessing=rfdetr-mask-logit-bilinear-v1

The runtime-evidence value is copied from the published ModelPack extension. It records the backend evidence attached to this release and is separate from the release-asset integrity checks above.

## Backend verification

Statuses are synchronized from the reviewed model/backend matrix for the exact official-catalog artifact. They summarize retained runtime evidence; regenerating this table does not rerun inference.

| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LlamaSharp |
| --- | :---: | :---: | :---: | :---: | :---: |
| onnx.fp32 | ✓ | ✗ | ✗ | ✓ | — |

`✓` means real inference and result decoding passed; `△` means partial contract or component evidence; `✗` means the tested combination cannot complete; `—` means untested or not applicable. A pass does not imply dataset accuracy, all input shapes, or all devices.

See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the evidence scope, failure reasons, and links to reproduction instructions.
