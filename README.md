<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/readme/hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/images/readme/hero-light.svg">
  <img alt="DeploySharp - reproducible AI inference workflows for .NET" src="docs/images/readme/hero-light.svg" width="100%">
</picture>

<p align="center">A modular .NET model deployment toolkit for reproducible vision, language, and multimodal inference across replaceable backends.</p>

<p align="center">
  <a href="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml?query=branch%3ADeploySharpV2.0"><img src="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml/badge.svg?branch=DeploySharpV2.0" alt="Windows CI" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/LICENSE.txt"><img src="https://img.shields.io/badge/License-Apache%202.0-blue.svg" alt="Apache-2.0 license" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/DeploySharp?style=flat&amp;label=stars" alt="GitHub stars" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET Framework 4.6 through .NET 10" /></a>
  <a href="docs/articles/platform-support.md"><img src="https://img.shields.io/badge/platform-Windows%20x64%20Alpha-0078D4" alt="Windows x64 Alpha" /></a>
</p>

<p align="center">
  <a href="docs/index.md"><img src="https://img.shields.io/badge/docs-DocFX-2f80ed" alt="DocFX documentation" /></a>
  <a href="docs/releases/2.0.0-alpha.1.md"><img src="https://img.shields.io/badge/release-2.0.0--alpha.1-f59e0b" alt="DeploySharp 2.0.0-alpha.1" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/releases"><img src="https://img.shields.io/github/v/release/guojin-yan/DeploySharp?include_prereleases&amp;label=GitHub%20Release" alt="GitHub Release" /></a>
</p>

<p align="center"><strong>English</strong> | <a href="README_cn.md">简体中文</a></p>

# DeploySharp

DeploySharp V2 provides explicit contracts for model artifacts, typed tensors, sessions, visual pipelines, language/multimodal workflows, ModelPack integrity, ModelFactory acquisition, and replaceable inference backends. Applications own model files and native runtimes; DeploySharp keeps backend selection and execution boundaries visible.

## 📖 Introduction

- **Stable application contracts:** model identity, typed tensors, named inputs/outputs, sessions, diagnostics, cancellation, and disposal.
- **Complete inference workflows:** classification, detection, segmentation, pose, OBB, OCR, anomaly, promptable segmentation, vision-language, LLM, and multimodal paths.
- **Explicit backend ownership:** ONNX Runtime, OpenVINO, OpenCV DNN, TensorRT/CUDA, and LLamaSharp adapters without silently installing every vendor runtime.
- **Reproducible model delivery:** ModelPack manifests, artifact size/SHA-256 checks, versioned Release downloads, offline cache reuse, and a runnable model case.

The V2 API is a clean redesign and does not provide V1 source, binary, configuration, or behavior compatibility.

## ✨ Release Highlights

- Core, Visual, LLM, Multimodal, ModelPack, ModelFactory, five backend families, and seven grouped sample modules.
- The model catalog, model/backend verification matrix, and named-device measurements are maintained as public documents.
- Windows x64 verification for ONNX Runtime, OpenVINO, OpenCV DNN, and named TensorRT/CUDA environments.
- Session pools, batching, asynchronous visual inference, sliding-window detection, and repeatable benchmark tooling.

## 📢 Latest Update: 2.0.0-alpha.1

<code>2.0.0-alpha.1</code> is the first DeploySharp V2 engineering preview, focused on source-first Windows 10/11 x64 reproduction while the public API and package surface settle. The complete scope and known boundaries are in the [release notes](docs/releases/2.0.0-alpha.1.md).

## 🚀 Get Started In 30 Seconds

Install the Core layer and the backend you need at the same version. Source-first reproduction can use project references from this repository.

~~~powershell
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
~~~

Create a model artifact, register ONNX Runtime, create a named-tensor session, and run one typed input:

~~~csharp
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();

var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    @"models\classifier.onnx",
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");
using IInferenceSession session = backends.CreateSession(
    artifact, request, SessionOptions.Default);

var input = new Tensor<float>(new TensorShape(1, 3), new[] { 0.1f, 0.2f, 0.7f });
InferenceOutputs outputs = session.Run(InferenceInputs.Create("images", input), CancellationToken.None);
Console.WriteLine(outputs.Count);
~~~

The code-first path, visual preparation, ModelFactory download flow, and model-specific examples are in the [usage tutorial](docs/articles/usage-tutorial.md) and [samples](samples/README.md).

