# PaddleOCR-VL Spotting：定位标记、原图坐标与页面 ROI 联测

`PaddleOcrVlSpottingParser` 已支持官方块式和平铺输出，保留文字、八个原始量化值及四点顺序。八个合成向量和一个真实 EOS 输出的文字与所有源像素坐标均匹配未修改的官方解析函数。实际 C# ORT CPU 页面联测另验证 286 个 Token、15 个文字区域及 60 个顶点的源裁剪/页面映射。

本记录验证解析和准确坐标映射，不代表人工标注的文字/定位准确率、阅读顺序、检测器质量、其它权重或后端支持，也不是正式性能测试。

## 固定官方规则与参考

- 上游为 [PaddleX `4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0` / `paddleocr_vl/uilts.py`](https://github.com/PaddlePaddle/PaddleX/blob/4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0/paddlex/inference/pipelines/paddleocr_vl/uilts.py)。源文件 SHA-256 为 `63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a`，Git blob 为 `601dfbfae08c97b0b4cf227ea8a9d8360627cd12`。
- [参考导出器](../scripts/Export-PaddleOcrVlSpottingReference.py)校验原始 SHA/Git blob，仅 AST 选择四项正则常量和未修改的 `post_process_for_spotting`。Python 3.10.12 执行，不导入整个 PaddleX、不加载模型。脚本、生成来源和原始文本 SHA 见[官方参考 JSON](paddleocr-vl-spotting-official-reference-20261024.json)。
- 同 revision 的 [pipeline.py](https://github.com/PaddlePaddle/PaddleX/blob/4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0/paddlex/inference/pipelines/paddleocr_vl/pipeline.py)在准备时只把局部 `block_img` 交给 Spotting 放大；组装时重新读取 `block["img"]` 原始 crop 的宽高。因此坐标是量化值除以 1000 后乘原始源尺寸，不是放大后的尺寸。
- 真实原文来自[六任务官方生成](paddleocr-vl-six-task-reference-linux-20261023.json)的 `spotting-demo-1`，固定权重 `PaddleOCR-VL-1.6` revision `c5630abae1d940eafe0697512a0325494b02ab42`，原图 500×500，286 个 Token，EOS。该原文包含 15 个文字四边形；不是人工标注真值。

八个合成向量涵盖配对块、先文本后定位的分组块、Unicode/公式文本、多个平铺区域、空文字、反向顶点绕序、合法凹四边形及坐标间空白。逐点双精度值与官方 JSON 直接比较，不通过浮点容差放宽，也不转换成 float 后宣称精确。

## 行为与边界

`<|TEXT_START|>…<|TEXT_END|>` 对应一个文本块，`<|LOC_BEGIN|>…<|LOC_END|>` 对应八个位置标记。平铺格式为“文字 + 八个 `<|LOC_n|>`”，可以重复多次。数值要求整数且在 `[0,1000]`，四点保留模型顺序；`x=n/1000.0*SourceSize.Width`，`y=n/1000.0*SourceSize.Height`。坐标不取整、不裁边、不补置信度，文字按上游规则去掉两端空白。完整文字以双换行连接，原始生成永远单独保留。

默认严格解析。官方函数会截取多余位置值、忽略尾部数据、按较短块数配对；本库将这些情况报告为 `InvalidStructure`。不完整标记、文本/定位块不配对、未知保留标记、尾部无定位文字、重复点、退化或自交四边形也失败；任何失败都不发布部分成功区域。只有 EOS 才可能成功，非 EOS 返回 `IncompleteGeneration`。默认上限 1,048,576 个 UTF-16 字符与 4096 个区域，超限 `LimitExceeded`，取消传播 `OperationCanceledException`。实现有界扫描，不使用回溯正则。

`SourcePolygon` 是原始 crop 局部像素；只有显式 `PaddleOcrVlSourceFrame` 才提供 `PagePolygon`。OpenCV 文件和整页入口自动绑定整页坐标，矩形区域入口绑定裁边、floor/ceil 后的实际整数 crop 原点和尺寸，不拿请求浮点框作为实际原点。`WithSourceFrame` 只建立新的输入包装，不复制 Patch Tensor。自定义旋转/透视/额外缩放 crop 不能套用此平移合同，未提供映射时只提供局部源坐标。

单图、区域和混合页面结果自动附带 `Spotting` 分析；原文构造器缺少图像规划时明确报告源几何不可用。`CoordinatesAvailable` 与 `PageCoordinatesAvailable` 独立，页面聚合只表示至少一个区域可用，必须逐区域检查。`SourceRegion` 保留原始版面请求框。JSON 同时保存原始量化值、两个命名坐标系和状态/诊断；Markdown 的文字用安全原文围栏，坐标分开命名。示例若解析失败返回非零退出码，不把“生成返回”当作“结构解析成功”。

## 当前验证

- 新 Spotting 合同 **30 passed / 0 failed / 0 skipped**，包括官方九向量、严格失败、不完整生成、限额、取消、帧验证、无 Patch 拷贝、并发隔离、旧原文 API、单图/单任务区域/混合聚合标记、JSON/Markdown，以及准备任务/页面和生成输入 SHA 错配。
- Visual 全回归 **686 passed / 6 optional skipped / 0 failed**；六项为未配置的其它外部资产测试。OpenCV VL 原生适配器回归 **21/21**。
- Visual 全部 **16 TFM Release** 编译通过；新生成与解析路径适用 .NET8/9/10。Windows x64 页面示例构建和独立 Ubuntu22.04 x64 发布通过。
- 真实外部测试 `FractionalPageCropGeneratesAllOfficialTokensAndMapsEverySpottingPoint` 在 Linux CPU 实际运行通过。最终模型、程序集 SHA、输入和所有断言边界见[机器报告](paddleocr-vl-spotting-page-linux-20261024.json)。

设备为 i7-1165G7（4 核/8 线程）、16 GB RAM、Ubuntu22.04.5、.NET10.0.9、OpenCV5.0.0、ORT1.28 CPU；intra-op=4/inter-op=1，单 Session，最大512 Token。开发基线为 `757888c` 加本范围实现，最终测试程序集身份与本地构建单独核对。

原始500×500 RGB无缩放拼入561×567无损 PNG，实际整数位置 `(17,29)`。请求框为 `(17.25,29.75,499.5,499)`，裁边和floor/ceil后恰好回到原始像素。全部 **3,048,192** 个 Patch 值的字节 SHA 与官方一致；286 Token/原文精确一致且EOS；15项识别文字和60个源图点逐一匹配官方，页面点逐一等于官方点加实际整数偏移，页码7和请求框保留，源工厂仅调用一次。

独立 .NET8/Ubuntu22.04 x64 页面示例在 .NET10 上以显式 Major roll-forward、unset `LD_LIBRARY_PATH` 运行，退出码0。全部286 Token/原文、15项文字/原始量化值、60个源图与页面点、实际映射、页码和请求框均与真实集成及官方参考一致；JSON/Markdown分别核对并保留原文。十个关键托管/native/配置运行文件与本地发布目录SHA一致，详见[独立应用核对 JSON](paddleocr-vl-spotting-document-example-linux-20261024.json)。新测试报告中的五个程序集也与本地最终测试目录SHA逐一相符。

首次复制测试目录缺少 OpenCV native，补齐已有可用 native 后又遇到内核 OOM。测试器已将大型 ONNX 图的整文件哈希改为流式哈希，减少无用大数组；远端同时存在其它编译工作。修复后的测试使用独立运行目录并显式 unset `LD_LIBRARY_PATH`，不把失败运行当作成功证据。原始失败/成功 TRX、PNG、页面导出与独立应用输出保留在外部 `E:\DeploySharpEvidence\paddleocr-vl-spotting-20261024`，大图和模型不进入 Git。运行耗时只用于正确性诊断，不是5+50、P50/P95或空闲设备性能。

## 复现

纯解析合同不需要模型或 native：

```powershell
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release `
  --filter FullyQualifiedName~PaddleOcrVlSpottingParserTests
```

重建参考需要上述固定原始 PaddleX 文件和本仓库的六任务生成 JSON；仅使用 Python 标准库：

```bash
python eng/models/paddle-ocr/scripts/Export-PaddleOcrVlSpottingReference.py \
  --source /path/to/pinned-paddlex-uilts.py --repo-root /path/to/repo \
  --output /path/to/spotting-official-reference.json
```

真实联测需要四图 Bundle、官方 tokenizer、[六任务 RGB fixture](paddleocr-vl-managed-processor-parity-20261024.md)和匹配的 ORT/OpenCV native：

```bash
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/pinned-checkpoint
export DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES=/path/to/managed-processor-fixtures
export DEPLOYSHARP_PADDLEOCR_VL_SPOTTING_REFERENCE=/path/to/spotting-official-reference.json
export DEPLOYSHARP_PADDLEOCR_VL_SPOTTING_OUTPUT=/path/to/new-spotting-output
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:FractionalPageCropGeneratesAllOfficialTokensAndMapsEverySpottingPoint --logger:trx
```

测试会生成 `pages.json` 和 `spotting-page.png`，可直接交给[页面示例](../../../../samples/02-visual/paddleocr-vl-document/README.md#运行与结果)。独立示例的 Token、原文、坐标和导出结果核对见[示例报告](paddleocr-vl-spotting-document-example-linux-20261024.json)。使用方法、局部/整页坐标与自定义准备器约束见[用户教程](../../../../docs/articles/visual-paddle-structure.md#vl-spotting文字四边形与页面坐标)。
