# NanoDet + CLIP：园区车辆与人员巡检

对应[第 07 篇教程](../../2026-10-29-07-custom-visual.md)。使用正式 NuGet `2.0.0`，自定义 NanoDet-Plus 前后处理和结果类，联合 CLIP 双编码器，处理两组固定视角、六张真实截图，输出区域统计、待复核事件、证据与静态 HTML 复核台。

使用公开道路与庭院素材模拟园区流程；不代表现场部署、检测准确率或安全告警产品。CLIP 只做车型候选检索，不参与区域事件触发。输入与来源见 [demo-inputs](demo-inputs/README.md)。

## 环境与 NuGet

需要 .NET SDK 10、Windows x64 或 Ubuntu 22.04 x64。同一 `.csproj` 按系统选择原生包，不引用本地主库项目：

| 职责 | 包与版本 |
| --- | --- |
| 图像适配器与视觉接口 | `JYPPX.DeploySharp.Visual.OpenCV 2.0.0` |
| ORT CPU | `JYPPX.DeploySharp.Backend.OnnxRuntime 2.0.0`、`Microsoft.ML.OnnxRuntime 1.28.0` |
| OpenVINO | `JYPPX.DeploySharp.Backend.OpenVINO 2.0.0` |
| Windows 原生库 | `JYPPX.OpenCV.runtime.win-x64 5.0.0`、`OpenVINO.runtime.win 2026.2.1` |
| Ubuntu 原生库 | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0`、`OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1` |

`Microsoft.ML.Tokenizers 2.0.0` 来自 Visual 传递依赖。读者不需要 Python 或视频解码工具；原始视频不随工程提供。Linux 原生库显式复制到 `native/opencv`、`native/openvino`。发行版 RID 资产若提示 `NETSDK1206`，请核对复制路径，运行时用下文的 `LD_LIBRARY_PATH`。

仅使用 ORT 的项目，可以移除 OpenVINO 包、复制项及注册分支。本文不扩展为其他 Linux 发行版或其他后端已验证。

## 下载模型

六张小型截图已经在 `demo-inputs` 中。将以下模型、词表和合并表放入 `models`，也可复用已有本地文件；无需进行用户侧哈希校验。

Windows PowerShell：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
Invoke-WebRequest 'https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx' `
  -OutFile models/object_detection_nanodet_2022nov.onnx

$modelRelease = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
foreach ($name in @('image-encoder.onnx', 'text-encoder.onnx', 'vocab.json', 'merges.txt', 'LICENSE')) {
  Invoke-WebRequest "$modelRelease/clip-vit-b-32.$name" -OutFile "models/clip-vit-b-32.$name"
}
```

Ubuntu Bash：

```bash
mkdir -p models
curl -fL 'https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx' \
  -o models/object_detection_nanodet_2022nov.onnx

model_release='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
for name in image-encoder.onnx text-encoder.onnx vocab.json merges.txt LICENSE; do
  curl -fL "$model_release/clip-vit-b-32.$name" -o "models/clip-vit-b-32.$name"
done
```

NanoDet 约 3.8 MB；CLIP FP32 图像和文本图约 352 MB、254 MB。[OpenCV Zoo 固定模型目录](https://github.com/opencv/opencv_zoo/tree/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet)采用 Apache 2.0，CLIP 资产使用附带的 MIT 许可。模型与第三方素材分别保留其适用许可。

NanoDet 配置为 RGB、416 Letterbox、黑边、尺寸 Floor、原像素域 mean/std。已发布适配器使用 Linear，Zoo `demo.py` 使用 Area，不宣称像素完全一致。CLIP 使用 RGB、短边缩放中心裁剪、独立 mean/std 与 OpenCV Cubic，不宣称与 Pillow 像素完全一致。

换模型必须同时核对端口、shape、几何、颜色、归一化和解码规则。本例 NanoDet 为三尺度六输出，检测 Batch=1；CLIP 图像为真正的动态 Batch，范围 1—16。

## 运行

Windows，在此目录执行：

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --backend ort-cpu --manifest inspection.json
dotnet run -c Release --no-build -- --backend openvino --manifest inspection.json
```

Ubuntu 22.04：

```bash
dotnet restore
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/CustomVisual.dll --backend ort-cpu --manifest inspection.json
dotnet bin/Release/net10.0/CustomVisual.dll --backend openvino --manifest inspection.json
```

