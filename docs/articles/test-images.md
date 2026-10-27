# 默认测试图片

DeploySharp 为示例和基准工具提供一组固定的默认输入。图片不放入 NuGet 包，也不混入模型 Release，而是放在独立的 [`test-assets.1`](https://github.com/guojin-yan/DeploySharp/releases/tag/test-assets.1) 资产集合中。文件名稳定，后续图片会继续追加到同一个 Release，并在清单和 Release notes 中记录。

## 当前映射

| 文件 | 默认任务 | 下载 |
| --- | --- | --- |
| `bus.jpg` | 目标检测、分割、异常检测、背景移除、视觉语言、提示分割 | [下载](https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/bus.jpg) |
| `demo_7.jpg` | 图像分类 | [下载](https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/demo_7.jpg) |
| `demo_9.jpg` | 关键点/姿态检测 | [下载](https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/demo_9.jpg) |
| `plane.png` | 旋转框检测 | [下载](https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/plane.png) |
| `ocr-demo_1.jpg` | PaddleOCR 完整流水线 | [下载](https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/ocr-demo_1.jpg) |

`eng/test-assets/test-image-catalog.json` 保存任务映射、文件大小和 SHA-256。五个默认输入分别用于目标检测/分割、分类、关键点、旋转框和 OCR；客户端下载到应用缓存后使用，不依赖维护者的本机目录。

## 自动使用

`DeploySharp.VisualBenchmark` 未指定 `--image` 时，会按模型任务从 Release 下载图片到 `%LOCALAPPDATA%\DeploySharp\TestImages`，并先校验 SHA-256。可用 `DEPLOYSHARP_TEST_IMAGE_ROOT` 指向已经下载的目录。显式传入 `--image` 时，工具只使用指定文件。

PaddleOCR 基准在未设置 `DEPLOYSHARP_PADDLEOCR_IMAGE` 时自动使用 `ocr-demo_1.jpg`；Release 推理案例在未指定 `--image` 时自动使用 `bus.jpg`。首次运行需要网络；`--offline` 只能复用已经校验过的缓存。

```powershell
dotnet run --project tools/DeploySharp.VisualBenchmark/DeploySharp.VisualBenchmark.csproj -c Release -- --kind all --backend onnxruntime
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-1.4
```

## 维护与追加

使用 `eng/test-assets/Stage-TestImageAssets.ps1` 可按清单从本地源文件生成带校验的发布目录；使用 `eng/test-assets/Publish-TestImageRelease.ps1 -Publish` 会校验已有资产并将新增资产追加到 `test-assets.1`。同名图片内容变化会被拒绝，避免静默改变基准输入；`README.md`、`test-image-catalog.json` 和 `SHA256SUMS` 会在追加图片时按校验结果受控更新。

## 使用协议

默认图片只用于示例和性能复现，不代表每个模型的精度测试集。运行基准时应把以下信息和结果一起保存：图片文件名、SHA-256、模型工件 SHA-256、后端版本、设备、warmup/iterations、batch、Session 数量和是否包含解码/后处理。更换图片后不能直接与历史 P50/P95 比较。

工具找不到远程图片时，可以设置 `DEPLOYSHARP_TEST_IMAGE_ROOT` 使用本地目录；目录中的文件名必须与清单一致，工具仍会校验 SHA-256。不要用同名不同内容的文件绕过校验，因为这会让后端对比失去意义。

## 按任务选择输入

`bus.jpg` 适合快速观察目标检测、分割和视觉语言输出，但不能覆盖小目标和遮挡场景；`plane.png` 只用于 OBB 的几何回归；`ocr-demo_1.jpg` 用于完整 OCR 流程，不能把它当成只有 det 或 rec 的基准。新增模型时应在清单中添加专用输入映射，并在模型文章中说明该输入覆盖了哪些边界。
