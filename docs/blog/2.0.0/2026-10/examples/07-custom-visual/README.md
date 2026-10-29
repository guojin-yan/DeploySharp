# NanoDet-Plus + CLIP 场景分析随文工程

对应[第 07 篇教程](../../2026-10-29-07-custom-visual.md)。使用正式 NuGet `2.0.0`，在业务代码中自定义 NanoDet-Plus 的前处理、六输出解码及结果类，再联合 CLIP 图像与文本编码器，完成检测 → 源图 ROI → 图像 Batch → 候选描述匹配 → 自定义场景报告。

本例不修改 DeploySharp 主库，也不增加内置 NanoDet Profile。CLIP 只在候选描述中匹配，不自由生成文本，不给检测器增加新类别；服装和车辆外观的匹配结果未经过专项准确率评测。

## 环境与依赖

.NET SDK 10、Windows x64 或 Ubuntu 22.04 x64。同一份 C# 源码由 `.csproj` 按系统选择原生包：

| 职责 | 包与版本 |
| --- | --- |
| 视觉、图像适配器 | `JYPPX.DeploySharp.Visual.OpenCV 2.0.0` |
| ORT CPU | `JYPPX.DeploySharp.Backend.OnnxRuntime 2.0.0`、`Microsoft.ML.OnnxRuntime 1.28.0` |
| OpenVINO | `JYPPX.DeploySharp.Backend.OpenVINO 2.0.0` |
| Windows 原生库 | `JYPPX.OpenCV.runtime.win-x64 5.0.0`、`OpenVINO.runtime.win 2026.2.1` |
| Ubuntu 原生库 | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0`、`OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1` |

`Microsoft.ML.Tokenizers 2.0.0` 来自 Visual 的传递依赖。读者运行不需要 Python、模型转换脚本或本地主库源码。

Linux 原生文件显式复制到 `native/opencv` 和 `native/openvino`。发行版 RID 资产可能出现 `NETSDK1206`，运行前检查输出目录和 `LD_LIBRARY_PATH`。本例原生包面向 Ubuntu 22.04，不代表其他发行版可直接复用。

只用 ORT 的应用可以移除 OpenVINO 引用、原生包和相关复制项，同时移除代码中的 OpenVINO 注册分支。

## 模型与图片

将三个 ONNX 图和 CLIP 的词表、合并表放入 `models`。默认图片保存为 `images/bus.jpg`。模型、图片、结果、bin/obj 均忽略提交。

Windows PowerShell：

```powershell
New-Item -ItemType Directory -Force models, images | Out-Null
Invoke-WebRequest 'https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx' `
  -OutFile models/object_detection_nanodet_2022nov.onnx

$modelRelease = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
foreach ($name in @('image-encoder.onnx', 'text-encoder.onnx', 'vocab.json', 'merges.txt', 'LICENSE')) {
  Invoke-WebRequest "$modelRelease/clip-vit-b-32.$name" -OutFile "models/clip-vit-b-32.$name"
}
Invoke-WebRequest 'https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/bus.jpg' `
  -OutFile images/bus.jpg
```

Ubuntu Bash：

```bash
mkdir -p models images
curl -fL 'https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx' \
  -o models/object_detection_nanodet_2022nov.onnx

model_release='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
for name in image-encoder.onnx text-encoder.onnx vocab.json merges.txt LICENSE; do
  curl -fL "$model_release/clip-vit-b-32.$name" -o "models/clip-vit-b-32.$name"
done
curl -fL 'https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/bus.jpg' \
  -o images/bus.jpg
```

也可以通过浏览器下载，或把已有本地文件放到对应目录。CLIP 两个 FP32 图约 352 MB 和 254 MB；NanoDet 图约 3.8 MB。上游 [OpenCV Zoo 模型目录](https://github.com/opencv/opencv_zoo/tree/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet)采用 Apache 2.0，CLIP 使用资产附带的 MIT 许可。NanoDet 的解码依据该目录 `nanodet.py` 的输出语义实现，前处理使用原始 NanoDet BGR 验证配置的直接缩放。

换模型无需计算 SHA-256，但要同步修改 `NanoDetModel.cs` 或 `ClipModels.cs` 的端口、形状、颜色、几何、归一化和解码规则。不能把任意同名模型当成兼容图。

## 运行

Windows：

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --backend ort-cpu --batch 4
dotnet run -c Release --no-build -- --backend openvino --batch 4
```

Ubuntu 22.04：

```bash
dotnet restore
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/CustomVisual.dll --backend ort-cpu --batch 4
dotnet bin/Release/net10.0/CustomVisual.dll --backend openvino --batch 4
```

默认使用 `models`、`images/bus.jpg`、`prompts.json`，结果写到 `results/<backend>`。完整参数示例：

```text
--backend ort-cpu
--models models
--image images/bus.jpg
--prompts prompts.json
--out results
--threshold 0.35
--nms 0.6
--batch 4
--max-rois 16
--padding 0.06
--review-margin 0.1
```

`--inspect` 打印三个图的实际输入输出，不执行图片任务。NanoDet 图可能产生旧导出格式 initializer 警告，不等于推理失败。

本篇实跑使用相同 `bus.jpg`、12 条候选、`--batch 4` 和默认检测参数：

| 环境 | ORT CPU | OpenVINO CPU |
| --- | --- | --- |
| Windows 11 x64 | 5 个检测、5 个语义 ROI，图像批次 4+1 | 5 个检测、5 个语义 ROI，图像批次 4+1 |
| Ubuntu 22.04.5 x64 | 5 个检测、5 个语义 ROI，图像批次 4+1 | 5 个检测、5 个语义 ROI，图像批次 4+1 |

四组候选 Top-1 与复核标记一致。具体设备、版本和包含误匹配的真实结果见文章第 8.4 节；这不是属性准确率评测，也不扩展为其他模型或后端的验证。

## 候选与输出

`prompts.json` 是分组数组。每组 `ClassIds` 选择 COCO 类别，`Candidates` 至少两个，全局 `Key` 唯一。默认行人组对应 0，车辆组对应 2/5/7，共 12 条英文描述。

本例 tokenizer 限定可打印 ASCII 英文，拒绝特殊 token 和超长文本。12 条默认候选及 3 条标点、大小写、缩写描述已与配套 Hugging Face tokenizer 对照，token ID 与 mask 一致。任意语言需要另外接入和验证适配器。

`scene.json` 保留所有检测、候选、源图框、实际扩边 ROI、Top-3、余弦相似度、候选组内 Softmax 分数和复核标记。没有候选的类别记录 `no-prompt-group`，超出语义区域上限记录 `max-rois-limit`。

`scene.jpg` 绘制框与最优候选 Key，`?` 表示分差不足。`roi-XX.jpg` 是原图裁剪，不是最终 224×224 输入张量。候选分数只表示当前组内的相对匹配，复核规则不是经过标定的业务判定。

## 生命周期

检测和图像 Runner 在循环外创建；文本编码一次后释放其 Session，保留业务特征。推理阶段通过 `OpenCvDecodedRoiImage` 共享一次原图解码；输出绘图工具另外读取原图，保存叠框和 ROI。每个 `PreparedVisualInput` 由 Runner 在推理与解码后释放。特征结果是自己的数组，不持有后端缓冲区；等所有 `await` 完成再释放原图、Runner 与注册中心。

`--batch` 范围 1—16，使用 CLIP 真正的动态 Batch；最后一批按实际数量运行。检测图仍为 Batch=1。本例顺序执行有依赖的阶段，没有自动多 Session 或跨帧流水线。`Ctrl+C` 取消后返回 2；正常失败返回 1；成功返回 0。
