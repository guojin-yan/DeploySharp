# 公式模型后端质量/执行状态索引（2026-10-14 更新）

这是一份证据索引，不是新的全量推理报告。它把六个官方 Paddle 公式 ONNX 导出的当前状态集中列出，避免把 ORT 的单后端字符串诊断、OpenVINO/OpenCV 的阻断和其它 PP-Structure 模型的 TensorRT 结果混为“公式模型全部支持”。机器可读版本见 [JSON](formula-backend-quality-status-20261011.json)。

## 当前矩阵

| 后端 | 当前状态 | 证据范围 |
| --- | --- | --- |
| ONNX Runtime CPU | **已执行，但质量未通过发布门** | MathNet realFormula v1 的 121 条人工标注样本；六模型 exact、CER、EOS 和空输出见 [六模型汇总](formula-realformula-six-models-ort-20261007.md)。 |
| ONNX Runtime CUDA | **进程隔离有界执行/Parity；质量未评测** | ORT 1.23.2 CUDA opt-in restore 在相同 realFormula 图片和专用预处理下与 CPU 对照；Plus-S/M/S 可在单进程三样本运行，Plus-L/L/UniMERNet 当前只接受每样本新进程的单样本证据。长生命周期多样本探针出现 CUDA Int64 状态污染，因此不能宣称普通多样本 Session 支持。详见 [ORT CUDA 报告](formula-realformula-ort-cuda-20261014.md)。这不是 121 样本质量或性能结论。 |
| OpenVINO CPU | **不支持** | 六个精确导出逐进程隔离；五个 FormulaNet 在 Loop-18 导入/规范输入处阻断，UniMERNet 在 model-reader 阶段中止。见 [隔离报告](formula-openvino-isolated-20261008.md)。 |
| OpenCV DNN CPU | **batch=1 与动态 Batch 合同均不支持** | 六个精确导出已分别尝试 batch=1 与 batch=2；batch=1 的五个 FormulaNet 在 `ConstantOfShape` 零维度处阻断，UniMERNet 在 importer 分配阶段阻断。见 [batch=1 边界](paddle-document-formula-batch1-opencv-20261012.md) 和 [动态 Batch 边界](paddle-document-formula-dynamic-batch-opencv-20261007.md)。这只限定当前 OpenCV 5.0.0 与精确工件，不外推其它 OpenCV 版本。 |
| TensorRT CUDA | **当前 TRT10.11/TRT11 精确 Builder 均阻断；执行/质量/性能未验证** | TensorRT 11.0.0.114-cu12 的 Batch=1 探针中，五个 FormulaNet 在 Loop `BitwiseAnd` 检查失败，UniMERNet 在 If `ConcatFromSequence` 检查失败；TensorRT 10.11.0.33-cu12 / API 10 bridge 的动态 Batch=2 探针得到相同的两类精确 importer 边界。六个 Engine 均未创建，因此没有 Decoder、token parity、公式质量或性能结果。见 [TRT11 五图汇总](formula-tensorrt11-builder-probe-20261012.md)、[TRT11 UniMERNet 记录](formula-unimernet-tensorrt11-builder-probe-20261012.md)和 [TRT10.11 动态 Batch 探针](paddle-document-formula-tensorrt-builder-probe-20261014.md)。Plus-S 的 TRT10.11 parser-startup `0xC0000005` 与上述动态 Batch importer 结果是独立历史诊断；不能从其他 PP-Structure TensorRT 证据外推。 |

## ORT CPU 121 条样本诊断

| 模型 | Exact | 字符 CER | EOS | 空输出 |
| --- | ---: | ---: | ---: | ---: |
| PP-FormulaNet Plus-S | 14/121 | 57.04% | 121/121 | 0 |
| PP-FormulaNet Plus-M | 11/121 | 58.34% | 121/121 | 0 |
| PP-FormulaNet Plus-L | 16/121 | 54.60% | 121/121 | 0 |
| PP-FormulaNet-S | 12/121 | 61.58% | 121/121 | 0 |
| PP-FormulaNet-L | 17/121 | 89.86% | 120/121 | 0 |
| UniMERNet | 18/121 | 51.59% | 121/121 | 0 |

这里的 CER 是去除 Unicode 空白后的字符串 Levenshtein 诊断，不能代表 TeX 语义等价或数学正确性；数据集与 checkpoint 的训练重叠未知。高 CER/低 exact 已经是质量风险，不应以单张官方示例的 EOS 或 Decoder 合同掩盖。

从同一六份逐样本 JSON 还生成了[结构诊断索引](formula-realformula-structure-quality-20261011.md)及[机器可读结果](formula-realformula-structure-quality-20261011.json)：按参考公式长度分层，统计生成括号是否平衡、生成 LaTeX 命令数量、exact/CER/EOS。进一步使用本地 MathNet manifest 的参考标签（只通过图片 SHA 关联，不把原始 LaTeX 写回仓库）生成了[命令 token 质量诊断](formula-realformula-command-quality-20261011.md)及[机器可读结果](formula-realformula-command-quality-20261011.json)。六模型的命令 token micro-F1 为 `45.15%–63.01%`；这仍是精确词法重叠，不是 TeX 渲染或数学语义指标。

