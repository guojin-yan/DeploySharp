# Installation / 安装

The first package will be published as `JYPPX.DeploySharp.Core` starting at version `2.0.0`. During source development, reference the project directly. / 首个包为 `JYPPX.DeploySharp.Core`，版本从 `2.0.0` 开始；源码开发阶段可直接引用项目。

```xml
<ProjectReference Include="..\DeploySharp\src\DeploySharp.Core\DeploySharp.Core.csproj" />
```

Backend packages contain managed adapters only unless their documentation explicitly states otherwise. Vendor runtimes such as TensorRT/CUDA and selectable LLamaSharp native backends are installed separately. / 除非后端文档另有明确说明，后端包只包含托管适配器；TensorRT/CUDA 等厂商运行时以及可选的 LLamaSharp 原生后端需要单独安装。

For an image-library-neutral visual workflow, install `JYPPX.DeploySharp.Visual` plus one backend package. Pixel decoding and preprocessing remain in a separate adapter; the future official default is `JYPPX.DeploySharp.Visual.OpenCV`. / 对于不绑定图像库的视觉流程，安装 `JYPPX.DeploySharp.Visual` 和一个后端包。像素解码与预处理位于独立适配器中；未来官方默认适配器是 `JYPPX.DeploySharp.Visual.OpenCV`。

```powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
```

For ONNX CPU inference, install the managed adapter and the matching application-owned official CPU runtime. / 对于 ONNX CPU 推理，请安装托管适配器和版本匹配、由应用持有的官方 CPU 运行时。

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

See the package-specific support matrix before selecting an older target framework. Package compatibility does not restore security support for an end-of-life .NET runtime. / 选择旧目标框架前请查阅对应包的支持矩阵；包可兼容并不意味着已终止生命周期的 .NET 运行时重新获得安全支持。
