# NuGet 包组合与安装指南

DeploySharp 按“核心契约、领域流程、图像适配器、执行后端和模型交付”拆分 NuGet 包。这样可以只安装当前应用需要的能力，并让 CUDA、TensorRT、OpenVINO、OpenCV 和 LLamaSharp 等原生运行时由应用明确管理。

## 安装原则

1. 同一 DeploySharp 版本的包保持一致，例如当前 Alpha 使用 `2.0.0-alpha.1`。
2. `Core` 是所有 DeploySharp 工作流的基础；其他包按任务和后端按需添加。
3. 后端包是托管适配器，不会自动下载或安装厂商 native DLL。
4. 模型文件、Tokenizer、OpenVINO XML/BIN、TensorRT Engine 和字典由应用部署。
5. 能够还原包不等于目标设备已经具备可运行的原生环境；部署前应运行对应 probe 或最小 smoke test。

## 推荐组合速查

| 场景 | DeploySharp 包 | 应用还需准备 |
| --- | --- | --- |
| 纯张量/自定义推理 | `Core` + 一个 `Backend.*` | 对应后端运行时和模型文件 |
| ONNX Runtime 视觉 | `Core` + `Visual` + `Visual.OpenCV` + `Backend.OnnxRuntime` | `Microsoft.ML.OnnxRuntime`（CUDA 另需 GPU 包、驱动和 CUDA/cuDNN） |
| OpenVINO 视觉 | `Core` + `Visual` + `Visual.OpenCV` + `Backend.OpenVINO` | `JYPPX.OpenVINO.CSharp.API` 对应的 OpenVINO Windows runtime |
| OpenCV DNN 视觉 | `Core` + `Visual` + `Visual.OpenCV` + `Backend.OpenCV` | `JYPPX.OpenCV.CSharp.API` 和匹配的 OpenCV runtime |
| TensorRT CUDA 视觉 | `Core` + `Visual` + `Visual.TensorRT` + `Backend.TensorRT` | CUDA、cuDNN、TensorRT、bridge 和目标 GPU 匹配的 Engine |
| LLM/GGUF | `Core` + `LLM` + `Backend.LlamaSharp` | `LLamaSharp.Backend.Cpu` 或应用选择的原生后端，以及 GGUF |
| 多模态工作流 | `Core` + `Multimodal` + `LLM`；视觉任务再加 `Visual` | 具体模型和后端运行时 |
| 模型目录/离线缓存 | `Core` + `ModelPack.Json` + `ModelFactory` | 应用选择的目录、下载权限和缓存目录 |
| 插件发现/运行时探测 | `Core` + `Extensibility` | 应用自己的插件目录、探测进程和用户配置 |

`Visual.OpenCV` 和 `Backend.OpenCV` 不是重复包：前者准备图像和张量，后者执行 OpenCV DNN。`Visual.TensorRT` 和 `Backend.TensorRT` 也分别对应视觉 CUDA 流程和通用 TensorRT 执行。

## 常用安装命令

下面示例使用当前 Alpha 版本；正式发布后只需替换版本号和源地址。

### ONNX Runtime CPU

```powershell
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

### OpenVINO 或 OpenCV DNN

```powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OpenVINO --version 2.0.0-alpha.1
# 或：dotnet add package JYPPX.DeploySharp.Backend.OpenCV --version 2.0.0-alpha.1
```

随后由应用选择与目标 TFM/RID 匹配的 OpenVINO 或 OpenCV runtime。不要把 OpenCV preview runtime 与其他版本混用。

### TensorRT CUDA 视觉

```powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.TensorRT --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.TensorRT --version 2.0.0-alpha.1
```

TensorRT 目前只发布 `net8.0` 托管资产。应用需要自行准备 CUDA、cuDNN、TensorRT、bridge、驱动和与设备/输入 profile 匹配的 Engine；DeploySharp 不会自动下载或再分发这些 NVIDIA 文件。

### 模型目录与插件探测

```powershell
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.ModelPack.Json --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.ModelFactory --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Extensibility --version 2.0.0-alpha.1
```

`ModelFactory` 只处理应用选择的模型目录、下载和缓存；`Extensibility` 只提供插件/运行时描述和探测合同。下载授权、签名信任、进程隔离和 UI 均属于宿主应用。

## 源码优先和 NuGet 消费

当包还未发布到公共源时，可以在仓库内使用 `ProjectReference` 进行源码复现；包发布后再替换为上面的 `PackageReference`。无论哪种方式，都应锁定同一版本的 DeploySharp 包和第三方运行时，并检查 `docs/articles/platform-support.md` 中的目标框架与真实验证范围。

## 选择顺序

1. 先确定任务：张量、视觉、LLM、多模态或模型交付。
2. 再确定后端：ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT 或 LLamaSharp。
3. 按目标 TFM/RID 准备 native runtime，并使用清单或 probe 检查实际路径、版本和 ABI。
4. 最后选择模型制品；TensorRT Engine 必须与 GPU、CUDA、TensorRT、输入 profile 和 bridge 身份一致。

更完整的代码示例见[使用教程](usage-tutorial.md)，平台限制见[平台与后端支持](platform-support.md)，模型选择见[模型支持状态](model-support.md)。
