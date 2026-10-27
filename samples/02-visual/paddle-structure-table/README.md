# PP-Structure table pipeline / 表格流水线

This Windows sample runs PP-LCNet table classification, RT-DETR cell detection, SLANeXt structure decoding, and PP-OCRv5 Mobile recognition of aligned cells. It uses ONNX Runtime CPU or OpenVINO CPU. The model downloader fetches only the three table assets selected by the classifier, plus the recognizer and its dictionary when cell alignment succeeds. `--offline` reuses an already populated cache.

本案例在一张表格图上执行表格分类、单元格检测和 SLANeXt 结构识别；当单元格候选对齐成功时，用 PP-OCRv5 Mobile 批量识别并填入 HTML。整个过程共享一次图像解码；仅识别路径不创建无用 DET Session。默认 `--table-type auto` 按分类结果只加载 wired 或 wireless 对应的后两份表格模型；可显式指定类型，分类结果不符时明确失败。无线模型已有逐模型后端证据，但不把有线案例的完整流水线实测外推为无线表格质量结论。

`--image` 可以使用自己的表格图。若要复现仓库实测的官方图片，可运行 [官方样例获取脚本](../../../eng/models/paddle-document/scripts/Acquire-PaddleDocumentValidation.ps1)，并将命令中的路径改为实际下载目录；脚本会校验图片 SHA-256。

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpResults\table `
  --table-type auto --backend ort
```

首次运行需要网络连接和 Windows x64 的 OpenCV/ONNX Runtime/OpenVINO native 运行库。模型可预先下载到缓存，之后给同一命令加 `--offline`。使用 OpenVINO 时将 `--backend ort` 改为 `--backend openvino`；SLANeXt 会自动选用 Release 中独立校验的 OpenVINO 兼容图，原始 ONNX 图不会被替换。

已有本地 ONNX 文件时可用 `--model-dir <目录>` 代替 Release 下载；该目录需有 `pp-lcnet-x1-0-table-cls.onnx`、所选类型的 `rt-detr-l-*-cell-det.onnx` 和 `slanext-*.onnx`。OpenVINO 需要同目录中的 `slanext-*-openvino-compat.onnx`。若对齐成功并启用文字填充，还需 `PP-OCRv5_mobile_rec.onnx` 和 `ppocrv5_dict.txt`；它们可放在同一目录，或分别通过 `--recognizer-model` 和 `--recognizer-dictionary` 指定。本地模型会记录实际 SHA，字典会校验预期 SHA；只有默认下载模式自动按 Release catalog 校验模型。`--model-dir` 不与 `--cache` 或 `--offline` 同用。

`table-result.json` 包含分类、候选单元格、结构 token、区域和阶段时间；`table-result.html` 是可打开的 HTML 页面；`table-cell-ocr.json` 单独记录每个结构槽位的检测框序号、文字、置信度和 REC 模型身份。`--structure-only` 可跳过 REC，保留原先只导出结构的路径。对齐失败时不会猜测单元格位置，HTML 保留结构，OCR JSON 记录 `skipped`；识别失败时保留结构文件并以非零退出码及 `failed` 诊断结束。首次墙钟包含按需下载、哈希和 Session 创建，不能作为稳态推理时间。单元格检测分数阈值默认 `0.5`，可用 `--cell-score-threshold 0..1` 调整；设为 `0` 可查看全部低分诊断候选，但候选数不代表召回率。默认阈值在官方示例上留下 13 个检测区域，见 [定向验证](../../../eng/models/paddle-document/verification/paddle-structure-table-sample-threshold-20261027.md)；该阈值不是经过标注集校准的精度设置。没有标注真值的图片不能用于断言表格准确率；出现 `missing-eos:sequence-may-be-truncated` 警告时，HTML 可能不完整。

`table-cell-geometry-outside-source:N/M` 表示 M 个 SLANeXt 结构区域中有 N 个原始顶点超出图像边界；对应区域的 `metadata.geometry-outside-source` 为 `true`。此时边界裁剪后的区域可能退化，不能直接拿它们裁图或绑定 OCR 文本。官方示例图中 13 个结构区域有 12 个越界，而 RT-DETR 的高分检测框仍位于真实表格网格上；案例只在额外几何合同通过时使用独立检测框做文字裁剪，不融合越界的结构框。详见 [几何核对记录](../../../eng/models/paddle-document/verification/slanext-cell-geometry-20261027.md)。

案例额外输出 `table-cell-alignment.json`：它只在输入来源、结构单元格数量、检测框数量和逐行几何顺序都一致时，将 SLANeXt 的结构槽位关联到独立 RT-DETR 检测框。`Succeeded=false` 时 `Matches` 为空并列出诊断；跨行单元格当前明确不自动对齐。即便 `Succeeded=true`，也只是一个候选裁剪位置映射，不是表格准确率保证。官方有线图的 ORT CPU 与 OpenVINO CPU 单次四阶段运行见[单元格识别记录](../../../eng/models/paddle-document/verification/paddle-structure-table-cell-ocr-20261027.md)：13 条文字和最终 HTML 完全一致。另有两张官方无线图的[定向记录](../../../eng/models/paddle-document/verification/paddle-structure-table-wireless-20261027.md)：分类、检测和结构阶段可运行，但检测/结构单元格数量不一致，逐格 OCR 被安全跳过。无线四阶段和标注精度仍未验证。

模型/后端的精确支持边界和正式复现证据见 [PP-Structure 教程](../../../docs/articles/visual-paddle-structure.md)。
