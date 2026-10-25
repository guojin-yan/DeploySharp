# PaddleOCR-VL 页面区域与多页示例

本示例把实际图片解码、版面区域裁剪、VL 生成和页面 JSON/Markdown 导出连成一个完整流程。支持 .NET 8/9/10 的实验性 PaddleOCR-VL-1.6 Bundle；当前使用 ONNX Runtime CPU，OpenCV 只负责图片解码和裁剪。

## 准备模型和页面

模型使用固定 revision `c5630abae1d940eafe0697512a0325494b02ab42`。`tokenizer-root` 包含官方 tokenizer/template 文件，`bundle-root` 包含同一批次的 `vision.onnx`、`embedding.onnx`、`decoder.onnx`、`head.onnx`。Bundle 的来源、验证工厂和下载状态见 [VL 使用说明](../../../docs/articles/visual-ocr.md#paddleocr-vl-是否已经可用于模型推理)；`models-paddleocr` Release 已提供九个独立资产，可用 `PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` 按需下载并校验。

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

将 `task` 改为 `Auto`，同一页中的 `text`、`table`、`formula`、`chart`、`seal` 会分别运行对应 VL 任务，无需先分组或重复解码。`Auto` 表示根据已有类别选择任务；检测器和阅读排序通过各自的配置显式启用。

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

## 可选的多栏阅读顺序

`task=Auto` 时，可在 `pages.json` 顶层加入以下配置，手工区域和自动检测两种模式都适用：

```json
"readingOrder": {
  "mode": "RecursiveXyCut",
  "rightToLeft": false,
  "minimumColumnGapRatio": 0.02,
  "minimumRowGapRatio": 0.005,
  "maximumDepth": 32
}
```

这是顶层字段片段，需要与 `task`、`pages` 等字段组成完整 JSON。省略 `readingOrder` 或使用 `mode=SourceOrder` 保持原顺序；单栏也可选 `TopToBottom`。XY-cut 按页面空白分栏/分区，优先读完一栏；`rightToLeft` 可反转栏顺序。阈值相对整页宽/高，重叠区域或深度耗尽时退回从上到下排序。

JSON 的 `payload.regions` 和区域文件名仍按原 `regionIndex`，新增 `payload.readingOrder` 指定阅读索引，Markdown 按此顺序展示。跳过/失败项也占据其阅读位置，不重编号；生成委托仍按原索引执行。固定单任务模式配置 `readingOrder` 会明确报错。该功能不推理额外模型，不校正旋转、不去重或补回漏检；几何排序的合同测试不能代替独立页面的阅读顺序准确率。库接口和自定义排列见[详细教程](../../../docs/articles/visual-paddle-structure.md#多栏阅读顺序与稳定区域索引)。

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

最后的 `2` 是页面并发上限，省略时顺序处理。示例自动引用 Windows x64 或 Ubuntu 22.04 x64 的 OpenCV runtime；原生系统依赖仍需可用。`Ctrl+C` 取消流水线。每页输出 `page-<index>.json` 和 `.md`，包括原始生成文本、Token、结束原因、页面与区域输入身份、源区域坐标、局部失败和阶段耗时。存在失败、未到 EOS、表格结构或 Spotting 解析失败的区域时返回非零退出码。

表格任务自动附带严格 OTSL 分析。解析成功的区域另生成 `page-<index>-region-<index>.table.html`，包含横向/纵向合并；JSON 的 `table` 对象保留逻辑单元格、跨度、状态和诊断，Markdown 显示转义后的表格并保留 OTSL 原文。生成成功数仍统计生成结果，表格是否可用应检查 `table.succeeded`；示例控制台单独打印该状态。示例不自动启用行宽修复，库的显式修复用法见[VL 表格教程](../../../docs/articles/visual-paddle-structure.md#vl-表格otsl合并单元格与-html)。输出目录应为本次运行专用目录，避免将以前运行留下的 HTML 误当成本次结果。

Linux 默认使用 `ubuntu.22.04-x64` RID；交叉发布可加 `-r ubuntu.22.04-x64 --self-contained false`。程序目标为 .NET 8，需要对应运行时；仅安装 .NET 9/10 时可以显式设置 `DOTNET_ROLL_FORWARD=Major` 后运行已发布的 DLL。[实际 Linux 页面记录](../../../eng/models/paddle-ocr/verification/paddleocr-vl-regional-pages-linux-20261024.md)包含两页三个手工表格区域的真实 ORT 运行和示例输出核对。

`Auto` 模式也有[Linux 混合页面运行记录](../../../eng/models/paddle-ocr/verification/paddleocr-vl-mixed-pages-linux-20261024.md)：由固定六任务 RGB 组成两张 PNG，六个任务区域的全部 765 Token/文本匹配官方参考、均 EOS，另有两个明确跳过项。独立示例实际运行的 JSON/Markdown 与集成测试一致；这是手工版面的拼接页面集成验证，不是独立多页质量或自动版面检测结果。

每页只解码一次；区域用底图的只读视图逐行转换为紧密 RGB，不编码 crop、不克隆整页，不先用 OpenCV 缩放。随后由托管 VL Processor 完成官方重采样、归一化和 Patch 打包。页面结果保存编码文件 SHA；区域结果使用尺寸绑定的 RGB 内容 SHA，相同尺寸/像素的重复区域具有相同身份，位置通过各自的 `sourceRegion` 保存。

页面并发可以重叠准备和等待，但示例共享一个 VL Session，生成仍按 Session 的队列串行执行，不代表模型 Batch 或多个独立推理器。使用多个 Session 前，应按实际权重、KV 和工作区占用检查内存预算。

检测器模式输出的 JSON/Markdown 同时包含实际布局结果和 VL 任务结果。`PaddleDocumentPipelineResult.Elapsed` 统计检测前处理/推理/映射及区域准备/生成阶段，外层文件读取和首次解码不在其中；测量完整调用延迟时应在 `RunAsync` 外部计时，不能直接将该字段作为正式端到端性能。

输出保留原始 VL 文本。Spotting 任务自动解析四边形，JSON 的 `spotting.regions` 同时保留八个量化值、`sourcePolygon` 和 `pagePolygon`，Markdown 明确区分源裁剪与整页坐标。源尺寸采用 Spotting 放大前的原图尺寸，页面平移采用实际取整/裁边后的整数 crop 原点，原 `sourceRegion` 仍保留请求框。控制台单独打印解析状态和多边形数，生成成功计数不等于解析成功。

可将 manifest 的 `task` 设置为 `TextSpotting`；混合模式则在 `taskByCategory` 中配置 `"spotting": "TextSpotting"`，并提供对应区域。只有完整 EOS、标记配对及四边形都合法时 `CoordinatesAvailable=true`，本 OpenCV 适配器也会提供 `PageCoordinatesAvailable=true`。源框本身不被当作文字预测框；其它任务不因页面聚合的可用标记变成有坐标。

表格的逻辑单元格/HTML 已有[官方对照及真实页面联测](../../../eng/models/paddle-ocr/verification/paddleocr-vl-otsl-tables-20261024.md)，Spotting 见[坐标规则及实际 ROI 联测](../../../eng/models/paddle-ocr/verification/paddleocr-vl-spotting-20261024.md)和[详细使用教程](../../../docs/articles/visual-paddle-structure.md#vl-spotting文字四边形与页面坐标)。几何阅读排序现已可选；标注集阅读顺序质量、单元格像素框、其它任务语义与独立质量仍需分别验证。
