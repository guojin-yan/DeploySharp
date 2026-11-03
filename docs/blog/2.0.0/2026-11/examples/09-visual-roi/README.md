# ROI 工程实战

使用正式 NuGet 2.0.0、YOLO26n 和四张真实道路/庭院图片，运行全图过滤、多个 ROI 裁剪、原图坐标合并和业务过滤。输出检测图片、逐框 JSON、配置快照与环境信息。

图文教程：[让检测只盯关键区域：DeploySharp ROI 工程实战](../../2026-11-03-09-visual-roi.md)。

## 环境与模型

.NET 10，x64。完整工程按 Windows / Ubuntu 22.04 选择 OpenCV、OpenVINO 和 TensorRT Bridge；只用 ORT CPU 时可以删除其他后端的包与注册分支。

在本目录下载模型，PowerShell 使用 `curl.exe`，Bash 使用 `curl`：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
curl.exe -L --fail https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/yolo-v26-detect-n.model.onnx -o models/yolo26n.onnx
dotnet build -c Release
dotnet bin/Release/net10.0/RoiDemo.dll --backend ort-cpu
```

```bash
mkdir -p models
curl -L --fail https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/yolo-v26-detect-n.model.onnx -o models/yolo26n.onnx
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/RoiDemo.dll --backend ort-cpu
```

输入要求：`images [1,3,640,640]`、`output0 [1,300,6]`、FP32、YOLO26 端到端框输出、COCO80 标签。替换其他输出格式时需要同步修改 Profile、Decoder 与类别规则。

## 后端

`--backend` 支持 `ort-cpu`、`ort-cuda`、`openvino`、`opencv`、`tensorrt`。CPU 路径构建后直接切换参数；GPU 原生依赖与完整操作见图文教程第八节。

ORT 1.28.0 的 CUDA 12 环境使用同版本官方 CUDA 12 发行包，传入 `-p:OrtCuda12NativeDir=发行包的lib目录`，输出到独立的 `bin/cuda12`。完整工程不会同时引入 ORT CPU native；不要混用不同版本 provider。CUDA 13 环境可使用 `-p:UseCuda=true`。

TensorRT 需要目标机安装对应本体、CUDA 与 cuDNN。缺少 Engine 时程序在目标机转换。Windows 本例对应 `--trt-api 10`，Ubuntu 对应 `--trt-api 11`；换主版本也要替换 `.csproj` 中的 Bridge 包。

## 配置与结果

| 参数 | 默认值 | 用途 |
| --- | --- | --- |
| `--model` | `models/yolo26n.onnx` | ONNX 路径；TensorRT 首次构建也使用它 |
| `--images` | `demo-inputs` | JPG 目录，文件名前缀对应配置名 |
| `--configs` | `configs` | `road.json`、`courtyard.json` 等配置 |
| `--output` | `results/后端名` | 图片、JSON 与实际快照 |
| `--threads` | `2` | ORT/OpenVINO/OpenCV CPU 推理线程配置；TensorRT 不使用该参数 |
| `--sessions` | `1` | 独立 Session 数量，范围 1—8 |
| `--engine` | 按模型、系统及 TRT 主版本生成 | TensorRT Engine 路径 |
| `--watch` | 关闭 | 每轮按 Enter 重读配置，`q` 或 EOF 退出 |
| `--rebind-normalized` | 关闭 | 仅对全部 Normalized ROI，允许按当前图像尺寸重建快照 |

`road-00.jpg` 使用 `configs/road.json`。默认拒绝源尺寸不匹配，防止默默套用错误区域。两种执行模式拆为单独快照，避免裁剪窗口参与业务过滤。配置更新先完整校验；已有可用快照时，无效配置保留旧版本。

每张图输出 `01-regions.jpg`、`02-full.jpg`、`03-filtered.jpg`、`04-crop-raw.jpg`、`04-crop-merged.jpg`、`05-crop-filtered.jpg`、`detections.json` 和 `roi.snapshot.json`。根 `results.json` 汇总环境、参数与全部结果。输出坐标均为源图坐标，不要再次添加裁剪偏移。

默认案例已在 Windows / Ubuntu 的 ORT CPU、ORT CUDA、OpenVINO CPU、OpenCV DNN CPU、TensorRT 路径运行；固定 Batch=1，通用 OpenCV 前处理在 CPU 上执行。默认流程只做功能展示，性能测量使用下面的独立开关；图片没有准确率标注。

## ROI 性能与几何展示

使用 `--benchmark` 运行性能对照，`--geometry-demo` 运行几何展示；这两个开关各自执行完成后退出，不进入默认的四图绘制流程。

```powershell
dotnet bin/Release/net10.0/RoiDemo.dll --backend ort-cpu --benchmark --warmup 5 --iterations 50 --output results/benchmark-ort
dotnet bin/Release/net10.0/RoiDemo.dll --backend ort-cpu --geometry-demo --output results/geometry
```

GPU 构建目录与加载环境沿用教程中的 `bin/cuda12` 和原生配置。正式计时最低为 5 次预热、50 次测量；`--images` 可选择待测 JPG 目录，`--revision` 可记录复测时的源码版本。原始样本写入 `benchmark.json`，含各阶段、总时间、均值、P50/P95、极值、托管分配、进程内存和运行前后的 GPU 采样。模型、输入和配置的身份由程序自动记录。

基准复用 JPG bytes 和已解析快照，每帧重新解码、准备输入、推理、解码结果并释放；不计磁盘 I/O、配置解析、绘图、模型加载和 Engine 构建。无 ROI 与“全图后过滤”共享同一次推理前缀；单整图 ROI 与两个局部 ROI 是另外两条完整路线，按轮次交错测量。进程峰值内存是本次进程的累计峰值；托管分配不包含 native/GPU 内存，GPU 采样也不是每条路线的显存峰值。

`RoiGeometryShowcase` 使用 `courtyard-04.jpg`（不存在则使用首图），输出矩形、多边形、旋转矩形、带孔洞 Mask 和显式透视四边形的真实输入 PNG、原图结果以及 `geometry.json`。另验证五种命中规则、person 筛选、置信度门槛、启停、矩形 margin 和像素预算拒绝。Mask 为示例配置生成，不是分割模型预测；`margin` 在正式版只支持矩形和旋转矩形。
