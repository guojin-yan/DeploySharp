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

上面的区域由调用方提供，不运行版面检测模型；固定任务模式对所有区域运行 `TableRecognition`。混合页面可使用下面的类别路由模式，需要模型自动产生区域时则启用 `layoutDetector`。

## 混合页面：按类别选择任务

将 `task` 改为 `Auto`，同一页中的 `text`、`table`、`formula`、`chart`、`seal` 会分别运行对应 VL 任务，无需先分组或重复解码。`Auto` 只表示根据已有类别选择任务，不执行版面检测或推断阅读顺序。

```json
{
  "task": "Auto",
  "maximumNewTokens": 512,
  "taskByCategory": { "spotting": "TextSpotting", "header": null },
  "pages": [
    {
      "image": "mixed-page.png", "width": 1000, "height": 1400, "pageIndex": 0,
      "regions": [
        { "category": "text", "x": 80, "y": 100, "width": 840, "height": 100 },
        { "category": "table", "x": 80, "y": 230, "width": 840, "height": 500 },
        { "category": "formula", "x": 80, "y": 760, "width": 840, "height": 100 },
        { "category": "figure", "x": 80, "y": 900, "width": 400, "height": 300 }
      ]
    }
  ]
}
```

默认映射覆盖现有 PP-Structure 的 3/5/17/23/Plus 标签：文字和标题类选择 `TextRecognition`，表格/公式/图表/印章选择各自任务；`image`、`figure`、`header_image`、`footer_image` 和未知类别明确跳过。类别匹配忽略大小写和首尾空白。`taskByCategory` 可覆盖默认映射，`null` 表示主动跳过；例如上面为应用自定义的 `spotting` 类别选择定位任务，并跳过页眉。省略该对象时保留默认映射。

如需对所有未匹配类别执行 OCR，可以增加 `"unmatchedCategoryTask": "TextRecognition"`。显式配置为 `null` 的类别（包括默认图片类）仍然跳过。固定任务模式下配置类别路由选项会报错，防止配置被静默忽略。

混合结果使用 `PaddleDocumentVisionLanguageRoutedPageResult` / `VisionLanguageParsing`，每个原始区域都有连续不变的 `regionIndex`，并记录 `Succeeded`、`Failed` 或 `Skipped`。跳过项保留类别/源框/原因，不生成文本，也不计入失败；失败项保留已选任务。每个成功项单独保存实际任务、模块、Token、结束原因和输入 SHA。示例不会将跳过项视为错误，但任何生成失败或非 EOS 结果都会返回退出码 `1`。

## 自动检测版面，再执行 VL

配置 `layoutDetector` 后，示例运行实际 PP-DocLayout-L ONNX 模型，再将全部检测区域按默认/自定义类别映射交给 VL。图片仍由 OpenCV 每页解码一次；检测前处理与 VL 裁剪复用同一底图。

```json
{
  "task": "Auto",
  "maximumNewTokens": 2048,
  "layoutDetector": {
    "modelId": "paddle-doc/pp-doclayout-l",
    "path": "pp-doclayout-l.onnx",
    "scoreThreshold": 0.3,
    "maximumRegions": 64,
    "maximumPagePixels": 134217728
  },
  "pages": [
    { "image": "page-a.png", "width": 771, "height": 1000, "pageIndex": 0 }
  ]
}
```

模型路径也相对 `pages.json` 所在目录；文件须为该模型已验证的 ONNX 导出，不能用任意同名文件替换。当前此示例的检测器配置只接受 `paddle-doc/pp-doclayout-l`；其它已经正确绑定的 LayoutDetection `VisualPipeline` 可直接传给库的 `OpenCvPaddleOcrVlDocumentPipeline`。检测器与 VL 目前都使用 ONNX Runtime CPU。

