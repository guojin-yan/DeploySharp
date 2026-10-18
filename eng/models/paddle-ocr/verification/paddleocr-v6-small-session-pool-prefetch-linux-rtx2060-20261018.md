# PP-OCRv6 Small Linux RTX 2060 Session Pool / Prefetch Contract

## 结果

远端 Ubuntu 22.04.5 / RTX 2060 6 GB 主机上，使用 DeploySharp 的真实 `DET → CLS → REC → merge` 流水线完成 `PpOcrV6SmallUsesIndependentSessionsAndPrefetchedConcurrentFrames`：测试通过，3 张输入均按输入顺序返回，均产生文本区域和识别结果，结果合同互不塌缩，输入准备峰值并发为 3，Pipeline 成功路径释放了回调创建的输入。

本次运行使用 Ubuntu 22.04 的 `JYPPX.OpenCV.Native` 原生运行时和 OpenCV 依赖；此前失败是因为远端没有该 DeploySharp 原生库，不是 ORT 或模型加载失败。

机器可读证据：[JSON 报告](paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-20261018.json)

## 精确环境

| 项目 | 值 |
| --- | --- |
| 主机 | `ygj-NUC11PHi7` |
| 操作系统 | Ubuntu 22.04.5 LTS |
| GPU | NVIDIA RTX 2060 6 GB |
| NVIDIA driver | `580.178.04` |
| 后端 | ONNX Runtime CPU |
| ORT 包 | `Microsoft.ML.OnnxRuntime 1.28.0` |
| .NET | 10.0.9 |
| 模型 | PP-OCRv6 Small DET/REC + PP-OCRv5 Mobile CLS |
| 输入 | 3 张不同的 HierText 公开 JPEG，SHA-256 见 JSON |
| 流水线并发 | 2 |
| 每阶段独立 Session | DET/CLS/REC 各 2 路 |
| 预取 | `RunPrefetchedAsync(prefetch: 2)` |
| 观测墙钟 | `1861.224 ms`（3 张输入，非稳态基准） |

## 复现

先提供匹配的 Ubuntu OpenCV runtime，并设置模型、三张图片的路径；然后启用 opt-in 测试和报告输出：

```bash
export LD_LIBRARY_PATH="$HOME/deploysharp-validation/opencv-native:$LD_LIBRARY_PATH"
export DEPLOYSHARP_PADDLEOCR_ROOT="$HOME/deploysharp-validation/paddleocr"
export DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_1="$HOME/deploysharp-validation/images/demo_1.jpg"
export DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_2="$HOME/deploysharp-validation/images/demo_2.jpg"
export DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_3="$HOME/deploysharp-validation/images/demo_3.jpg"
export DEPLOYSHARP_PADDLEOCR_SESSION_POOL_REPORT="$HOME/deploysharp-validation/session-pool-report.json"
export DEPLOYSHARP_PADDLEOCR_SESSION_POOL_RUN_EXTERNAL=1
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj \
  -c Release -f net10.0 --no-restore --no-build \
  --filter FullyQualifiedName~PaddleOcrSessionPoolExternalIntegrationTests
```

## 边界

这是一个单机、三图的执行顺序、并发重叠和资源所有权合同。它不是 `5 warm-up + 50 measured` 性能测试，不是吞吐排名、准确率、GPU 推理证据、长时间 soak，也不能外推到 PP-OCR 其它模型或其它后端。RTX 2060 在本次运行中只作为主机设备信息记录；推理后端是 ORT CPU，并未使用 TensorRT/CUDA。

该报告的源码基于提交 `7b03d77`，并包含测试入口的本地修改；JSON 记录了 dirty 状态和测试源文件 SHA-256 `c2da693cc401f0ab2eb948925c482cc827d065c1936210d75d025ca113f58af6`。若要把它升级为正式性能证据，需要固定提交、统一设备遥测和 5+50 协议后重新跑测。
