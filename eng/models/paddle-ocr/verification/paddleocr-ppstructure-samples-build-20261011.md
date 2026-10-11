# DeploySharp sample build evidence (2026-10-11)

Environment: Microsoft Windows 10.0.26200, .NET SDK `10.0.401`, Release, `--no-restore`. Source revision `4898d865ad72bb9fc045a12dbf10e0d662f4daac`. The working tree was dirty: `True`. Each project was built using its own declared target framework.

| Sample project | Target framework | Result | Errors | Warnings |
| --- | --- | --- | ---: | ---: |
| `samples/01-core/CoreContractInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/02-visual/roi-official-sam/OfficialSamRoi.csproj` | `net10.0` | PASS | 0 | 0 |
| `samples/02-visual/roi-workflow/VisualRoiWorkflow.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/02-visual/VisualProfileInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/03-backends/OpenCvDnnContractInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/04-multimodal/MultimodalContractInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/05-llm/LlmPromptInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/06-models/release-inference/ModelReleaseInference.csproj` | `net8.0` | PASS | 0 | 0 |
| `samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj` | `net8.0` | PASS | 0 | 0 |

**Total:** 10 passed, 0 failed, 10 projects.

This is build-only evidence. It does not prove model availability, backend support, inference correctness, quality, performance, or long-running stability.

Machine-readable record: [paddleocr-ppstructure-samples-build-20261011.json](paddleocr-ppstructure-samples-build-20261011.json).

