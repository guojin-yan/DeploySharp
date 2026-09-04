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

`eng/test-assets/test-image-catalog.json` 保存任务映射、文件大小和 SHA-256。当前五个输入分别对应本机的 `E:\Data\image\bus.jpg`、`E:\Data\image\demo_7.jpg`、`E:\Data\image\demo_9.jpg`、`E:\Data\image\plane.png` 和 `E:\Data\ocr\demo_1.jpg`；这些本机路径不是使用者的前置条件。

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
