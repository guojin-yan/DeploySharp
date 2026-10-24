# PaddleOCR-VL：解码底图复用、真实区域裁剪和两页生成验证

本记录验证 `OpenCvPaddleOcrVlRegionStage` 的图片解码、区域像素、生成和页面来源。对应 [机器报告](paddleocr-vl-regional-pages-linux-20261024.json)包含源图/图/程序集 SHA、全部生成 Token、文本和终止状态。

## 输入、环境和边界

使用固定 `PaddlePaddle/PaddleOCR-VL-1.6` revision `c5630abae1d940eafe0697512a0325494b02ab42`，ONNX Runtime CPU 四图 FP32 Bundle。环境为 Ubuntu 22.04.5 LTS、.NET 10.0.9、OpenCV 5.0.0、CPU provider 4 个 intra-op 线程、1 个 VL Session、页面并发上限 2。

输入来自[六任务官方 Processor 对照](paddleocr-vl-managed-processor-parity-20261024.md)中的 `table-official-example`：原图 551×132，固定 RGB SHA 为 `d4565ea740b369bfaa6700e0894d189bf267f59a80a3d5eaf8716d646091335d`。测试将该图的 RGB 无缩放地放入两张白色 PNG 页面，共三个矩形；测试代码生成 PNG 和页面 manifest，二进制图片保留在外部验证目录，不进入 Git。

这些是**由同一个真实样图拼成的页面、手工区域**，不是三个独立质量样本，也不运行版面检测模型。首个页面故意按下方区域→上方区域输入；页码按 7→3 输入，用于检查调用方顺序不会被重新排序。

## 实际结果

| 输入页码/尺寸 | 区域索引 | 页面矩形 x,y,w,h | Patch 与官方参考 | Token/文本 | 结束 |
| --- | ---: | --- | --- | --- | --- |
| 7 / 599×330 | 0 | 29,181,551,132 | 精确一致 | 71 Token，精确一致 | EOS |
| 7 / 599×330 | 1 | 17,19,551,132 | 精确一致 | 71 Token，精确一致 | EOS |
| 3 / 700×220 | 0 | 60,31,551,132 | 精确一致 | 71 Token，精确一致 | EOS |

总计 213 Token。每个 crop 的 `352,800` 个 float32 Patch 值均匹配官方参考文件 SHA `c66c539dd3c39ceab0c5eea3613091c099853d3396a689227e13a6e5e94dbcbb`。三个区域内容和尺寸相同，所以 RGB 裁剪身份相同：`c5a057abf4d50a56e0ad5efec82591578826ba3b644c28bde287abe3f79d223a`；各自位置、页面编码 SHA 与页码分别保留。生成完成顺序可以不同，页面返回顺序和页面内区域顺序均与输入一致。

测试通过 `TwoPagesCropRealTableRegionsAndGenerateExactTokens`，`1 passed / 0 skipped / 0 failed`。最终代码单次测试约 44 秒，包括图文件校验、加载和三次生成；报告中的每次生成观察约 9.4–10.6 秒。这没有预热/重复 5+50 协议，也没有首 Token 或持续内存采样，不能当正式 P50/P95、页面吞吐或后端排名。

单元与回归验证另外覆盖 PNG 文件/字节入口一致性、非连续行 stride、BGR/BGRA/Gray→RGB、分数边界裁剪、超 Int32 的有限边界、源文件移除后的底图复用、并发 crop、取消、像素预算、Dispose、尺寸错配、局部失败隔离与多页来源。修复 Markdown 导出将 `<fcel>` 等原始 Token 误当 HTML 的问题，以长度自适应的原文代码围栏保留内容；增加 HTML Token 和模型自身围栏的测试。Visual 全回归 `605 passed / 6 optional skipped`；OpenCV 本轮 VL+ROI 范围 `18 passed / 14 optional external skipped`，其中本轮 VL 专项 11 项均通过。六项 Visual 跳过与本次 VL 无关，OpenCV 的 14 项是另外的数据/后端 opt-in 外部测试，没有计入本次真实区域结果。OpenCV 全部 15 个目标框架 Release 编译通过，零警告/错误。

## 复现

先按 [RGB Processor 对照](paddleocr-vl-managed-processor-parity-20261024.md)生成固定 `fixtures.json`、RGB 与 Patch 文件，并准备相同 revision 的四图 Bundle、官方 Tokenizer 和六任务 reference JSON。Linux 测试运行目录需包含 Visual OpenCV 测试程序集、ORT CPU native 和匹配的 OpenCV 5 native 系统依赖：

```bash
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/pinned-official-tokenizer
export DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES=/path/to/managed-processor-fixtures
export DEPLOYSHARP_PADDLEOCR_VL_REFERENCE=/path/to/generation-six-tasks-cached-20261023.json
export DEPLOYSHARP_PADDLEOCR_VL_PAGE_OUTPUT=/path/to/new-page-evidence-directory
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:TwoPagesCropRealTableRegionsAndGenerateExactTokens --logger:trx
```

输出目录包含 `page-7.png`、`page-3.png`、`pages.json`、`result-page-7.json`、`result-page-3.json` 和 `report.json`。可直接将其中的 `pages.json` 交给[页面示例](../../../../samples/02-visual/paddleocr-vl-document/README.md)继续验证真实 CLI 和 Markdown/JSON 导出。

页面示例也已通过独立运行：以 .NET 8 目标发布到 `ubuntu.22.04-x64`，显式携带 ORT CPU 和 OpenCV runtime，使用发行版 RID graph 保留原生资产。在同一 Linux .NET10 机器通过 `DOTNET_ROLL_FORWARD=Major` 启动，不借用测试目录的 native DLL 或设置 `LD_LIBRARY_PATH`。两页输出分别为 `page=7 succeeded=2 failed=0` 和 `page=3 succeeded=1 failed=0`，退出码 0；Windows x64 构建也为零警告/错误。模型图和 Tokenizer 仍是命令传入的外部资产。JSON 对照及 Markdown 原文检查摘要见 [示例输出核对](paddleocr-vl-document-example-linux-20261024.json)。

## 尚未覆盖

本记录验证 PNG 解码后的区域像素，没有证明任意 JPEG Decoder 与 Pillow 逐像素一致。未验证原生版面模型→自动任务路由/阅读顺序、独立真实多页文档质量、HTML/单元格/Spotting 结构化输出、GPU 或其它后端。页面区域坐标是来源信息，VL 输出的 `CoordinatesAvailable` 仍为 `false`。
