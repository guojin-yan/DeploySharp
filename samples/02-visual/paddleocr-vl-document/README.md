# PaddleOCR-VL 页面区域与多页示例

本示例把实际图片解码、版面区域裁剪、VL 生成和页面 JSON/Markdown 导出连成一个完整流程。支持 .NET 8/9/10 的实验性 PaddleOCR-VL-1.6 Bundle；当前使用 ONNX Runtime CPU，OpenCV 只负责图片解码和裁剪。

## 准备模型和页面

模型使用固定 revision `c5630abae1d940eafe0697512a0325494b02ab42`。`tokenizer-root` 包含官方 tokenizer/template 文件，`bundle-root` 包含同一批次的 `vision.onnx`、`embedding.onnx`、`decoder.onnx`、`head.onnx`。Bundle 的来源、验证工厂和下载状态见 [VL 使用说明](../../../docs/articles/visual-ocr.md#paddleocr-vl-是否已经可用于模型推理)；目前尚未提供可下载的 VL Release 二进制。

新建 `pages.json`，为每页指定图片、解码后尺寸、唯一页码和已知区域。例如两页各有一块表格：

```json
{
  "task": "TableRecognition",
  "maximumNewTokens": 512,
  "pages": [
    {
      "image": "page-a.png",
      "width": 1000,
      "height": 1400,
      "pageIndex": 0,
      "regions": [
        { "category": "table", "x": 80, "y": 200, "width": 840, "height": 500 }
      ]
    },
    {
      "image": "page-b.png",
      "width": 1000,
      "height": 1400,
      "pageIndex": 1,
      "regions": [
        { "category": "table", "x": 60, "y": 100, "width": 880, "height": 600 }
      ]
    }
  ]
}
```

图片相对路径基于 `pages.json` 所在目录。尺寸使用解码、EXIF 方向归一后的页面像素；与实际图片不符会报错。矩形采用页面坐标，左上向下取整、右下向上取整，并裁至页面边界。完全不相交、非有限或空区域会失败。

这里的区域由调用方提供，不运行版面检测模型。接入真实 PP-Structure 时，用版面检测阶段输出的 `PaddleDocumentRegionResult` 替换示例中的 `manual-layout` 阶段。一个阶段对所有输入区域执行同一任务；`category` 是来源元数据，不会自动选择任务，混合类型页面应由调用方先分组。

## 运行与结果

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-document/PaddleOcrVlDocument.csproj -c Release -- `
  E:\Model\PaddleOCR-VL\1.6\c5630abae1d940eafe0697512a0325494b02ab42 `
  E:\Model\PaddleOCR-VL\exports\dynamic-fp32-20261023 `
  E:\Data\document\pages.json E:\Data\document\vl-output 2
```

最后的 `2` 是页面并发上限，省略时顺序处理。示例自动引用 Windows x64 或 Ubuntu 22.04 x64 的 OpenCV runtime；原生系统依赖仍需可用。`Ctrl+C` 取消流水线。每页输出 `page-<index>.json` 和 `.md`，包括原始生成文本、Token、结束原因、页面与区域输入身份、源区域坐标、局部失败和阶段耗时。存在失败或未到 EOS 的区域时返回非零退出码。

Linux 默认使用 `ubuntu.22.04-x64` RID；交叉发布可加 `-r ubuntu.22.04-x64 --self-contained false`。程序目标为 .NET 8，需要对应运行时；仅安装 .NET 9/10 时可以显式设置 `DOTNET_ROLL_FORWARD=Major` 后运行已发布的 DLL。[实际 Linux 页面记录](../../../eng/models/paddle-ocr/verification/paddleocr-vl-regional-pages-linux-20261024.md)包含两页三个手工表格区域的真实 ORT 运行和示例输出核对。

每页只解码一次；区域用底图的只读视图逐行转换为紧密 RGB，不编码 crop、不克隆整页，不先用 OpenCV 缩放。随后由托管 VL Processor 完成官方重采样、归一化和 Patch 打包。页面结果保存编码文件 SHA；区域结果使用尺寸绑定的 RGB 内容 SHA，相同尺寸/像素的重复区域具有相同身份，位置通过各自的 `sourceRegion` 保存。

页面并发可以重叠准备和等待，但示例共享一个 VL Session，生成仍按 Session 的队列串行执行，不代表模型 Batch 或多个独立推理器。使用多个 Session 前，应按实际权重、KV 和工作区占用检查内存预算。

输出保留原始 VL 文本，`CoordinatesAvailable=false`：源区域坐标来自调用方，不是 VL 预测的文字框。自动阅读顺序、任务分类、HTML/单元格/Spotting 结构化解析和独立标注质量不属于该示例的已验证能力。
