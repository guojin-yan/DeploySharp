# llm/qwen2.5-0.5b-instruct-q4-k-m

Model ID: llm/qwen2.5-0.5b-instruct-q4-k-m
Task: language-generation-and-embedding
Artifact variants: qwen2.5-0.5b-instruct.q4-k-m.gguf
Release: [models-llm.1](https://github.com/guojin-yan/DeploySharp/releases/tag/models-llm.1) (maintained language-model collection)

## Complete case workflow

Catalog selection and GGUF package verification; pair with the LLM prompt workflow for generation. The case stages are: select the versioned catalog entry, verify the ModelPack and asset identities, prepare the declared input, create the compatible backend session, decode the task result, and write an owned output.

Run:

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id llm/qwen2.5-0.5b-instruct-q4-k-m
```

See `samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj` for catalog verification; runtime support is tracked in the backend verification record below.

## Verification record

Audit date: 2026-08-24
Catalog revision: models-llm.1

Reproduce the release and ModelPack checks from the repository root:

~~~powershell
pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -ModelId 'llm/qwen2.5-0.5b-instruct-q4-k-m' -UpdateReadmes
~~~

| Check | Result | Details |
| --- | --- | --- |
| Official catalog selection | PASS | Exact model ID and artifact filters were selected from the versioned catalog. |
| GitHub Release asset metadata | PASS | Every declared asset is uploaded and its size/SHA256 matches the catalog. |
| ModelPack manifest download | PASS | Manifest HTTP download, byte size, SHA256, model ID, artifact ID, and declared file size/SHA256 identities passed. |
| Full asset download and SHA256 | NOT RUN | Add -DownloadAssets for a local full-payload download; release metadata and the manifest were checked in this audit. |

Published runtime evidence:

- qwen2.5-0.5b-instruct.q4-k-m.gguf: source-integrity-and-runtime-evidence-verified

The runtime-evidence value is copied from the published ModelPack extension. It records the backend evidence attached to this release and is separate from the release-asset integrity checks above.

## Backend verification

Statuses are synchronized from the reviewed model/backend matrix for the exact official-catalog artifact. They summarize retained runtime evidence; regenerating this table does not rerun inference.

| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LlamaSharp |
| --- | :---: | :---: | :---: | :---: | :---: |
| qwen2.5-0.5b-instruct.q4-k-m.gguf | — | — | — | — | ✓ |

`✓` means real inference and result decoding passed; `△` means partial contract or component evidence; `✗` means the tested combination cannot complete; `—` means untested or not applicable. A pass does not imply dataset accuracy, all input shapes, or all devices.

See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the evidence scope, failure reasons, and links to reproduction instructions.
