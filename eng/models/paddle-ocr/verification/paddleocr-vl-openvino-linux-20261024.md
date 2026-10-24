# PaddleOCR-VL-1.6：OpenVINO CPU 六任务动态生成验证

固定四图 ONNX Bundle 已在生产 `PaddleOcrVlOnnxSession.GenerateAsync` 和 OpenVINO CPU 后端完成六任务生成：全部 765 个 Token 与原始文本精确匹配官方及 ORT 参考，全部正常 EOS。36 路首次空 KV、后续增长和每图一次视觉编码均通过逐步断言。

这是带明确内存配置的 Linux 单图生成正确性证据。它不代表 Windows VL、OpenVINO 页面/版面联合质量、人工标注准确率、其它权重或 GPU 后端验证，也不是正式性能或长时间稳定性结论。16 GB 无 Swap 主机的异步默认配置曾 OOM，不能省略下方资源边界。

## 身份与环境

| 项目 | 本次实际值 |
| --- | --- |
| 权重 | `PaddlePaddle/PaddleOCR-VL-1.6` / `c5630abae1d940eafe0697512a0325494b02ab42` |
| 四图 | 原始固定导出的 `vision.onnx`、`embedding.onnx`、`decoder.onnx`、`head.onnx`，没有为本次运行改图 |
| CPU / 主机内存 | Intel Core i7-1165G7，4 核 / 8 逻辑处理器；16 GB RAM；无 Swap |
| OS / .NET | Ubuntu 22.04.5 x64 / .NET 10.0.9 |
| Managed / native | `JYPPX.OpenVINO.CSharp.API 3.3.1`；实际 build `2026.3.1-22476-759c5a6ab8c-releases/2026/3` |
| 执行配置 | CPU，Latency，4 inference threads，1 stream，1 request，每图一个独立 Session；Batch=1 |
| 精度 | Float32 图输入/输出，原生默认内部计算；没有设置精度提示，不宣称所有内部运算为严格 FP32 |
| 生成 | 有界 greedy，最多 512 个新 Token；六次顺序调用共享四个 Session，每次独立 KV；每次超时 15 分钟 |
| 进程资源设置 | `MALLOC_ARENA_MAX=2`、`DOTNET_GCHeapHardLimit=0x40000000`（托管堆 1 GiB） |
| 输入 | 同一组六张固定真实输入的上游解码 RGB；C# Processor 准备 Patch，不重复运行 Python 模型 |

生产代码基线为 `74924db31c3795525f36502f7c001ec6b7f49f62`，加本轮专项测试。最终测试源码 SHA-256 为 `80dd06c74cf7bfca246ac1d89e9efd91a8477265a2a69fbf64864cf34647d541`。五个实际运行程序集和十三个实际加载 native 模块的 SHA 均保留在[原始机器报告](paddleocr-vl-openvino-linux-20261024.json)，并与本地最终构建及对应 Linux runtime 包逐一复核。后台负载和时钟没有连续采样，不据此比较后端性能。

构建时工作树含有既有的其它测试文件修改，本轮没有纳入提交；报告的 `sourceCommit` 是开发基线，不表示干净 checkout。实际运行文件以逐程序集 SHA 为准，专项源码 SHA 不能单独保证从该基线重建出位级相同的整份测试程序集。

四图 SHA 与[现有 ORT 六任务记录](paddleocr-vl-managed-csharp-ort-linux-20261024.json)逐一相同：

| 图 | SHA-256 |
| --- | --- |
| vision | `940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f` |
| embedding | `1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86` |
| decoder | `823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad` |
| head | `8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148` |

官方生成参考 SHA 为 `aaa5ace39098c307e27107562045994aabdd952fcf8dc1cb388be87e6f58eb79`，RGB fixture manifest SHA 为 `ff6ffe322ac3a1373ce830ffc953e18173115b654f265591c0dd214c701db481`。来源、输入身份和 Processor 复现见[六任务官方参考](paddleocr-vl-six-task-reference-linux-20261023.json)及[托管 Processor 对照](paddleocr-vl-managed-processor-parity-20261024.md)。

## 六任务结果与检查范围

| 输入 / 任务 | Prompt Token | 生成 Token | 官方 Token / 文本 / EOS | 单次 Elapsed（秒） |
| --- | ---: | ---: | --- | ---: |
| `ocr-demo-1` / OCR | 337 | 162 | 全部一致 / EOS | 20.897 |
| `table-official-example` / 表格 | 163 | 71 | 全部一致 / EOS | 7.641 |
| `formula-official-example` / 公式 | 173 | 80 | 全部一致 / EOS | 8.574 |
| `chart-official-example` / 图表 | 453 | 137 | 全部一致 / EOS | 21.541 |
| `spotting-demo-1` / 文字定位 | 1309 | 286 | 全部一致 / EOS | 95.809 |
| `seal-official-example` / 印章 | 543 | 29 | 全部一致 / EOS | 10.745 |

`Elapsed` 是生成结果自带的单次诊断时间，包含视觉编码、embedding、prefill/decode、head 和 argmax，不包含图文件读取/哈希、加载/编译、RGB 准备、Prompt/位置规划、最终文本解码及报告写入。六个不同任务不能当作重复样本计算正式 P50/P95；没有预热、5+50 或锁频协议，不宣称后端排名或加速倍率。

