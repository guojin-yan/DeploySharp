# 视觉多任务随文案例

配套教程：[DeploySharp 视觉多任务实战](../../2026-10-29-05-visual-multitask.md)。使用正式 NuGet `2.0.0`，在 Windows x64 的 ONNX Runtime CPU 上分别运行分类、实例分割、COCO-17 Pose 与 OBB，保存结果图和 JSON。

## 准备

需要 .NET 10 SDK。项目引用图像适配器、ORT 后端、ORT CPU `1.28.0` 与 OpenCV Windows x64 runtime `5.0.0`，不需要 CUDA。本文没有验证此工程在其他操作系统或其他后端上的运行结果。

按教程第四节下载以下文件，或运行时传入与配置匹配的本地路径：

| 任务 | 本地模型 | 默认图片 | 输入与输出 |
| --- | --- | --- | --- |
| `classify` | `models/yolov8s-cls.onnx` | `images/demo_7.jpg` | 224 中心裁剪；1000 类概率 |
| `segment` | `models/yolo26s-seg.onnx` | `images/bus.jpg` | 640 留边；`[1,300,38]` + `[1,32,160,160]` |
| `pose` | `models/yolo26s-pose.onnx` | `images/demo_9.jpg` | 640 留边；`[1,300,57]` |
| `obb` | `models/yolo26s-obb.onnx` | `images/plane.png` | 1024 留边；`[1,300,7]` |

公共模型来自 [models-visual.1](https://github.com/guojin-yan/DeploySharp/releases/tag/models-visual.1)，图片来自 [test-assets.1](https://github.com/guojin-yan/DeploySharp/releases/tag/test-assets.1)。模型文件、图片和本地输出不提交进 Git。YOLO 模型及其数据、标签遵循上游各自的使用条款；上游项目为 [Ultralytics](https://github.com/ultralytics/ultralytics)。`imagenet-labels.json` 的顺序与名称提取自本例默认 YOLOv8s 分类 ONNX 的 `names` 元数据，不是新的类别定义。

## 运行

在本目录执行：

```powershell
dotnet run -c Release -- all
dotnet run -c Release -- segment
dotnet run -c Release -- pose "models/my-yolo26-pose.onnx" "images/my-person.jpg"
```

单任务参数为 `任务 [模型路径] [图片路径] [分类标签JSON]`。`all` 使用四组默认路径，顺序创建和释放四个模型会话。默认模型都是静态 Batch=1，本例没有把不同任务拼成模型 Batch。

## 代码与输出

- `Program.cs`：选择任务、创建 Pipeline、准备图像、执行推理、取得强类型结果并写 JSON。
- `Profiles.cs`：公开模型的输入输出合同、标签和前后处理；分割/Pose/OBB 复用内置多任务工厂。
- `ResultWriter.cs`：绘图、源图掩码导出、骨架有效性检查和旋转四顶点绘制。
- `imagenet-labels.json`：分类标签，构建时复制到输出目录。

每个任务写入 `results/<task>/result.jpg` 和 `result.json`，分割另存 `mask-NN.png`。JSON 使用源图浮点坐标；掩码以完整源图大小保存，黑色背景、白色前景。绘图器按行处理 `Mat.Step`，不会假设行连续。

本次默认图片的结果为：棕熊 Top-1 `0.9937447`；分割 4 实例（3 人、1 公交车）；Pose 1 人、16/17 有效点；OBB 12 个飞机。具体结果用于验证示例执行与结果消费，不是数据集准确率或性能结论。

替换模型时，先修改 `Profiles.cs` 中的类别、输入尺寸、候选数、输出合同与预处理。主程序直接使用本地 `ModelArtifact`；多任务工厂保留默认导出的记录，但本例不进行模型文件摘要校验。更换同一输入输出合同的权重可直接传路径；自有类别或其他布局需要同步配置解码器。

长时间运行时复用 Pipeline；本次输入、绘图 Mat、Pipeline 和后端注册中心按示例中的 `using` 生命周期释放。图像解码、叠色和保存文件均在 CPU 执行，本例没有启用 TensorRT CUDA 前后处理，也没有输出性能排行。
