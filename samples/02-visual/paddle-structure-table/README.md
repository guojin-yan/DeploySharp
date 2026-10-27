# PP-Structure table pipeline / 表格流水线

This Windows sample runs a table task on one image: PP-LCNet table classification, RT-DETR cell detection, and SLANeXt table structure decoding to HTML. It uses ONNX Runtime CPU or OpenVINO CPU. The model downloader fetches and verifies only the three ONNX assets selected by the classifier from the `models-paddleocr` Release; `--offline` reuses an already populated cache without network access.

本案例在一张表格图上执行表格分类、单元格检测和 SLANeXt 结构识别，并输出带模型/输入来源和阶段时间的 JSON，以及 HTML。三个阶段共享一次图像解码。默认 `--table-type auto` 按分类结果只加载 wired 或 wireless 对应的后两份模型；可显式指定类型，分类结果不符时明确失败。无线模型已有逐模型后端证据，但不把有线案例的完整流水线实测外推为无线表格质量结论。

`--image` 可以使用自己的表格图。若要复现仓库实测的官方图片，可运行 [官方样例获取脚本](../../../eng/models/paddle-document/scripts/Acquire-PaddleDocumentValidation.ps1)，并将命令中的路径改为实际下载目录；脚本会校验图片 SHA-256。

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpResults\table `
  --table-type auto --backend ort
```

首次运行需要网络连接和 Windows x64 的 OpenCV/ONNX Runtime/OpenVINO native 运行库。模型可预先下载到缓存，之后给同一命令加 `--offline`。使用 OpenVINO 时将 `--backend ort` 改为 `--backend openvino`；SLANeXt 会自动选用 Release 中独立校验的 OpenVINO 兼容图，原始 ONNX 图不会被替换。

已有本地 ONNX 文件时可用 `--model-dir <目录>` 代替 Release 下载；该目录需有 `pp-lcnet-x1-0-table-cls.onnx`、所选类型的 `rt-detr-l-*-cell-det.onnx` 和 `slanext-*.onnx`。OpenVINO 需要同目录中的 `slanext-*-openvino-compat.onnx`。本地模式会计算实际文件 SHA 写入模型工件，但不验证它们与 Release catalog 相同；只有默认下载模式自动做 Release SHA 校验。`--model-dir` 不与 `--cache` 或 `--offline` 同用。

`--image` 可改为自己的表格图片；输出目录下的 `table-result.json` 包含分类、候选单元格、结构 token、区域和阶段时间，`table-result.html` 将 SLANeXt 的行片段包成可打开的 HTML 页面。此案例只输出表格**结构**，没有逐单元格 OCR 文本填充。自动模式下后两阶段的首次墙钟包含按需下载、哈希和 Session 创建，不能作为稳态推理时间；各模块 `metadata.elapsedMs` 记录实际推理时间，也不是正式性能基准。单元格检测分数阈值默认 `0.5`，可用 `--cell-score-threshold 0..1` 调整；设为 `0` 可查看全部低分诊断候选，但候选数不代表召回率。默认阈值在官方示例上留下 13 个检测区域，见 [定向验证](../../../eng/models/paddle-document/verification/paddle-structure-table-sample-threshold-20261027.md)；该阈值不是经过标注集校准的精度设置。没有标注真值的图片不能用于断言表格准确率；出现 `missing-eos:sequence-may-be-truncated` 警告时，HTML 可能不完整。

`table-cell-geometry-outside-source:N/M` 表示 M 个 SLANeXt 结构区域中有 N 个原始顶点超出图像边界；对应区域的 `metadata.geometry-outside-source` 为 `true`。此时边界裁剪后的区域可能退化，不能直接拿它们裁图或绑定 OCR 文本。官方示例图中 13 个结构区域有 12 个越界，而 RT-DETR 的高分检测框仍位于真实表格网格上；13 对 13 的数量相同不代表位置匹配。当前案例只使用 SLANeXt 的 HTML 结构，不进行单元格框融合。详见 [几何核对记录](../../../eng/models/paddle-document/verification/slanext-cell-geometry-20261027.md)。

模型/后端的精确支持边界和正式复现证据见 [PP-Structure 教程](../../../docs/articles/visual-paddle-structure.md)。