[真实外部测试](../../../../tests/DeploySharp.Backend.OpenVINO.Tests/PaddleOcrVlIntegrationTests.cs)通过 `1/1`，并检查：

- 四图实际端口、类型和动态维度，六图共 6,818,448 个 Patch 数值的字节 SHA，图像/参考身份；
- 每张图仅调用一次 vision；embedding、decoder、head 调用数各等于该任务生成 Token 数；
- decoder 首步 query 等于 Prompt 长度、36 个 past KV 的长度均为 0；之后 query=1，past=`Prompt+step-1`，36 个 present KV 长度均为 `Prompt+step`；
- 全部 765 个 Token、原始文本、EOS，以及各个调用的来源，不使用容差放宽 Token 差异；
- 失败时保留已采集病例和轨迹，只有六任务全部到 EOS 且精确一致才报告完成。

带固定官方资产的 Visual VL 专项回归为 **155 passed / 0 skipped / 0 failed**。本机 OpenVINO 常规回归为 **25 passed / 11 optional external skipped / 0 failed**；后者不充作 VL 真实模型或所有外部模型通过。专项测试 Release 编译零警告/错误。

## 内存失败、复测与边界

首次同步生成已完整通过。随后异步默认进程被内核 OOM 终止；仅设置 1 GiB 托管堆预算的复测也在 Spotting 阶段终止。[失败快照](paddleocr-vl-openvino-heap-oom-linux-20261024.json)保留前四任务成功及 Spotting 的 `running` 状态；这是未完成的快照，不能按前四项成功汇总为全部通过。

内核对第二次失败记录为：

```text
10月 24 19:23:55 ygj-NUC11PHi7 kernel: Out of memory: Killed process 752004 (dotnet) total-vm:25765844kB, anon-rss:10808476kB, file-rss:128kB, shmem-rss:16768kB, UID:1000 pgtables:23684kB oom_score_adj:0
```

失败快照中前四任务末尾托管存活对象约 46–183 MB，而进程 WorkingSet 约 9.1–10.0 GiB；不能把整个占用归结为托管 KV 数组，也不能据此断言已定位某个原生内存泄漏。原生工作区、线程分配器保留及动态 shape 的贡献仍需要专门 profiling。

在同一最终程序集上额外设置 `MALLOC_ARENA_MAX=2` 后，异步六任务全部通过，进程最高 RSS 约 **12.05 GiB**；Spotting 后托管存活约 489 MiB，印章后约 69 MiB。该资源设置是本次可复现配置，不是所有设备上的最优参数或普适修复。1 GiB 预算仅限制 .NET 托管堆，不限制原生权重、工作区或整个进程；不能承诺进程只使用 1 GiB。

没有修改远端 Swap、系统分配器配置、驱动或 CPU/GPU 时钟，也没有通过 `GC.Collect()` 把测试硬压过关。原始归档、成功/失败 TRX、运行脚本与独立 SHA 复核位于外部 `E:\DeploySharpEvidence\paddleocr-vl-openvino-20261024`。内存优化、更多图像/生成长度及长期运行保持开放；此证据不授权扩大 Session 数量或宣称稳定内存上界。

## 复现

先准备上述精确图和官方 tokenizer，并按[Processor 复现步骤](paddleocr-vl-managed-processor-parity-20261024.md)生成同一组六任务 RGB fixture。Linux 需要匹配的 OpenVINO 2026.x native 库和依赖；测试项目中的 Windows runtime 引用不能替代 Linux `.so`。已有构建输出可部署到独立目录，或从源代码运行专项测试：

```bash
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/pinned-official-tokenizer
export DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES=/path/to/managed-processor-fixtures
export DEPLOYSHARP_PADDLEOCR_VL_REFERENCE=/path/to/generation-six-tasks-cached.json
export DEPLOYSHARP_PADDLEOCR_VL_OPENVINO_REPORT=/path/to/output/openvino.json
export DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT=$(git rev-parse HEAD)
export DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256=$(sha256sum tests/DeploySharp.Backend.OpenVINO.Tests/PaddleOcrVlIntegrationTests.cs | cut -d' ' -f1)
export LD_LIBRARY_PATH=/path/to/openvino-2026-linux-native
export DOTNET_GCHeapHardLimit=0x40000000
export MALLOC_ARENA_MAX=2
dotnet test tests/DeploySharp.Backend.OpenVINO.Tests/DeploySharp.Backend.OpenVINO.Tests.csproj \
  -c Release --filter FullyQualifiedName~FourDynamicGraphsGenerateAllSixRealTasksWithGrowingKvAndExactReferenceTokens
```

缺少必要环境变量时测试明确跳过，不会伪造后端勾号。实际应用注册 OpenVINO 后复用相同 Session API，见[用户用法](../../../../docs/articles/visual-ocr.md#paddleocr-vl-是否已经可用于模型推理)。图文件仍未进入下载 catalog/Release；本次不把验证成功写成模型已经可下载。
