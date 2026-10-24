# PaddleOCR-VL 混合任务页面：区域路由与六任务真实生成

本记录验证 `PaddleOcrVlLayoutStage` / `OpenCvPaddleOcrVlLayoutStage`：同一张页面上的不同类别区域选择各自任务，每页解码一次，保留全部原始区域索引，明确区分成功、失败和跳过。[完整机器报告](paddleocr-vl-mixed-pages-linux-20261024.json)保存图、输入、程序集身份，以及全部 Token 和文本；[页面 manifest](paddleocr-vl-mixed-page-inputs-20261024.json)可交给[独立页面示例](../../../../samples/02-visual/paddleocr-vl-document/README.md)的 `Auto` 模式。

## 固定输入与运行环境

- 模型：`PaddlePaddle/PaddleOCR-VL-1.6`，revision `c5630abae1d940eafe0697512a0325494b02ab42`；四图 FP32 ONNX Bundle：vision、embedding、decoder、head。
- 设备环境：Ubuntu 22.04.5 / .NET 10.0.9 / OpenCV 5.0.0 / ONNX Runtime CPU，4 个 intra-op 线程、1 个 inter-op 线程；1 个 VL Session，页面并发上限 2。
- 参考：[已固定的六任务官方生成](paddleocr-vl-reference-summary-linux-20261023.md)和[官方 RGB/Processor fixture](paddleocr-vl-managed-processor-parity-20261024.md)。六任务来自五张不同的源图，OCR 与 Spotting 使用同一源图。
- 输入：将这些固定 RGB 不缩放地放进两张无损 PNG 页面，使用手工区域类别/矩形。页面 7 为 686×867，页面 3 为 701×1741；每页三个任务区域和一个主动跳过区域。二进制 PNG/RGB/Patch 留在外部验证目录，不提交 Git。

这是拼接页面与手工版面输入的集成验证，不是独立真实多页文档质量，也没有运行自动版面检测模型。路由器只消费已有类别，不推断类别或阅读顺序。

## 实际结果

| 页面 | 原区域索引 | 类别 | 实际任务 | Token | Patch / Token / 文本对照 | 结束 |
| ---: | ---: | --- | --- | ---: | --- | --- |
| 7 | 0 | table | TableRecognition | 71 | 全部精确一致 | EOS |
| 7 | 1 | figure | 跳过 | — | 不准备输入、不执行生成 | — |
| 7 | 2 | formula | FormulaRecognition | 80 | 全部精确一致 | EOS |
| 7 | 3 | chart | ChartRecognition | 137 | 全部精确一致 | EOS |
| 3 | 0 | seal | SealRecognition | 29 | 全部精确一致 | EOS |
| 3 | 1 | unmapped | 跳过 | — | 不准备输入、不执行生成 | — |
| 3 | 2 | spotting | TextSpotting（自定义映射） | 286 | 全部精确一致 | EOS |
| 3 | 3 | text | TextRecognition | 162 | 全部精确一致 | EOS |

六个任务共 **765 Token**，全部 EOS；六个 crop 的全部 **6,818,448 个 float32 Patch 值**匹配参考 SHA。页面顺序保持 7→3；跳过项仍占据原始索引 1，后续结果不会被重新编号。生成完成顺序可以不同，返回顺序仍与页面/区域输入一致。

源工厂共调用两次，每个页面一次；OpenCV 适配器解码后的底图用于该页所有已映射区域。矩形是原图 stride 视图，转换为紧密 RGB 后由托管 Processor 完成官方重采样与 Patch 打包。页面 metadata 保存 PNG 编码 SHA，区域 metadata 保存尺寸绑定的 RGB SHA；每个成功项分别保存实际任务和对应的文档模块。

外部集成测试 `MixedTasksCropTwoPagesAndMatchEveryOfficialToken`：`1 passed / 0 skipped / 0 failed`，约 3 分 23 秒（包含图加载、两个页面和六次生成）。报告中的 elapsed 是单次观察，没有 5+50、首 Token 或内存采样，不用作正式 P50/P95、设备上限或速度排名。

## 合同与兼容验证

Visual 全回归为 `628 passed / 6 optional skipped / 0 failed`。新混合阶段与原单任务阶段的聚焦测试 `33/33`，覆盖大小写/空白匹配、不可变映射、显式跳过与未知类别备用任务、任务模块/源坐标/输入身份、失败隔离、任务感知备用生成、截断警告、空版面，以及忽略取消 Token 的回调不会掩盖取消。

OpenCV VL 专项 `14/14`，包含混合页面的单次图像源读取、实际 RGB 区域准备、并发页面、跳过索引、尺寸错配、局部失败和提前取消。OpenCV 库全部 15 个目标框架 Release 编译零警告/错误；实际 VL 实现保持只在 .NET 8/9/10 启用，没有新增 NuGet 包。

## 复现

先准备相同 revision 的官方 tokenizer、四图 Bundle、六任务参考 JSON 和官方 RGB/Patch fixtures。使用包含匹配 ORT/OpenCV native 的 Visual OpenCV 测试运行目录：

```bash
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/pinned-tokenizer
export DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES=/path/to/managed-processor-fixtures
export DEPLOYSHARP_PADDLEOCR_VL_REFERENCE=/path/to/generation-six-tasks-cached-20261023.json
export DEPLOYSHARP_PADDLEOCR_VL_MIXED_PAGE_OUTPUT=/path/to/new-mixed-page-output
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:MixedTasksCropTwoPagesAndMatchEveryOfficialToken --logger:trx
```

测试生成两张 PNG、`pages.json`、`report.json` 以及每页 JSON/Markdown。`pages.json` 使用 `task=Auto`，并显式映射 `spotting` → `TextSpotting`；其它任务使用默认 PP-Structure 标签映射。单任务示例的 `task=TableRecognition` 等既有模式继续保留。

独立示例已在同一 Linux 设备实际运行：发布目标为 .NET 8 / `ubuntu.22.04-x64`，通过 `DOTNET_ROLL_FORWARD=Major` 使用 .NET 10.0.9，并显式移除 `LD_LIBRARY_PATH`。使用自身发布目录的 ORT/OpenCV native，不借用测试目录；两个页面均输出 `succeeded=3 failed=0 skipped=1`、退出码 0。逐区域 JSON 的全部 765 Token/文本、任务/模块、源框和输入 SHA 与集成测试一致，Markdown 保留原始模型文本。[示例核对摘要](paddleocr-vl-mixed-document-example-linux-20261024.json)记录各输出文件 SHA 和五个运行文件的本地/远端哈希核对。Windows x64 示例也编译零警告/错误。

## 当前边界

仍未验证真实版面检测模型→VL 的联合质量、自动阅读顺序、独立标注集、JPEG Decoder parity、HTML/表格单元格/公式/图表/Spotting 结构化归一，或 CUDA/OpenVINO/TensorRT/OpenCV DNN 的 VL 推理。源框只是输入来源，`CoordinatesAvailable=false`；所有结果均保留原始 VL 文本。

模型二进制还没有进入 VL 下载 catalog 或 Release；本记录不将混合路由结果外推到其它权重、后端或完整 PP-Structure 支持。