完成后用浏览器打开 `results/<backend>/index.html`，可按相机筛选或只查看待复核截图。`--inspect` 只检查三个图的真实端口。原始 NanoDet 的 initializer 警告不等于运行失败。

可选参数及默认值：

```text
--backend ort-cpu
--models models
--manifest inspection.json
--prompts prompts.json
--out results
--threshold 0.35
--nms 0.6
--batch 4
--max-rois 16
--padding 0.06
--review-margin 0.1
```

## 换自己的相机、图片与规则

`inspection.json` 的 `Cameras` 定义相机与多边形规则；`Snapshots` 定义截图，包含唯一 `Id`、`CameraId`、`Image` 与 `SourceSeconds`。图像相对路径从配置文件目录解析，时间表示来源片段的位置。

每条区域规则包含：

| 字段 | 用法 |
| --- | --- |
| `Id` / `Name` | 稳定标识与展示名称，ID 使用小写字母、数字和短横线 |
| `Polygon` | `[0,1]` 的归一化多边形，视角改变后需重新配置 |
| `ClassIds` | COCO 类别过滤：0 人员、2 汽车、5 公交、7 卡车 |
| `TriggerAtLeast` | 当前截图区域内达到该数量时生成待复核事件 |
| `Reason` | 业务原因代码，保存在事件报告中 |

程序按源图检测框底边中心判断区域归属，再按类别计数。同一多边形可定义多条独立规则；配置会拒绝未知字段、重复 ID、非法坐标及不存在的输入图。

`prompts.json` 当前为 `vehicle-body` 组，六条英文车型候选。类别不能跨组重复，候选 Key 全局唯一，每组至少两个候选。描述更改后重新编码；tokenizer 限定可打印 ASCII 英文，拒绝特殊 token 与超长文本。

## 输出与含义

| 文件 | 内容 |
| --- | --- |
| `index.html` | 静态复核台，筛选相机和事件，打开证据或完整 JSON |
| `summary.json` | 汇总及逐截图完整报告 |
| `observations.csv` | 每截图每区域的计数、成员与是否触发 |
| `review.csv` | 待复核事件、原因、成员 ID 与证据相对路径 |
| `<snapshot>/scene.json` | 完整检测、规则、CLIP 候选、配置和环境 |
| `<snapshot>/scene.jpg` | 区域、所有检测框与底边中心点 |
| `<snapshot>/roi-XX.jpg` | 进入 CLIP 的原图实际裁剪区域 |

青绿框命中业务区域，灰框为其他检测。CLIP 保存 Top-3、余弦、组内 Softmax 分数、Top-1/2 分差；`?` 提示候选歧义，分数不是业务概率。区域事件均为 `pending-human-review`，不依赖 CLIP。静态复核台不写回审核结果，也不发送通知。

区域外、没有候选组或超出 ROI 数量上限的目标仍保留，并分别标记 `outside-zones`、`no-prompt-group`、`max-rois-limit`。

## 已实跑范围与限制

六张默认图、六条车型候选、默认参数的完整执行：

| 环境 | ORT CPU | OpenVINO CPU |
| --- | --- | --- |
| Windows 11 x64 / Ryzen 7 5800H | 6 图、40 次目标观测、3 个 CLIP ROI、4 条待复核事件 | 相同计数与事件成员 |
| Ubuntu 22.04.5 x64 / i7-1165G7 | 6 图、40 次目标观测、3 个 CLIP ROI、4 条待复核事件 | 相同计数与事件成员 |

四组车型 Top-1 一致。CLIP 批次为首张道路图 `[2]`、末张道路图 `[1]`；其余图不需要语义模型。设备、版本和真实误报说明见文章第八节。这是工程运行验证，没有标注准确率评测或速度排名。

存在远处车辆漏检、路锥/标牌附近误报，部分影响区域计数；未触发不代表无人或安全。40 是逐图观测总数，不是独立人车数。没有跨帧跟踪、事件去重、停留或越线判定。

检测与图像会话在循环外复用，文本先编码一次并释放文本 Session。每图等待 ROI 推理完成再释放原图；Runner 释放每批准备输入，报告拥有自己的数据。绘图工具额外读取原图。示例顺序执行，不自动创建多 Session 或跨帧流水线。`Ctrl+C` 返回 2，失败返回 1，成功返回 0。
