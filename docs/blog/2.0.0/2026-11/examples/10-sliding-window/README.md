# 大图滑动窗口检测

基于正式 NuGet 2.0.0、YOLO26n、.NET 10/x64。两张真实图片分别运行整图、重叠切片、全图补检与跨窗口 NMS，保存源图坐标、来源窗口、原始张量、检测图与 JSON。

图文教程：[让大图检测看得更细：DeploySharp 滑动窗口实战](../../2026-11-09-10-sliding-window.md)。图片来源与尺寸见 [demo-inputs](demo-inputs/README.md)。

## 下载与运行

在本工程目录执行。Windows：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
curl.exe -L --fail https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/yolo-v26-detect-n.model.onnx -o models/yolo26n.onnx
dotnet build -c Release
dotnet bin/Release/net10.0/SlidingDemo.dll --backend ort-cpu --compare
```

Ubuntu 22.04 x64：

```bash
mkdir -p models
curl -L --fail https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/yolo-v26-detect-n.model.onnx -o models/yolo26n.onnx
# OpenCV 图像编解码的系统依赖；已有环境不必重复安装。
sudo apt-get install libopenexr25 libilmbase25 libglib2.0-0 libgl1
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/SlidingDemo.dll --backend ort-cpu --compare
```

模型合同是 `images [1,3,640,640] → output0 [1,300,6]`、Float32、COCO80、YOLO26 端到端输出。预处理为 RGB、Letterbox、除以 255、补边 114；score 阈值 0.25。替换模型需同步 Profile、输入输出合同与 Decoder，不要求读者计算文件校验值。

## 后端与原生依赖

`--backend` 可选 `ort-cpu`、`ort-cuda`、`openvino`、`opencv`、`tensorrt`。完整项目配置了两种系统的包；OpenCV 同时用于图像处理，OpenCV DNN 则是独立的推理路径。

CPU 构建默认使用 ORT 1.28.0；OpenVINO Windows 为 2026.2.1、Ubuntu 为 2026.3.1，OpenCV 为 5.0.0。

CUDA 12 环境使用官方 **同版本** ORT 1.28.0 CUDA 12 发行包，完整解压后构建：

```powershell
dotnet build -c Release -o bin/cuda12 -p:OrtCuda12NativeDir=runtime/onnxruntime-win-x64-gpu_cuda12-1.28.0/lib
dotnet bin/cuda12/SlidingDemo.dll --backend ort-cuda --compare
```

```bash
dotnet build -c Release -o bin/cuda12 -p:OrtCuda12NativeDir="$PWD/runtime/onnxruntime-linux-x64-gpu_cuda12-1.28.0/lib"
export LD_LIBRARY_PATH="$PWD/bin/cuda12:$PWD/bin/cuda12/native/opencv:$PWD/bin/cuda12/native/openvino:$CUDA_HOME/lib64:$CUDNN_HOME/lib:${LD_LIBRARY_PATH:-}"
dotnet bin/cuda12/SlidingDemo.dll --backend ort-cuda --compare
```

Windows 还需在当前进程 `PATH` 中加入实际 CUDA、cuDNN、TensorRT 的 DLL 目录。`OrtCuda12NativeDir` 会使用 Managed 包并复制完整 GPU native 集合，避免误用 CPU native 或 CUDA 13 发行包；官方发行包地址见正文。CUDA 13 环境可选 `-p:UseCuda=true`。

TensorRT 使用匹配的本体和 Bridge，Windows 对应 TRT 10.11，Ubuntu 对应 TRT 11；缺少 Engine 时使用正式库的 Builder 在**目标机**构建。默认 Engine 文件名包含系统和 TRT 主版本，换设备或模型后应重新指定 `--engine`，不要复用其他设备生成的文件。

本例已在 Windows / Ubuntu 五条路径运行并逐框核对。Ubuntu 复测使用 RTX 2060、TRT 11.0.0.114、CUDA Runtime 12.9.79、cuBLAS 12.6.4.1、cuDNN 9.10.2.21。若旧 CUDA 12.6 环境出现 `cudaLibraryGetKernel` 未定义，应安装匹配的新 CUDA Runtime，并将其库目录放到本进程搜索路径前面；不应降级到 CPU 掩盖加载错误。

```powershell
dotnet bin/Release/net10.0/SlidingDemo.dll --backend tensorrt --trt-api 10 --compare
```

## 参数与输出

| 参数 | 默认 | 作用 |
| --- | --- | --- |
| `--model` | `models/yolo26n.onnx` | ONNX 模型；TRT 首次构建也读取它 |
| `--images` | `demo-inputs` | JPG 目录 |
| `--output` | `results/后端名` | 输出目录 |
| `--window` | `640` | 源图中的正方形窗口边长；模型输入仍是 640×640 |
| `--overlap` | `0.2` | 目标重叠比例，末尾回贴可能产生更大的实际重叠 |
| `--sessions` | `1` | 独立 Session 数，1—8；不改变模型 Batch=1 |
| `--threads` | `2` | ORT/OpenVINO/OpenCV 的 CPU 推理线程配置 |
| `--no-full-pass` | 关闭 | 省略额外一次全图补检 |
| `--compare` | 关闭 | 再跑“无补检”和“overlap=0”两条路线 |
| `--roi` | 不启用 | 额外运行指定矩形内的滑窗，格式 `x,y,width,height` |
| `--engine` / `--trt-api` | 按系统选择 | Engine 路径与 TRT 主版本 10/11 |

例如只在矩形区域内补跑一组滑窗：

```powershell
dotnet bin/Release/net10.0/SlidingDemo.dll --backend ort-cpu --sessions 2 --roi 250,100,900,600 --output results/roi
```

每张图保存 `01-full.jpg`、`sliding-windows.jpg`、`sliding-raw.jpg`、`sliding-merged.jpg`、`detections.json`。`--compare` 增加两组图片；`--roi` 增加 `roi-merged.jpg`。根 `results.json` 汇总模型、设备环境、设置与全部检测结果。绘图观察用的原始张量只在功能模式记录，性能模式关闭。

运行器设置最大 128 个窗口（含补检）、最终最多 1000 个结果、源窗口累计像素预算 6400 万。像素预算是所有窗口面积之和，不是显存字节上限；最后结果上限也不代表原始候选缓冲区上限。代码显式采用 `Source` 坐标模式，因为 `PrepareRectangle` 的 Transform 已还原裁剪与 Letterbox。

## 性能复测

```powershell
dotnet bin/Release/net10.0/SlidingDemo.dll --backend ort-cpu --benchmark --warmup 5 --iterations 50 --revision your-commit --output results/benchmark-ort
```

同一个后端的整图与滑窗路线交错运行，每图每路线至少 5 次预热、50 次测量。计时包括一次 JPG 解码、切片准备、实际完成的推理、Decoder、源图结果合并、全局 NMS 和释放；不含磁盘 I/O、绘图、模型加载、Engine 构建和观察用张量记录。

`benchmark.json` 包含原始样本、分组阶段时间、mean/P50/P95/min/max、托管分配、进程内存及测前测后的 GPU 状态。分配不含 native/GPU；进程峰值为整个进程的累计峰值，GPU 测前/后采样也不是显存峰值。此处没有 GPU 专用前处理或设备端 NMS，通用 OpenCV 路线在 CPU 上准备输入并合并结果。