检测器模式要求 `task=Auto`，且页面不能同时配置手工 `regions`。`scoreThreshold` 过滤低分候选；`maximumRegions` 是过滤后所有检测区域的上限，包括将跳过的图片类。超限会在 VL 生成前明确失败，不静默截断为前 K 个。`maximumPagePixels` 是解码后页面像素上限，超限在检测前失败；它不替代编码文件大小、解码器内存或 VL Token/KV 的独立预算。

返回顺序是检测器顺序，**不是阅读顺序**。默认图片类/未知类别仍跳过；如果模型将正文误判为图片，该内容也不会被默认解析，可显式配置 `"taskByCategory": { "image": "TextRecognition" }`。这改变任务选择，不改善版面模型本身的准确率。检测到重叠类别时保留每个原始区域，不自动合并重复文本。

## 运行与结果

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-document/PaddleOcrVlDocument.csproj -c Release -- `
  E:\Model\PaddleOCR-VL\1.6\c5630abae1d940eafe0697512a0325494b02ab42 `
  E:\Model\PaddleOCR-VL\exports\dynamic-fp32-20261023 `
  E:\Data\document\pages.json E:\Data\document\vl-output 2
```

最后的 `2` 是页面并发上限，省略时顺序处理。示例自动引用 Windows x64 或 Ubuntu 22.04 x64 的 OpenCV runtime；原生系统依赖仍需可用。`Ctrl+C` 取消流水线。每页输出 `page-<index>.json` 和 `.md`，包括原始生成文本、Token、结束原因、页面与区域输入身份、源区域坐标、局部失败和阶段耗时。存在失败或未到 EOS 的区域时返回非零退出码。

Linux 默认使用 `ubuntu.22.04-x64` RID；交叉发布可加 `-r ubuntu.22.04-x64 --self-contained false`。程序目标为 .NET 8，需要对应运行时；仅安装 .NET 9/10 时可以显式设置 `DOTNET_ROLL_FORWARD=Major` 后运行已发布的 DLL。[实际 Linux 页面记录](../../../eng/models/paddle-ocr/verification/paddleocr-vl-regional-pages-linux-20261024.md)包含两页三个手工表格区域的真实 ORT 运行和示例输出核对。

`Auto` 模式也有[Linux 混合页面运行记录](../../../eng/models/paddle-ocr/verification/paddleocr-vl-mixed-pages-linux-20261024.md)：由固定六任务 RGB 组成两张 PNG，六个任务区域的全部 765 Token/文本匹配官方参考、均 EOS，另有两个明确跳过项。独立示例实际运行的 JSON/Markdown 与集成测试一致；这是手工版面的拼接页面集成验证，不是独立多页质量或自动版面检测结果。

每页只解码一次；区域用底图的只读视图逐行转换为紧密 RGB，不编码 crop、不克隆整页，不先用 OpenCV 缩放。随后由托管 VL Processor 完成官方重采样、归一化和 Patch 打包。页面结果保存编码文件 SHA；区域结果使用尺寸绑定的 RGB 内容 SHA，相同尺寸/像素的重复区域具有相同身份，位置通过各自的 `sourceRegion` 保存。

页面并发可以重叠准备和等待，但示例共享一个 VL Session，生成仍按 Session 的队列串行执行，不代表模型 Batch 或多个独立推理器。使用多个 Session 前，应按实际权重、KV 和工作区占用检查内存预算。

检测器模式输出的 JSON/Markdown 同时包含实际布局结果和 VL 任务结果。`PaddleDocumentPipelineResult.Elapsed` 统计检测前处理/推理/映射及区域准备/生成阶段，外层文件读取和首次解码不在其中；测量完整调用延迟时应在 `RunAsync` 外部计时，不能直接将该字段作为正式端到端性能。

输出保留原始 VL 文本，`CoordinatesAvailable=false`：源区域坐标来自调用方或检测器，不是 VL 预测的文字框。阅读顺序、HTML/单元格/Spotting 结构化解析和独立标注质量仍需分别验证。
