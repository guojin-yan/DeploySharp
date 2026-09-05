# Third-party notices

This file covers third-party software used to build or run the current `2.0.0-alpha.1` source tree. It is a dependency notice, not an approval of any model license or a grant to redistribute model weights. Model assets have separate source/license records under `eng/models/` and ModelPack manifests.

| Component | Version used by this repository | License/source boundary |
| --- | --- | --- |
| .NET / .NET Framework reference assemblies | `net46`-`net10.0`; `Microsoft.NETFramework.ReferenceAssemblies 1.0.3` for legacy builds | Microsoft runtime and reference-assembly terms apply to the runtime/SDK selected by the consumer. Source: [dotnet](https://github.com/dotnet), [reference assemblies](https://github.com/microsoft/dotnet/tree/main/src/reference-assemblies). |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT; test-only dependency. Source: [vstest](https://github.com/microsoft/vstest). |
| MSTest.TestAdapter / MSTest.TestFramework | 4.3.3 | MIT; test-only dependency. Source: [testfx](https://github.com/microsoft/testfx). |
| LLamaSharp / LLamaSharp.Backend.Cpu | 0.27.0 | MIT managed/native bridge packages selected by the consumer; native assets remain consumer-owned. Source: [LLamaSharp](https://github.com/SciSharp/LLamaSharp). |
| Microsoft.Bcl.AsyncInterfaces | 10.0.10 central version (transitive package metadata may resolve a compatible asset) | MIT; compatibility dependency. Source: [dotnet/runtime](https://github.com/dotnet/runtime). |
| Microsoft.ML.OnnxRuntime.Managed / Microsoft.ML.OnnxRuntime | 1.28.0 | Microsoft ONNX Runtime license/notice applies; native provider/runtime selection remains consumer-owned. Source: [onnxruntime](https://github.com/microsoft/onnxruntime). |
| Microsoft.ML.OnnxRuntime.Gpu.Windows | 1.28.0 | Microsoft ONNX Runtime CUDA provider package; CUDA, cuDNN, NVIDIA driver and native runtime selection remains consumer-owned. Source: [onnxruntime](https://github.com/microsoft/onnxruntime). |
| Microsoft.ML.Tokenizers | 2.0.0 | MIT. Source: [dotnet/machinelearning](https://github.com/dotnet/machinelearning). |
| OnnxSharp | 0.3.2 | MIT; ONNX format parsing/manipulation dependency used by the TensorRT adapter. Source: [OnnxSharp](https://github.com/nietras/OnnxSharp). |
| System.Text.Json | 10.0.10 central version | MIT. Source: [dotnet/runtime](https://github.com/dotnet/runtime). |
| JYPPX.OpenVINO.CSharp.API / OpenVINO.runtime.win | 3.3.1 / 2026.2.1 | Upstream package/runtime license and notices apply; DeploySharp does not redistribute the native runtime in its managed packages. Source: [OpenVINO-CSharp-API](https://github.com/guojin-yan/OpenVINO-CSharp-API). |
| JYPPX.TensorRT.CSharp.API | 4.0.0 central dependency; exact admitted public package is reviewed in ADR 0034 | The exact managed package declares Apache-2.0; TensorRT/CUDA/cuDNN/NVIDIA driver and native bridge licenses remain consumer-owned and are not asserted here. Source: [TensorRT-CSharp-API](https://github.com/guojin-yan/TensorRT-CSharp-API). |
| JYPPX.OpenCV.CSharp.API / JYPPX.OpenCV.runtime.win-x64 | 5.0.0-preview.1 | Upstream preview package declares Apache-2.0; native runtime remains consumer-owned. Source: [OpenCV-CSharp-API](https://github.com/guojin-yan/OpenCV-CSharp-API). |

DeploySharp source code and its managed packages are licensed under the repository license in `LICENSE.txt`. That license applies only to DeploySharp-owned code and does not relicense the components above.

## Model and dataset boundary

The catalog's model entries retain per-asset license expressions and redistribution flags. A package notice cannot approve Qwen, Ultralytics, PaddleOCR, BLIP, SAM, CLIP, or any other model/checkpoint/dataset for redistribution. Review the exact ModelPack manifest and release evidence before mirroring an asset.