## 📦 Package Layout

| Package family | Contents | Native runtime ownership |
| --- | --- | --- |
| <code>JYPPX.DeploySharp.Core</code> | Models, tensors, sessions, results, diagnostics, backend registration | None |
| <code>JYPPX.DeploySharp.Extensibility</code> | Plugin descriptors, runtime dependencies, options schemas, and native probes | None; host-owned probing |
| <code>JYPPX.DeploySharp.Visual</code> | Visual profiles, preprocessing metadata, decoders, canonical results | None |
| <code>JYPPX.DeploySharp.Visual.OpenCV</code> | OpenCV image loading and tensor preparation | Application selects OpenCV runtime |
| <code>JYPPX.DeploySharp.Visual.TensorRT</code> | CUDA preprocessing and device-resident TensorRT visual pipelines | Application provides TensorRT, CUDA, bridge, and engine |
| <code>JYPPX.DeploySharp.LLM</code> / <code>Multimodal</code> | Generation, chat, embeddings, ordered media, streaming | Application selects model runtime |
| <code>JYPPX.DeploySharp.ModelPack.Json</code> / <code>ModelFactory</code> | Manifests, integrity validation, catalog downloads, offline cache | None; model files stay application-owned |
| <code>JYPPX.DeploySharp.Backend.*</code> | ONNX Runtime, OpenVINO, OpenCV DNN, TensorRT, and LLamaSharp adapters | Backend-specific and explicit |

### Recommended Package Combinations

| Scenario | DeploySharp packages | Application-owned runtime |
| --- | --- | --- |
| ONNX Runtime visual inference | `Core` + `Visual` + `Visual.OpenCV` + `Backend.OnnxRuntime` | ONNX Runtime CPU/CUDA package and model files |
| OpenVINO or OpenCV DNN visual inference | `Core` + `Visual` + `Visual.OpenCV` + the matching `Backend.*` | Matching OpenVINO/OpenCV native runtime |
| TensorRT CUDA visual inference | `Core` + `Visual` + `Visual.TensorRT` + `Backend.TensorRT` | CUDA, cuDNN, TensorRT, bridge, and compatible Engine |
| LLM or multimodal inference | `Core` + `LLM` or `Multimodal` + the selected backend | GGUF/model files and selected native runtime |
| Model catalog/cache and plugin probing | `Core` + `ModelPack.Json` + `ModelFactory` and/or `Extensibility` | Application-owned catalog, cache, and probe host |

See the [detailed package combination and installation guide](docs/articles/package-combinations.md) for commands, TFM/RID notes, and runtime ownership.

## 🌐 Public Packages And Release Assets

DeploySharp packages are not yet published to nuget.org. The exact package IDs and `2.0.0-alpha.1` candidate version remain visible here; each NuGet badge points to its real package page and will show the published version automatically after the first release.

