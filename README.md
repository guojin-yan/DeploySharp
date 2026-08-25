<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/readme/hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/images/readme/hero-light.svg">
  <img alt="DeploySharp - reproducible AI inference workflows for .NET" src="docs/images/readme/hero-light.svg" width="100%">
</picture>

<p align="center">
  A modular .NET model deployment toolkit for reproducible vision, language, and multimodal inference across replaceable backends.
</p>

<p align="center">
  <a href="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml?query=branch%3ADeploySharpV2.0"><img src="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml/badge.svg?branch=DeploySharpV2.0" alt="Windows CI" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/LICENSE.txt"><img src="https://img.shields.io/badge/License-Apache%202.0-blue.svg" alt="Apache-2.0 license" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/DeploySharp?style=flat&amp;label=stars" alt="GitHub stars" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET Framework 4.6 through .NET 10" /></a>
  <a href="docs/articles/platform-support.md"><img src="https://img.shields.io/badge/platform-Windows%20x64%20Alpha-0078D4" alt="Windows x64 Alpha" /></a>
</p>

<p align="center">
  <a href="docs/index.md"><img src="https://img.shields.io/badge/docs-DocFX-2f80ed" alt="DocFX documentation" /></a>
  <a href="docs/articles/release-2.0.0-alpha.1.md"><img src="https://img.shields.io/badge/release-2.0.0--alpha.1-f59e0b" alt="DeploySharp 2.0.0-alpha.1" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/releases"><img src="https://img.shields.io/github/v/release/guojin-yan/DeploySharp?include_prereleases&amp;label=GitHub%20Release" alt="GitHub Release" /></a>
</p>

<p align="center"><strong>English</strong> | <a href="README_cn.md">简体中文</a></p>

# DeploySharp

DeploySharp V2 provides explicit contracts for model artifacts, typed tensors, sessions, visual pipelines, language/multimodal workflows, ModelPack integrity, ModelFactory acquisition, and replaceable inference backends. Application code owns the model files and native runtimes; DeploySharp keeps backend selection and execution boundaries visible.

## 📖 Introduction

The project is designed around four practical boundaries:

- **Stable application contracts:** model identity, typed tensors, named inputs/outputs, sessions, diagnostics, cancellation, and disposal.
- **Complete inference workflows:** classification, detection, segmentation, pose, OBB, OCR, anomaly, promptable segmentation, vision-language, LLM, and multimodal paths.
- **Explicit backend ownership:** ONNX Runtime, OpenVINO, OpenCV DNN, TensorRT/CUDA, and LLamaSharp adapters without silently installing every vendor runtime.
- **Reproducible model delivery:** ModelPack manifests, artifact size/SHA-256 checks, immutable Release downloads, offline cache reuse, and one runnable case for every catalog model.

The V2 API is a clean redesign and does not provide V1 source, binary, configuration, or behavior compatibility.

## ✨ Release Highlights

- Core, Visual, LLM, Multimodal, ModelPack, ModelFactory, five backend families, and seven grouped sample modules.
- 42 Preview catalog entries, 43 artifact variants, and a generated model/backend verification matrix.
- Windows x64 CPU verification for ONNX Runtime, OpenVINO, and OpenCV DNN; local TensorRT 11 + CUDA 12.9 evidence on an RTX 3060.
- A repeatable cross-backend speed sample that reports warm latency, P50/P95, throughput, managed allocations, and environment metadata.
- Bilingual API documentation and a DocFX site, with engineering/audit history separated from the user documentation path.

## 📢 Latest Update: 2.0.0-alpha.1

<code>2.0.0-alpha.1</code> is the first DeploySharp V2 engineering preview. It is currently a source-first Windows 10/11 x64 release while the public API and package surface settle.

The complete first-release change list, verification snapshot, known boundaries, and reproduction commands are in the [2.0.0-alpha.1 release notes](docs/articles/release-2.0.0-alpha.1.md). Future releases will add one detailed version document and keep this page at summary level.

## 🚀 Get Started In 30 Seconds

### 1. Install the packages

The Alpha packages are produced locally as versioned Release candidates. When the package feed is available, install the Core layer and the backend you need at the same version; for source-first reproduction, use project references from this repository.

~~~powershell
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
~~~

Native runtime ownership remains explicit. OpenCV DNN and OpenVINO require the matching native runtime package for the target RID; TensorRT requires the user-installed TensorRT/CUDA/cuDNN stack.

### 2. Write a few lines of C#

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

var input = new Tensor<float>(
    new TensorShape(1, 3),
    new[] { 0.1f, 0.2f, 0.7f });
InferenceOutputs outputs = session.Run(
    InferenceInputs.Create("images", input),
    CancellationToken.None);
