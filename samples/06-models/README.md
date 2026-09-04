# Model workflows / 模型工作流

This module is split into two complete workflows:

- catalog-workflow: loads the official catalog, selects every artifact with explicit backend/format/precision/quantization filters, and prints a model/task/artifact matrix. Pass --model-id to run one model case.
- release-inference: downloads and verifies a published ModelPack through ModelFactory, creates the Visual profile, prepares an image with OpenCV, runs ONNX Runtime CPU inference, decodes the result, and writes a PGM mask. It currently provides independent runnable cases for PaDiM, BRIA RMBG 1.4, and BRIA RMBG 2.0 fp32/dynamic-int8.

When `--image` is omitted, release-inference downloads and verifies the default `bus.jpg` from the dedicated `test-assets.1` Release. Pass `--image <image>` to use a custom input.

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id yolo/v8/detect/n
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-1.4
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image <image>
```

The cases directory contains one case folder for every current official catalog model. Each case records the task, artifact variant, complete workflow stages, and the exact command that starts its catalog selection. A case is not marked as real inference merely because catalog selection succeeds; model/runtime/input prerequisites remain explicit.

## Verification / 验证

Every model case README contains a generated verification record. The audit checks the versioned catalog selection, GitHub Release asset presence, asset size/SHA256 metadata, downloaded ModelPack manifest SHA256, model/artifact identity, and declared artifact file integrity. It also records the runtime-evidence, preprocessing, postprocessing, and golden-evidence fields published by each ModelPack.

The model-by-backend execution results are consolidated in the [model/backend verification matrix](../../docs/model-backend-verification-matrix.md), including the exact OpenCV DNN and TensorRT reproduction commands and recorded compatibility failures.

Run the full 42-model audit from the repository root:

~~~powershell
pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -UpdateReadmes -CachePath E:/DeploySharpModelAudit/metadata
~~~

Add -ModelId <id> to reproduce one case. Add -DownloadAssets to download every declared asset for that case and verify every local file size/SHA256. The current catalog contains 42 models and 43 artifact variants; the metadata/ModelPack audit is required for all cases, while full payload download is an explicit opt-in because the complete release payload is approximately 6.2 GB.
