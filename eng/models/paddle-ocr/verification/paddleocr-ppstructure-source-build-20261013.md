# DeploySharp source project build evidence (2026-10-13)

Environment: Microsoft Windows 10.0.26200, .NET SDK `10.0.401`, Release, `--no-restore`. Source revision `c7589d257a566a245f921039fd2c14ab1b364167`. Working tree dirty: `True`.

| Source project | Declared target frameworks | Result | Errors | Warnings |
| --- | --- | --- | ---: | ---: |
| `src/DeploySharp.Backend.LlamaSharp/DeploySharp.Backend.LlamaSharp.csproj` | `netstandard2.0;net8.0` | PASS | 0 | 0 |
| `src/DeploySharp.Backend.OnnxRuntime/DeploySharp.Backend.OnnxRuntime.csproj` | `netstandard2.0;net8.0` | PASS | 0 | 0 |
| `src/DeploySharp.Backend.OpenCV/DeploySharp.Backend.OpenCV.csproj` | `net46;net461;net462;net47;net471;net472;net48;net481;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.Backend.OpenVINO/DeploySharp.Backend.OpenVINO.csproj` | `net46;net461;net462;net47;net471;net472;net48;net481;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.Backend.TensorRT/DeploySharp.Backend.TensorRT.csproj` | `net8.0` | PASS | 0 | 0 |
| `src/DeploySharp.Core/DeploySharp.Core.csproj` | `$(DeploySharpLibraryTargetFrameworks)` | PASS | 0 | 0 |
| `src/DeploySharp.Extensibility/DeploySharp.Extensibility.csproj` | `net46;netstandard2.0;net8.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.LLM/DeploySharp.LLM.csproj` | `netstandard2.0;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.ModelFactory/DeploySharp.ModelFactory.csproj` | `netstandard2.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.ModelPack.Json/DeploySharp.ModelPack.Json.csproj` | `netstandard2.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.Multimodal/DeploySharp.Multimodal.csproj` | `netstandard2.0;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.Visual.OpenCV/DeploySharp.Visual.OpenCV.csproj` | `net46;net461;net462;net47;net471;net472;net48;net481;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0` | PASS | 0 | 0 |
| `src/DeploySharp.Visual.TensorRT/DeploySharp.Visual.TensorRT.csproj` | `net8.0` | PASS | 0 | 0 |
| `src/DeploySharp.Visual/DeploySharp.Visual.csproj` | `$(DeploySharpLibraryTargetFrameworks)` | PASS | 0 | 0 |

**Total:** 14 passed, 0 failed, 14 projects.

Build-only evidence. It does not prove model availability, native runtime loading, backend support, inference correctness, quality, performance, packaging, or long-running stability.

Machine-readable record: [paddleocr-ppstructure-source-build-20261013.json](paddleocr-ppstructure-source-build-20261013.json).