Console.WriteLine(outputs.Count);
~~~

The complete code-first path, visual preparation, ModelFactory download flow, and model-specific examples are in the [usage tutorial](docs/articles/usage-tutorial.md) and [samples](samples/README.md).

## 📦 Package Layout

| Package family | Contents | Native runtime ownership |
| --- | --- | --- |
| <code>JYPPX.DeploySharp.Core</code> | Models, tensors, sessions, results, diagnostics, backend registration | None |
| <code>JYPPX.DeploySharp.Visual</code> | Visual profiles, preprocessing metadata, decoders, canonical results | None |
| <code>JYPPX.DeploySharp.Visual.OpenCV</code> | OpenCV image loading and tensor preparation | Application selects OpenCV runtime |
| <code>JYPPX.DeploySharp.LLM</code> / <code>Multimodal</code> | Generation, chat, embeddings, ordered media, streaming | Application selects model runtime |
| <code>JYPPX.DeploySharp.ModelPack.Json</code> / <code>ModelFactory</code> | Manifests, integrity validation, catalog downloads, offline cache | None; model files stay application-owned |
| <code>JYPPX.DeploySharp.Backend.*</code> | ONNX Runtime, OpenVINO, OpenCV DNN, TensorRT, and LLamaSharp adapters | Backend-specific and explicit |

## 🌐 Public Packages And Release Assets

The repository currently has no published DeploySharp package on nuget.org. The package IDs and the exact Alpha candidate version are kept visible so the first publication can be reproduced without changing application references.

| Package | Version | NuGet.org | GitHub Packages | Purpose |
| --- | --- | --- | --- | --- |
| <code>JYPPX.DeploySharp.Core</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Core) | Not published | Core contracts and backend registration |
| <code>JYPPX.DeploySharp.Visual</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Visual) | Not published | Visual profiles, preprocessing, and decoders |
| <code>JYPPX.DeploySharp.Visual.OpenCV</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Visual.OpenCV) | Not published | OpenCV image preparation |
| <code>JYPPX.DeploySharp.LLM</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.LLM) | Not published | LLM generation and embedding contracts |
| <code>JYPPX.DeploySharp.Multimodal</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Multimodal) | Not published | Ordered multimodal orchestration |
| <code>JYPPX.DeploySharp.ModelPack.Json</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.ModelPack.Json) | Not published | Model manifest and integrity validation |
| <code>JYPPX.DeploySharp.ModelFactory</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.ModelFactory) | Not published | Catalog selection, downloads, cache, and offline reuse |
| <code>JYPPX.DeploySharp.Backend.OnnxRuntime</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OnnxRuntime) | Not published | ONNX Runtime named-tensor adapter |
| <code>JYPPX.DeploySharp.Backend.OpenVINO</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OpenVINO) | Not published | OpenVINO named-tensor adapter |
| <code>JYPPX.DeploySharp.Backend.OpenCV</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OpenCV) | Not published | OpenCV DNN adapter |
| <code>JYPPX.DeploySharp.Backend.TensorRT</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.TensorRT) | Not published | TensorRT inference and ONNX-to-engine boundaries |
| <code>JYPPX.DeploySharp.Backend.LlamaSharp</code> | <code>2.0.0-alpha.1</code> | Not published; [search](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.LlamaSharp) | Not published | LLamaSharp GGUF generation and embeddings |

