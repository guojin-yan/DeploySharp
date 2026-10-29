# NanoDet 自定义前后处理

对应[基础第 07 篇](../../2026-10-29-07-custom-visual.md)。使用正式 NuGet `2.0.0`，独立定义 NanoDet 的图像前处理、六输出 Decoder 与 `ObjectDetections`。工程只运行一个检测模型，输出检测图和 JSON。

需要 .NET 10 SDK、x64。Windows / Ubuntu 22.04 的图像与推理原生包已在 `.csproj` 配置。两张示例图片随工程提供，来源见 [demo-inputs](demo-inputs/README.md)。

## 准备模型

将固定版本的模型保存为 `models/object_detection_nanodet_2022nov.onnx`：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
Invoke-WebRequest 'https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx' `
  -OutFile models/object_detection_nanodet_2022nov.onnx
```

Ubuntu 可用 `curl -fL '<上面的 URL>' -o models/object_detection_nanodet_2022nov.onnx`。模型目录使用 Apache 2.0。

## 运行

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --inspect
dotnet run -c Release --no-build -- --backend ort-cpu --image demo-inputs/road-00.jpg
dotnet run -c Release --no-build -- --backend openvino --image demo-inputs/courtyard-00.jpg
```

Ubuntu 在构建后设置：

首次构建可能出现 `NETSDK1206`（旧 Ubuntu RID）警告；工程已显式复制原生文件，下面的搜索路径指向这些文件。

```bash
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/CustomVisual.dll --backend ort-cpu --image demo-inputs/road-00.jpg
dotnet bin/Release/net10.0/CustomVisual.dll --backend openvino --image demo-inputs/courtyard-00.jpg
```

其他参数：`--model`、`--out`、`--threshold 0.35`、`--nms 0.6`。`--inspect` 使用 ORT 查看 ONNX 真实端口，不运行检测。

输出：`results/<backend>/<图片名>/result.jpg` 和 `detections.json`。再次运行会覆盖同名输出。退出码为成功 `0`、错误 `1`、取消 `2`。

`NanoDetModel.cs` 包含所有自定义接入逻辑，前处理为 RGB、黑边 Letterbox、Floor、原像素域 mean/std，输入 NCHW 416。适配器 Linear 与 Zoo demo 的 Area 插值不同。结果含实际误报，适合验证接入路径；自己的模型需一起调整端口、形状、类别、前处理和 Decoder。

多个模型联合的完整应用已迁至[园区巡检](../../../applications/2026-10/examples/01-campus-inspection/README.md)。