2026-10-12 又增加了[保守语法/结构诊断](formula-realformula-syntax-quality-20261012.md)及[机器可读结果](formula-realformula-syntax-quality-20261012.json)。该解析器只检查常见命令参数、分组和 `begin/end` 环境闭合，并将字母/数字抽象后比较结构指纹；六模型预测语法有效率为 `82.64%–96.69%`，结构指纹匹配为 `9.09%–14.88%`。MathNet 参考标签自身有 7 条不满足这个保守子集，因此这些数字只用于发现 malformed output/结构退化，不是 TeX 渲染、数学语义或发布质量分数。

同日补充了基于 SymPy ANTLR LaTeX parser 的[解析可接受性诊断](formula-realformula-latex-parseability-20261012.md)及[机器可读结果](formula-realformula-latex-parseability-20261012.json)。在六模型共 `726` 条输出中，`548` 条被当前 parser 接受、`178` 条被拒绝，整体接受率为 `75.48%`（各模型 `73.55%–78.51%`）。该 parser 只覆盖其自身支持的 LaTeX 子集；数据集中的 `aligned/array`、自定义宏、物理记号和不规则转义等合法或近似合法写法也可能被拒绝。因此该结果只用于定位 malformed-output 和 parser coverage 边界，**不表示 TeX 能成功渲染，不表示视觉相似，不表示数学语义等价，也不改变 ORT 的发布质量结论**。

2026-10-13 已补充锁定 `MathJax 4.1.3` SVG 和 `sharp 0.35.5` 的[渲染/视觉结构诊断](formula-realformula-render-quality-20261013.md)及[逐样本机器结果](formula-realformula-render-quality-20261013.json)。121 条参考中 `118` 条由该配置成功渲染；六模型预测分别成功 `110/121`、`121/121`、`120/121`、`111/121`、`117/121` 和 `120/121`，可比较配对为 `107–118/121`。失败项保留图片、公式哈希和精确 MathJax/栅格器错误，不复制原始 LaTeX。前景 IoU P50 仅 `0.2253–0.2434`，与现有低 exact/高 CER 风险方向一致，但该像素指标对排版和字形敏感，**只关闭“没有可复现渲染器”的工具阻断，不关闭数学语义等价或发布质量门**。

本次同步后的[本地文档链接审计](document-link-audit-20261013-formula-render.json)覆盖受维护的 PP-OCR/PP-Structure 入口文档；它只检查仓库内 Markdown 目标，不验证外部 URL、模型执行或质量结论。

TensorRT 11 已对六个官方公式 ONNX 分别执行固定 batch=1 Builder 探针；TensorRT 10.11 又以动态 batch=2 profile 对同六图完成独立 DeploySharp Builder 探针。两条 runtime 线都没有创建 Engine：五个 FormulaNet 的精确阻断原因是 `BitwiseAnd`，UniMERNet 是 `ConcatFromSequence`。精确 SHA、profile 和错误边界分别记录在 [TRT11 FormulaNet 五图汇总](formula-tensorrt11-builder-probe-20261012.md)、[TRT11 UniMERNet 独立探针](formula-unimernet-tensorrt11-builder-probe-20261012.md)和 [TRT10.11 动态 Batch 探针](paddle-document-formula-tensorrt-builder-probe-20261014.md)。六模型的[静态兼容性审计](formula-tensorrt-static-compatibility-20261011.md)仍是图结构风险盘点，不是支持判定；任何新导出/plugin 均需从 Engine、token parity、真实质量和性能重新准入。

## 已知 FormulaNet-L 边界

一个真实样本在 raw token index `1,024` 前没有 EOS。复现表明官方 Paddle IR 与 ONNX Loop 均有 1,024 步生成上限；尝试把 Loop 延长会在 learned positional embedding `[1026,512]` 的 index `1,026` 越界。该现象不能通过在 DeploySharp Decoder 中伪造 EOS 修复，否则会静默截断公式；当前保留 `missing-eos` 警告。详见 [generation-limit](formula-formulanet-l-generation-limit-20261007.md) 与 [EOS/预处理敏感性](formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md)。

## 收尾门

1. 在已有 MathNet 词法和 MathJax 渲染/视觉结构诊断基础上，补充数学语义等价指标及独立渲染器复核；当前 ORT 结果仍只作诊断。
2. 继续定位 FormulaNet-L 的非 EOS 样本，但不得扩大 decoder 上限或合成终止 token 来掩盖模型导出边界。
3. 获得匹配 TensorRT 运行时和可复现 Engine 后，再单独建立六模型 TensorRT 质量/性能矩阵；OpenVINO 需要新的兼容导出或 importer 修复证据。