| Release channel | Current status | Assets |
| --- | --- | --- |
| [NuGet.org](https://www.nuget.org/) | DeploySharp package publication is pending | Future managed package feed |
| [GitHub Packages](https://github.com/guojin-yan/DeploySharp/packages) | DeploySharp package publication is pending | Future package mirror |
| [GitHub Releases](https://github.com/guojin-yan/DeploySharp/releases) | Used for model artifact delivery | Immutable ModelPack assets and verification metadata |

### Application-owned runtime packages

These are dependency/runtime packages used by the current Windows Alpha. They are not silently installed by the DeploySharp managed contracts:

| Package | Version | Role |
| --- | --- | --- |
| [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | [![NuGet version](https://img.shields.io/nuget/v/Microsoft.ML.OnnxRuntime.svg?label=version)](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | ONNX Runtime CPU native execution |
| [JYPPX.OpenCV.runtime.win-x64](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.OpenCV.runtime.win-x64.svg?label=version)](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | Windows x64 OpenCV native runtime |
| [OpenVINO.runtime.win](https://www.nuget.org/packages/OpenVINO.runtime.win/) | [![NuGet version](https://img.shields.io/nuget/v/OpenVINO.runtime.win.svg?label=version)](https://www.nuget.org/packages/OpenVINO.runtime.win/) | Windows OpenVINO native runtime |
| [JYPPX.TensorRT.CSharp.API](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.TensorRT.CSharp.API.svg?label=version)](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | Managed TensorRT/CUDA API; NVIDIA libraries remain user-installed |
| [LLamaSharp.Backend.Cpu](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | [![NuGet version](https://img.shields.io/nuget/v/LLamaSharp.Backend.Cpu.svg?label=version)](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | CPU native backend for LLamaSharp GGUF workflows |

Native dependencies are never implied by the managed package table. See [installation and runtime ownership](docs/articles/installation.md) before selecting a deployment RID.

## 🖥️ Platforms And Frameworks

| Platform | Build/package boundary | Inference verification | Native runtime/package |
| --- | --- | --- | --- |
| Windows 10 x64 | Supported for Alpha | ONNX Runtime, OpenVINO, OpenCV DNN CPU; local TensorRT GPU evidence | Verified Windows packages and local NVIDIA stack |
| Windows 11 x64 | Supported for Alpha | Same code path as Windows 10 x64 | Matching Windows x64 runtime packages |
| Windows ARM64 | Build scope only | Not tested | Deferred |
| Linux x64/ARM64 | Managed source may build | Not tested in this Alpha | Deferred; install matching vendor runtime when enabled |
| macOS x64/ARM64 | Managed source may build | Not tested in this Alpha | Deferred |
| Android/iOS/NPU | No release claim | Not tested | Deferred |

The complete framework list and backend evidence are in [platform and backend support](docs/articles/platform-support.md). Build compatibility is not the same as inference verification.

## 🤖 Supported Models

The first catalog contains 42 Preview entries and 43 artifact variants:

| Family | Entries | Current scope |
| --- | ---: | --- |
| YOLO v5-v13/v26 | 22 | Detection, classification, segmentation, pose, and OBB |
| DETR family | 8 | DEIMv2, PP-YOLOE, RF-DETR, and RT-DETR variants |
| PP-OCRv5 | 6 | Mobile/server classification, detection, and recognition |
| Anomalib / BRIA | 3 entries / 4 artifacts | PaDiM, RMBG 1.4, RMBG 2.0 fp32/dynamic-int8 |
| Vision-language / segmentation / LLM | 4 | CLIP, BLIP, SAM, and Qwen GGUF |

Use the [model support guide](docs/articles/model-support.md) for all catalog IDs and the [43-artifact model/backend matrix](docs/model-backend-verification-matrix.md) for each current cell.

## 🧪 Example Series

Samples are organized by complete workflows rather than one sample per method:

| Module | Demonstration |
| --- | --- |
| <code>01-core</code> | Backend-neutral model/tensor lifecycle |
| <code>02-visual</code> | Visual profiles, preprocessing metadata, decoder ownership |
| <code>03-backends</code> | OpenCV DNN native loading and named-tensor execution |
| <code>04-multimodal</code> | Ordered media, streaming, cancellation, and cleanup |
| <code>05-llm</code> | Conversation history and prompt formatting |
| <code>06-models</code> | Catalog selection, model cases, Release download/inference |
| <code>07-benchmarks</code> | Same-model backend/platform latency and throughput |

See the [sample learning path](samples/README.md). The speed runner writes an optional JSON report and records unavailable native runtimes explicitly.

## 📚 Documentation

| Resource | Link | Purpose |
| --- | --- | --- |
| Documentation index | [docs/index.md](docs/index.md) | DocFX entry point and bilingual guide index |
| First release notes | [2.0.0-alpha.1](docs/articles/release-2.0.0-alpha.1.md) | Complete initial version snapshot |
| Usage tutorial | [Usage tutorial](docs/articles/usage-tutorial.md) | Code-first tensor and visual workflows |
| Platform/backend support | [Support table](docs/articles/platform-support.md) | Target frameworks and verification boundaries |
| Model support | [Model guide](docs/articles/model-support.md) | Catalog IDs, families, and status semantics |
| Performance benchmark | [Benchmark guide](docs/articles/performance-benchmarking.md) | Cross-backend and cross-platform methodology |
| Engineering history | [History](docs/history/README.md) | Maintainer-only development records, separate from user guides |

## 🔨 Build From Source

~~~powershell
dotnet restore DeploySharp.sln --locked-mode
dotnet build DeploySharp.sln -c Release --no-restore
dotnet test DeploySharp.sln -c Release --no-build --no-restore
~~~

The current Windows validation uses the isolated cache documented in the release notes. The default global cache may contain a known upstream NU1403 mismatch for OpenVINO; this is a local package-cache issue, not a DeploySharp API failure.

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
