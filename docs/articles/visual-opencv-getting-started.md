# Visual.OpenCV 快速开始

JYPPX.DeploySharp.Visual.OpenCV 是 Visual 的图像输入适配器。它将文件、字节或流解码为后端无关的 PreparedVisualInput，不直接引用 ONNX Runtime、OpenVINO 或 OpenCV DNN。

## 安装

应用需要同时引用：

~~~powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0-alpha.1
dotnet add package JYPPX.OpenCV.CSharp.API --version 5.0.0-preview.1
dotnet add package JYPPX.OpenCV.runtime.win-x64 --version 5.0.0-preview.1
~~~

DeploySharp 包只包含托管引用，native DLL 由应用显式安装。当前已验证 Windows x64；其他 RID 和设备需在目标机单独验证。

## 准备输入

~~~csharp
var options = new OpenCvPreprocessOptions(
    new VisualSize(224, 224),
    resizeMode: OpenCvResizeMode.Letterbox,
    colorOrder: VisualColorOrder.Rgb,
    means: new[] { 123.675f, 116.28f, 103.53f },
    standardDeviations: new[] { 58.395f, 57.12f, 57.375f },
    layout: VisualTensorLayout.Nchw);

using PreparedVisualInput input =
    new OpenCvVisualInputFactory().CreateFromFile(
        Path.GetFullPath("image.jpg"), "images", options,
        cancellationToken);
~~~

输入工厂会记录源图尺寸和可逆的 ImageTransform。Letterbox、CenterCrop、RGB/BGR、灰度和 alpha 处理都由不可变选项声明；调用方不应在后端再次归一化或重复交换通道。

## 生命周期与取消

FromFile 只接受绝对普通文件；FromStream/FromBytes 会在边界内读取编码数据。PreparedVisualInput 默认由调用方拥有，使用完后 Dispose。取消会在解码、几何变换和有界行边界检查；native 单次调用返回后才会观察到取消。

OpenCV DNN 的动态 shape、辅助输入和 importer 限制见[OpenCV DNN 兼容性](visual-opencv-compatibility.md)。后端无关的推理调用见[Visual 快速开始与生命周期](visual-getting-started.md)。