| Package | Candidate version | NuGet.org | Purpose |
| --- | --- | --- | --- |
| `JYPPX.DeploySharp.Core` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Core.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Core) | Core contracts and backend registration |
| `JYPPX.DeploySharp.Extensibility` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Extensibility.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Extensibility) | Plugin descriptors, runtime probes, and options schemas |
| `JYPPX.DeploySharp.Visual` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Visual.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Visual) | Visual profiles, preprocessing, and decoders |
| `JYPPX.DeploySharp.Visual.OpenCV` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Visual.OpenCV.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Visual.OpenCV) | OpenCV image preparation |
| `JYPPX.DeploySharp.Visual.TensorRT` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Visual.TensorRT.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Visual.TensorRT) | CUDA preprocessing and TensorRT visual pipelines |
| `JYPPX.DeploySharp.LLM` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.LLM.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.LLM) | LLM generation and embedding contracts |
| `JYPPX.DeploySharp.Multimodal` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Multimodal.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Multimodal) | Ordered multimodal orchestration |
| `JYPPX.DeploySharp.ModelPack.Json` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.ModelPack.Json.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.ModelPack.Json) | Model manifests and integrity validation |
| `JYPPX.DeploySharp.ModelFactory` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.ModelFactory.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.ModelFactory) | Catalog selection, download, cache, and offline reuse |
| `JYPPX.DeploySharp.Backend.OnnxRuntime` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Backend.OnnxRuntime.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Backend.OnnxRuntime) | ONNX Runtime named-tensor adapter |
| `JYPPX.DeploySharp.Backend.OpenVINO` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Backend.OpenVINO.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Backend.OpenVINO) | OpenVINO named-tensor adapter |
| `JYPPX.DeploySharp.Backend.OpenCV` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Backend.OpenCV.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Backend.OpenCV) | OpenCV DNN adapter |
| `JYPPX.DeploySharp.Backend.TensorRT` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Backend.TensorRT.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Backend.TensorRT) | TensorRT inference and ONNX-to-engine boundary |
| `JYPPX.DeploySharp.Backend.LlamaSharp` | `2.0.0-alpha.1` | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.DeploySharp.Backend.LlamaSharp.svg?label=version)](https://www.nuget.org/packages/JYPPX.DeploySharp.Backend.LlamaSharp) | LLamaSharp GGUF generation and embeddings |

| Publication channel | Current status | Assets |
| --- | --- | --- |
| [NuGet.org](https://www.nuget.org/) | DeploySharp packages await their first publication | Public managed package feed |
| [GitHub Packages](https://github.com/guojin-yan/DeploySharp/packages) | DeploySharp packages await their first publication | Package mirror |
| [GitHub Releases](https://github.com/guojin-yan/DeploySharp/releases) | Used for model artifact delivery | Immutable ModelPack assets and verification metadata |

### Default Test Images

Examples and benchmark tools use the dedicated [`test-assets.1` release](https://github.com/guojin-yan/DeploySharp/releases/tag/test-assets.1) when no local image is supplied. The task defaults are `bus.jpg` for detection/segmentation, `demo_7.jpg` for classification, `demo_9.jpg` for pose, `plane.png` for oriented detection, and `ocr-demo_1.jpg` for PaddleOCR. Files are SHA-256 verified and cached under `%LOCALAPPDATA%\DeploySharp\TestImages`; the full mapping and maintenance commands are in the [default test images guide](docs/articles/test-images.md).

### Application-Owned Runtime Packages

These are application dependencies/runtime packages used by the Windows Alpha. Referencing a DeploySharp managed package does not silently install them:

| Package | NuGet | Purpose |
| --- | --- | --- |
| [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | [![NuGet version](https://img.shields.io/nuget/v/Microsoft.ML.OnnxRuntime.svg?label=version)](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | ONNX Runtime CPU native execution |
| [JYPPX.OpenCV.runtime.win-x64](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.OpenCV.runtime.win-x64.svg?label=version)](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | Windows x64 OpenCV native runtime |
| [OpenVINO.runtime.win](https://www.nuget.org/packages/OpenVINO.runtime.win/) | [![NuGet version](https://img.shields.io/nuget/v/OpenVINO.runtime.win.svg?label=version)](https://www.nuget.org/packages/OpenVINO.runtime.win/) | Windows OpenVINO native runtime |
| [JYPPX.TensorRT.CSharp.API](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.TensorRT.CSharp.API.svg?label=version)](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | Managed TensorRT/CUDA API; NVIDIA libraries remain application-installed |
| [LLamaSharp.Backend.Cpu](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | [![NuGet version](https://img.shields.io/nuget/v/LLamaSharp.Backend.Cpu.svg?label=version)](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | CPU native backend for LLamaSharp GGUF workflows |

The managed package tables do not mean native dependencies are installed automatically. See [installation and runtime ownership](docs/articles/installation.md) before selecting a deployment RID.

## 🖥️ Platforms And Frameworks

| Platform | Build/package boundary | Inference verification |
| --- | --- | --- |
| Windows 10 x64 | Alpha support | ONNX Runtime, OpenVINO, OpenCV DNN CPU; named TensorRT GPU evidence |
| Windows 11 x64 | Alpha support | Same code path; named-device evidence is recorded separately |
| Windows ARM64, Linux, macOS, mobile, NPU | No Alpha inference claim | Not yet verified for this release |

The complete framework list and backend evidence are in [platform and backend support](docs/articles/platform-support.md). Build compatibility is not the same as inference verification.

## 🤖 Supported Models

The catalog covers YOLO, DETR, PaddleOCR v5 Preview, PaDiM, BRIA RMBG, SAM, CLIP, BLIP, and Qwen GGUF. PaddleOCR v4/v6 also have local pipeline evidence but are not presented as downloadable catalog entries. Use the [model support guide](docs/articles/model-support.md) for catalog IDs and the [model/backend matrix](docs/model-backend-verification-matrix.md) for current backend cells.

## 🧪 Example Series

| Module | Demonstration |
| --- | --- |
| <code>01-core</code> | Backend-neutral model/tensor lifecycle |
| <code>02-visual</code> | Visual profiles, preprocessing metadata, asynchronous inference, and sliding-window detection |
| <code>03-backends</code> | Native backend loading and named-tensor execution |
| <code>04-multimodal</code> | Ordered media, streaming, cancellation, and cleanup |
| <code>05-llm</code> | Conversation history and prompt formatting |
| <code>06-models</code> | Catalog selection, model cases, Release download/inference |
| <code>07-benchmarks</code> | Same-model backend/platform latency and throughput |

See the [sample learning path](samples/README.md).

## 📚 Documentation

| Resource | Link | Purpose |
| --- | --- | --- |
| Documentation index | [docs/index.md](docs/index.md) | Public DocFX entry point |
| First release notes | [2.0.0-alpha.1](docs/releases/2.0.0-alpha.1.md) | Release scope and known boundaries |
| Usage tutorial | [Usage tutorial](docs/articles/usage-tutorial.md) | Code-first tensor and visual workflows |
| Platform/backend support | [Support table](docs/articles/platform-support.md) | Target framework and verification boundaries |
| Model support | [Model guide](docs/articles/model-support.md) | Catalog IDs, families, and status semantics |
| Device measurements | [Named-device results](docs/articles/device-performance-benchmarks.md) | Reproducible environment and timing records |

## 🔨 Build From Source

~~~powershell
dotnet restore DeploySharp.sln --locked-mode
dotnet build DeploySharp.sln -c Release --no-restore
dotnet test DeploySharp.sln -c Release --no-build --no-restore
~~~

## ⚖️ License

DeploySharp source code is licensed under the [Apache License 2.0](LICENSE.txt). Models and vendor runtimes are separate artifacts with their own runtime and distribution terms.

## 🤝 Contact And Sponsorship

For questions, issue reports, testing feedback, or sponsorship, please use the project homepage and issue tracker.

<p align="center">
  <img src="docs/images/readme/contact-support-en.png" width="100%" alt="Developer contact channels, community entry points, and sponsorship QR codes">
</p>

---

## ⚠️ Software Notice And Disclaimer

### 📜 1. Open Source License Notice

All open-source project code authored by the project author follows the **Apache License 2.0**.

*Special note: This project integrates several third-party libraries. If any third-party library uses a license that conflicts with or differs from Apache 2.0, the original license of that third-party library takes precedence. This project does not include or represent the license notices of those third-party libraries. Read and comply with the relevant third-party licenses before use.*

### 🤖 2. Code Development And Quality Notice

- **AI-assisted development**: AI assistance was used to generate and improve parts of this code during development; it was not written entirely line by line by a human.
- **Security statement**: **The author solemnly declares that this code contains no intentionally planted backdoors, viruses, Trojans, or malicious code intended to damage user devices or steal data.**
- **Technical limitations**: Due to the author's technical level and capabilities, the code may contain basic issues caused by insufficiently rigorous logic, incomplete optimization, or limited experience, including but not limited to memory leaks, occasional crashes, and unreleased resources. These issues are the result of limitations in ability and are not intentional.
- **Testing scope**: Because the author's time is limited, this software has not been fully tested across every scenario and edge case.

### 🚨 3. Important Disclaimer

**Before applying this code to any real-world project, especially a commercial, industrial, or mission-critical environment, you must perform thorough and rigorous independent testing and validation.** In view of the possible defects and limited test coverage described above, **the author accepts no responsibility for any direct or indirect loss caused by using this code, including but not limited to equipment failure, data loss, system outage, or loss of profits.** By using this code, you acknowledge these risks and agree to bear all consequences independently; related issues are not the responsibility of the author.

### 🔓 4. Scope Of Open Source Code

The core logic of this project is fully open source. However, the binary files, source code, and related resources of the third-party libraries mentioned above are outside this project's open-source obligations. Obtain and use them according to their respective instructions and licenses.

### 🤝 5. Community And Feedback

Despite these limitations, everyone is welcome to download and use the project, submit Issues, and participate in testing. If you find a bug, memory overflow, or improvement opportunity, please use the contact channels on the project homepage. We will do our best to provide assistance within the time available.
