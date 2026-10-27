# 安装与运行时

## 安装 DeploySharp

当前公开版本为 `2.0.0-alpha.1`，已发布到 NuGet.org。使用者可以安装同版本 NuGet 包；需要调试当前分支或验证未发布改动时，也可以直接引用仓库项目。包 ID、职责和当前版本见根目录 README。

当前源码版本为 `2.0.0` 待发布候选，使用指南以该源码为准，可能包含预览版发包后新增的 API。这些功能在正式包发布前需要项目引用；不要把当前源码的全部能力视为已发布预览包的能力。版本范围见 [2.0.0 说明](../releases/2.0.0.md)。

如果不确定需要安装哪些包，请先阅读[NuGet 包组合与安装指南](package-combinations.md)；本文重点说明目标框架和原生运行时的部署边界。

各包的直接 Target Framework 以[平台与后端支持](platform-support.md)为唯一事实来源；安装后端时还要按应用的目标框架选择匹配资产，不能只看项目能够还原成功。

```xml
<ProjectReference Include="..\DeploySharp\src\DeploySharp.Core\DeploySharp.Core.csproj" />
```

使用视觉流程时，安装 `JYPPX.DeploySharp.Visual` 以及一个图像适配器（例如 `JYPPX.DeploySharp.Visual.OpenCV`）。后端包按应用实际需要单独安装。

## 后端运行时

DeploySharp 后端包主要包含托管适配器，不会静默安装厂商原生运行时。应用需要为目标 RID 准备与适配器匹配的运行时：

| 后端 | DeploySharp 适配器 | 应用需要准备 |
| --- | --- | --- |
| ONNX Runtime | `JYPPX.DeploySharp.Backend.OnnxRuntime` | `Microsoft.ML.OnnxRuntime` 或 CUDA 运行时包 |
| OpenVINO | `JYPPX.DeploySharp.Backend.OpenVINO` | Windows x64 OpenVINO runtime |
| OpenCV DNN | `JYPPX.DeploySharp.Backend.OpenCV` | `JYPPX.OpenCV.runtime.win-x64` 等匹配包 |
| TensorRT | `JYPPX.DeploySharp.Backend.TensorRT`、可选 `Visual.TensorRT` | CUDA、cuDNN、TensorRT、匹配 Engine/bridge |
| LLamaSharp | `JYPPX.DeploySharp.Backend.LlamaSharp` | LLamaSharp CPU/CUDA 原生后端与 GGUF |

例如，Windows x64 的 ONNX Runtime CPU 项目可以使用：

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

模型文件、字典、Tokenizer、OpenVINO XML/BIN、TensorRT Engine 和原生 DLL 均由应用负责部署。请勿仅根据包能够还原来推断目标设备已经具备可运行的原生环境。

目标框架兼容性和已验证设备见[平台与后端支持](platform-support.md)；运行真实模型前请查阅[模型 × 后端验证矩阵](../model-backend-verification-matrix.md)。

## 目标框架、RID 与位数

应用目标框架决定托管 API 是否可引用，RID 和进程位数决定 native runtime 是否能加载。建议视觉 Windows 应用显式声明 `win-x64`，并在发布时使用与模型后端一致的 RID：

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PlatformTarget>x64</PlatformTarget>
  <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

不要将 x64 OpenCV、CUDA 或 TensorRT DLL 放进 x86 进程，也不要把不同版本的同名 DLL 混在应用目录和系统 PATH 中。发布目录中应能够从应用启动位置找到所有依赖；生产环境不要依赖开发机的全局 PATH。

## 后端安装检查表

| 检查项 | 通过标准 | 失败时的表现 |
| --- | --- | --- |
| 托管包版本 | DeploySharp 适配器与底层运行时来自声明的兼容组合 | 类型能编译但 Session 创建失败 |
| native 架构 | 所有 DLL 与进程均为 x64 或均为 x86 | `BadImageFormatException`/入口点错误 |
| 模型资产 | 主模型、external data、字典和 tokenizer 都存在且哈希一致 | 模型加载或生成阶段失败 |
| 设备驱动 | `nvidia-smi`/OpenVINO 设备查询与目标后端一致 | Provider/设备不可用 |
| 搜索路径 | 发布目录或显式加载目录包含依赖 | `DS-NATIVE-6001` |

## 发布前自检

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet publish -c Release -r win-x64 --self-contained false
dotnet --info
```

在干净机器上运行发布目录中的自检程序，再运行一张默认测试图片。不要只在包含 Visual Studio、CUDA Toolkit 或 Python 环境的开发机上验证；那些全局依赖可能掩盖部署目录缺失。
