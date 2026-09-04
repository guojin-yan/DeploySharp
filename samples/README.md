# Samples / 示例

Samples are organized by complete workflows. A workflow may combine profile registration, preprocessing, backend selection, inference, decoding, output ownership, cancellation, and cleanup; a sample is not created merely to expose one method.

## Module map / 模块划分

| Folder | Complete workflow | Entry point |
| --- | --- | --- |
| 01-core | Backend-neutral model/tensor/profile contract lifecycle | CoreContractInspection |
| 02-visual | Visual profile registration, exact tensor bindings, decoder ownership, registry freeze | VisualProfileInspection |
| 03-backends | OpenCV DNN native loading, named tensor execution, golden check, disposal | OpenCvDnnContractInspection |
| 04-multimodal | Ordered media, in-memory adapter, streaming, cancellation, lifecycle | MultimodalContractInspection |
| 05-llm | Conversation history, prompt formatting, assistant boundary | LlmPromptInspection |
| 06-models | Catalog-wide model selection plus published model download/inference | See the model workflows below |
| 07-benchmarks | Same-model backend/platform latency and throughput measurement | InferenceSpeedBenchmark |

Every module has a README with its prerequisites and command. Run commands from the repository root.

```powershell
dotnet run --project samples/01-core/CoreContractInspection.csproj -c Release
dotnet run --project samples/03-backends/OpenCvDnnContractInspection.csproj -c Release
dotnet run --project samples/04-multimodal/MultimodalContractInspection.csproj -c Release
dotnet run --project samples/05-llm/LlmPromptInspection.csproj -c Release
dotnet run --project samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj -c Release -- --backend all --warmup 10 --iterations 100 --output artifacts/benchmark.json
```

## Model workflows / 模型工作流

06-models/catalog-workflow walks every official catalog entry and every artifact variant. It selects a compatible backend with explicit format, precision, and quantization filters, verifies the selected artifact identity, and prints the task/artifact matrix without downloading large files. A single model can be inspected with --model-id.

For an application-independent catalog workflow, the source-built ModelFactory CLI provides the same selection surface plus JSON inspection: doctor checks the local catalog, list enumerates artifacts, show prints versioned release assets, and install downloads and verifies a selected ModelPack. See the [CLI README](../tools/DeploySharp.ModelFactory.Cli/README.md).

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id bria/rmbg-2.0
```

06-models/release-inference is the real published-model path. ModelFactory downloads the versioned Release ModelPack, verifies size/SHA256, Visual prepares the image, ONNX Runtime runs CPU inference, and the task decoder writes an inspectable PGM mask. It has independent cases for PaDiM, BRIA RMBG 1.4, and BRIA RMBG 2.0 fp32/dynamic-int8.

```powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-1.4 --image <image>
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image <image>
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id anomalib/padim/mvtec-bottle --image <image>
```

06-models/cases contains one folder for each of the 42 current official catalog entries. Each folder records the model ID, task, artifact variant, complete workflow stages, and exact starting command. The three published vision entries have a runnable inference path; the other entries begin with catalog/package verification and explicitly state the input, native runtime, tokenizer, or task-decoder prerequisites still required for real inference.

07-benchmarks/InferenceSpeedBenchmark measures the same pinned classification fixture through ONNX Runtime, OpenCV DNN, and OpenVINO when their native runtimes are available. It reports warm P50/P95 latency, average latency, throughput, and managed allocation together with OS and architecture metadata. The sample records an unavailable backend explicitly; it does not turn an unavailable runtime into a zero result. See its [benchmark README](07-benchmarks/InferenceSpeedBenchmark/README.md) and the [performance benchmarking guide](../docs/articles/performance-benchmarking.md).

The catalog-to-case mapping is checked with: `pwsh -NoProfile -File eng/model-catalog/Test-ModelSampleCoverage.ps1`.

All 42 model case READMEs now include a release verification record. Run the complete online audit with pwsh -NoProfile -File eng/model-catalog/Test-PublishedModelCases.ps1 -UpdateReadmes -CachePath E:/DeploySharpModelAudit/metadata; add -ModelId <id> for one case or -DownloadAssets for a full local asset download and SHA256 check. The full published payload is about 6.2 GB, so payload downloads are opt-in.

## Ownership and evidence / 所有权与证据

Samples never commit large model files, native runtimes, or user images. Release models are application-cache assets and remain SHA256/ModelPack verified. A successful catalog selection is not reported as algorithm or inference verification; the case README states the evidence boundary.
