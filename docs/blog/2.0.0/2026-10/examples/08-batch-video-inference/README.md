# 批量与视频推理

配套文章：[从单帧到并发流水线](../../2026-10-30-08-batch-video-inference.md)。正式 NuGet `2.0.0`，.NET 10 / x64，Windows 与 Ubuntu 22.04。

## 准备

1. 安装 .NET 10 SDK、FFmpeg，确保 `ffmpeg`、`ffprobe` 可调用。Windows 可使用 `winget install --id Gyan.FFmpeg --exact`；Ubuntu 使用 `sudo apt install ffmpeg`。
2. 下载视频到 `videos/vtest.avi`：`https://raw.githubusercontent.com/opencv/opencv/4.x/samples/data/vtest.avi`。
3. 下载固定 Batch=1 模型到 `models/yolo26n.onnx`：`https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/yolo-v26-detect-n.model.onnx`。程序会跳过真 Batch，仍可对照另外四种模式。
4. 真 Batch 使用文章中的 Ultralytics `8.4.193` 导出命令，得到 `models/yolo26n-dynamic.onnx`。显式 `nms=False`，使用 end-to-end 输出。

模型、视频和运行结果不随源码提交。代码遵循仓库 Apache 2.0 协议；模型和视频的许可、权利归属以各自上游为准。

## 运行

```powershell
dotnet run -c Release -- --model models/yolo26n-dynamic.onnx --backend ort-cpu --output results/ort-cpu
dotnet run -c Release -- --model models/yolo26n-dynamic.onnx --backend openvino --output results/openvino
```

Ubuntu 先构建并设置原生搜索路径：

```bash
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/BatchVideo.dll --model models/yolo26n-dynamic.onnx --backend ort-cpu
```

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| `--model` | `models/yolo26n.onnx` | YOLO26n end-to-end FP32 ONNX，`images → output0` |
| `--video` | `videos/vtest.avi` | 文件视频；不使用实时播放节奏 |
| `--backend` | `ort-cpu` | `ort-cpu` / `openvino` |
| `--threads` | `2` | 每个独立 Session 的 CPU 推理线程数 |
| `--frames` | `48` | 每轮处理的视频前 N 帧 |
| `--window` | `12` | 同时保留的已解码帧数上限，最大 64 |
| `--batch` | `4` | 真 Batch 的最大帧数，要求动态模型，最大 16 |
| `--repeats` | `3` | 每种模式的完整测量轮数 |
| `--output` | `results/<backend>` | JSON 和四张结果预览的目录 |
| `--ffmpeg` / `--ffprobe` | 同名命令 | 自定义可执行文件路径 |
| `--inspect` | 关闭 | 只检查模型端口，不读取视频 |
| `--cancel-after` | 不设置 | 调试取消：启动后 N 毫秒取消整个工作 |

使用 `Ctrl+C` 取消。退出前等待活动推理退出、归还输入缓冲并关闭 FFmpeg。`--frames` 大于视频帧数时明确报错，不静默减少样本。

## 输出和计时

五种模式为 `serial`、`pool-2`、`prefetch-1`、`prefetch-2`、`batch-4`（Batch 大小随参数变化）。每种模式预热两个完整窗口，再测量指定轮数；输出 `benchmark.json`、各模式最后一轮的 `frames.json` 和四张带框原视频帧。

计时包含原始 BGR 管道读取等待、前处理、调度、推理、解码和结果投影，排除模型加载、ffprobe/进程创建、预热、绘图和文件保存。报告中的窗口 P50/P95 是窗口完成时间，不是单帧延迟；三轮 48 帧仅用于教程演示。

Session/预取结果与同后端串行结果逐值对照；Batch 用同帧同类别最高 IoU 匹配，要求无未配对项、最低 IoU≥0.99、最大分数差≤0.001。不以这种一致性检查代替标注集准确率评测。

内存有界于当前帧窗口、在途输入与当前程序积累的结果元数据。极长视频应将每窗口的结果写入数据库/日志或交给消费者，避免一直保留 `List<FrameResult>`；本例限制像素和模型张量，不宣称结果集合也与视频长度无关。
