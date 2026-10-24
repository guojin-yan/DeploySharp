# PaddleOCR-VL OTSL：逻辑表格、HTML 与真实页面联测

`PaddleOcrVlTableParser` 已能将完整 PaddleOCR-VL 表格生成转换为逻辑单元格和 HTML，包含横向、纵向及矩形合并。12 个解析样例和 2 个已有真实生成的单元格/HTML 均与固定官方转换器一致。新程序集另在两张 FUNSD 页面重跑真实 PP-DocLayout-L → VL → 表格导出：653 个 Token/文本仍精确一致，其中表格为 **28 行、6 列、73 个逻辑单元格**。

这是转换器与流水线正确性验证，不是表格内容准确率、页面阅读顺序、单元格像素定位或正式性能结论。

## 官方参考与输入身份

- 固定上游：[PaddleX `4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0` / `paddleocr_vl/uilts.py`](https://github.com/PaddlePaddle/PaddleX/blob/4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0/paddlex/inference/pipelines/paddleocr_vl/uilts.py)。原文件 SHA-256 为 `63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a`，Git blob 为 `601dfbfae08c97b0b4cf227ea8a9d8360627cd12`。
- [参考导出器](../scripts/Export-PaddleOcrVlTableReference.py)校验原文件 SHA/Git blob，通过 AST 只选择原样的两个表格类、OTSL 常量及五个转换函数，不导入整个 PaddleX Pipeline，也不执行模型。环境是 Python 3.10.12 / pydantic 2.12.3；完整定义清单和脚本 SHA 在[参考 JSON](paddleocr-vl-otsl-official-reference-20261024.json)中。
- [解析输入](paddleocr-vl-otsl-inputs-20261024.json)含中文、HTML 特殊字符、换行、空单元格、三类合并、可省略的末行分隔、补位、裁剪、最小代价宽度并列及空行。它们是人工构造的解析案例，不是模型质量样本。
- 两个真实 OTSL 来自既有[六任务官方生成](paddleocr-vl-six-task-reference-linux-20261023.json)和[真实检测 crop 官方生成](paddleocr-vl-detected-crop-official-reference-20261024.json)，均为同一固定 `PaddleOCR-VL-1.6` revision `c5630abae1d940eafe0697512a0325494b02ab42`，并确认 `stopReason=eos`。报告文件、原始文本 SHA、单元格及 HTML 均保存于参考 JSON。

| 真实输出 | 逻辑行×列 | 锚点单元格 | 严格解析 | 官方单元格 / HTML |
| --- | ---: | ---: | --- | --- |
| table-official-example | 4×4 | 13 | 成功 | 完全一致 |
| page-7-region-0 / FUNSD 87147607.png | 28×6 | 73 | 成功 | 完全一致 |

## 行为与边界

默认严格模式拒绝不等宽或空行、悬空延续、非矩形/重叠合并、非单元格位置的文本以及非 OTSL HTML。末行可由 EOS 终止。只有完整 EOS 输出才发布表格；`MaxTokens`、取消、错误、停止序列和未知终止原因不会输出“完整表格”，即使原文恰好形成矩形。

显式 `OfficialRowWidth` 采用官方最小代价宽度，保留全部带文本的锚点，补空槽、裁剪非文本尾槽及去掉空行；所有改动都有数量诊断并返回 `Repaired`。该策略可能改变合并跨度；修复后的网格仍必须通过合并合法性检查。它不改变原始结果，也不能作为模型内容准确率修复。

原文默认限额为 1,048,576 个 UTF-16 字符，原始行/列上限为 4096/1024，原始槽和修复后网格分别最多 65,536 格；在网格分配前检查。实现线性扫描标记，行宽优化用受最后文本锚点约束的低中位数，避免照搬官方逐候选宽度遍历；HTML 直接按行写出，不重复构造文本矩阵。取消请求会传播，数据问题通过状态/诊断返回。

单元格文本仅去掉两端空白，HTML 对 `& < > " '` 转义；仅产生库控制的 `table/tr/td` 及跨度属性。没有像素框、推测表头或置信度。`PaddleDocumentVisionLanguageResult` 保留原文、Token、EOS、页码、源框和输入身份，在 `Table` 附带默认严格分析。JSON 导出附带状态、诊断、单元格和 HTML；Markdown 继续用自适应围栏保留原始 OTSL，另显示已转义表格。

## 当前测试结果

- `PaddleOcrVlTableParserTests`：**28 passed / 0 failed / 0 skipped**。其中官方对照逐一检查 14 个向量；其它检查覆盖非法结构、不完整生成、预算、取消、不可变性、并发隔离、显式修复不改变默认分析，以及单图/单任务区域/混合页面的 JSON/Markdown 导出。
- Visual 全回归：**656 passed / 6 optional skipped / 0 failed**。跳过为未配置的 Whisper、自然宽行 trace、外部图 Bundle、Chart tokenizer 等既有外部资产测试；本轮 OTSL 测试没有跳过。
- OpenCV VL 原生回归：**21/21**；Visual 全部 **16 TFM Release 构建零警告/错误**；Windows x64 示例构建及独立 Ubuntu22.04 RID 发布均零警告/错误。
- 新外部测试 `ActualLayoutDetectorAndVlExportOfficialLogicalTableAndHtml`：**1 passed / 0 failed / 0 skipped**，约 2 分 12 秒，使用当前程序集。

真实运行设备为 Intel i7-1165G7（4 核/8 线程）、16 GB RAM、Ubuntu22.04.5、.NET10.0.9、OpenCV5.0.0、ORT1.28 CPU，intra-op=4 / inter-op=1，页面并发=1、VL Session=1，greedy 最大2048 Token。开发基线为 `9a43305` 加本范围代码；[机器报告](paddleocr-vl-detected-table-linux-20261024.json)记录实际模型/程序集 SHA 和逐区域输出。所有 3,960,768 个 Patch 值、653 Token/文本精确匹配官方参考且 EOS；每页3成功/0失败/1图片跳过。表格73个单元格的文本/行列/跨度/空锚点属性和完整 HTML 均相同，HTML SHA-256 为 `bd3c17d0ef53545d6e09b7baa98d924fecdef835eb67d29d92b2d061dd362511`。

[独立示例核对](paddleocr-vl-table-document-example-linux-20261024.json)使用单独 .NET8/Ubuntu22.04 发布目录、显式 unset `LD_LIBRARY_PATH`、.NET10 roll-forward 运行，退出码0。新增 `page-7-region-0.table.html` 与官方 HTML/测试导出逐字一致；两页 JSON 的全部 Token、文本、输入身份、来源坐标及 Markdown 原文均与联合测试一致。十个关键托管/native/运行配置文件本地发布与远端 SHA 一致，测试报告中的五个程序集身份也已核对。原始输出、TRX 及核对脚本留存外部 `E:\DeploySharpEvidence\paddleocr-vl-table-20261024`，不将模型、图片或运行目录加入 Git。

以上运行没有 5+50、首 Token、内存与时钟协议，不能用于性能排名。原模型误识别、检测器误分类或重叠区域不会由 OTSL 转换器解决；其它任务结构化、阅读顺序、标注集质量、其它后端和 VL Release 下载仍保持各自边界。

## 复现

纯解析不需要下载模型或安装 native：

```powershell
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release `
  --filter FullyQualifiedName~PaddleOcrVlTableParserTests
```

重建官方参考时，获取上述固定上游文件，保留其原始字节，并在独立 Python 环境安装 `pydantic==2.12.3`：

```bash
python eng/models/paddle-ocr/scripts/Export-PaddleOcrVlTableReference.py \
  --source /path/to/pinned-paddlex-uilts.py --repo-root /path/to/repo \
  --corpus eng/models/paddle-ocr/verification/paddleocr-vl-otsl-inputs-20261024.json \
  --output /path/to/official-table-reference.json
```

真实模型联测复用[检测器联测的模型/输入/native 准备步骤](paddleocr-vl-detected-pages-linux-20261024.md#复现入口)，额外指定官方表格参考：

```bash
export DEPLOYSHARP_PADDLEOCR_VL_TABLE_REFERENCE=/path/to/official-table-reference.json
export DEPLOYSHARP_PADDLEOCR_VL_DETECTED_OUTPUT=/path/to/new-table-output
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:ActualLayoutDetectorAndVlExportOfficialLogicalTableAndHtml --logger:trx
```

图片级独立应用入口见[页面示例](../../../../samples/02-visual/paddleocr-vl-document/README.md#运行与结果)；严格解析失败或生成未完成时返回非零退出码，不自动启用行宽修复。
