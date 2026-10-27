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
| Microsoft.ML.OnnxRuntime.Gpu.Windows / Microsoft.ML.OnnxRuntime.Gpu.Linux | 1.28.0 by default; CUDA 12 opt-in uses 1.23.2 | Microsoft ONNX Runtime CUDA provider packages; CUDA, cuDNN, NVIDIA driver and native runtime selection remain consumer-owned. Source: [onnxruntime](https://github.com/microsoft/onnxruntime). |
| Microsoft.ML.Tokenizers | 2.0.0 | MIT. Source: [dotnet/machinelearning](https://github.com/dotnet/machinelearning). |
| OnnxSharp | 0.3.2 | MIT; ONNX format parsing/manipulation dependency used by the TensorRT adapter. Source: [OnnxSharp](https://github.com/nietras/OnnxSharp). |
| System.Text.Json | 10.0.10 central version | MIT. Source: [dotnet/runtime](https://github.com/dotnet/runtime). |
| JYPPX.OpenVINO.CSharp.API / OpenVINO.runtime.win | 3.3.1 / 2026.2.1 | Upstream package/runtime license and notices apply; DeploySharp does not redistribute the native runtime in its managed packages. Source: [OpenVINO-CSharp-API](https://github.com/guojin-yan/OpenVINO-CSharp-API). |
| OpenVINO.runtime.ubuntu.22-x86_64 | 2026.3.1 | Optional Linux runtime for named experiments; native runtime remains consumer-owned. Source: [OpenVINO](https://github.com/openvinotoolkit/openvino). |
| JYPPX.TensorRT.CSharp.API | 4.0.0 | The managed package declares Apache-2.0; TensorRT/CUDA/cuDNN/NVIDIA driver and native bridge licenses remain consumer-owned and are not asserted here. Source: [TensorRT-CSharp-API](https://github.com/guojin-yan/TensorRT-CSharp-API). |
| JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge / JYPPX.TensorRT.CSharp.API.Runtime.linux-x64.ubuntu22.04.trt11.0.cuda12.9.cudnn9.22.Bridge | 4.0.0 | Optional platform-specific bridge packages; NVIDIA runtime libraries remain application-installed. Source: [TensorRT-CSharp-API](https://github.com/guojin-yan/TensorRT-CSharp-API). |
| JYPPX.OpenCV.CSharp.API / JYPPX.OpenCV.runtime.win-x64 / JYPPX.OpenCV.runtime.ubuntu.22.04-x64 | 5.0.0 | Upstream packages declare Apache-2.0; native runtime remains consumer-owned. Source: [OpenCV-CSharp-API](https://github.com/guojin-yan/OpenCV-CSharp-API). |

DeploySharp source code and its managed packages are licensed under the repository license in `LICENSE.txt`. That license applies only to DeploySharp-owned code and does not relicense the components above.

## Model and dataset boundary

The catalog's model entries retain per-asset license expressions and redistribution flags. A package notice cannot approve Qwen, Ultralytics, PaddleOCR, BLIP, SAM, CLIP, or any other model/checkpoint/dataset for redistribution. Review the exact ModelPack manifest and release evidence before mirroring an asset.
