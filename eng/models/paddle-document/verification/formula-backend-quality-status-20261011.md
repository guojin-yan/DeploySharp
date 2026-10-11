# 公式模型后端质量/执行状态索引（2026-10-11）

这是一份证据索引，不是新的全量推理报告。它把六个官方 Paddle 公式 ONNX 导出的当前状态集中列出，避免把 ORT 的单后端字符串诊断、OpenVINO/OpenCV 的阻断和其它 PP-Structure 模型的 TensorRT 结果混为“公式模型全部支持”。机器可读版本见 [JSON](formula-backend-quality-status-20261011.json)。

## 当前矩阵

| 后端 | 当前状态 | 证据范围 |
| --- | --- | --- |
| ONNX Runtime CPU | **已执行，但质量未通过发布门** | MathNet realFormula v1 的 121 条人工标注样本；六模型 exact、CER、EOS 和空输出见 [六模型汇总](formula-realformula-six-models-ort-20261007.md)。 |
| OpenVINO CPU | **不支持** | 六个精确导出逐进程隔离；五个 FormulaNet 在 Loop-18 导入/规范输入处阻断，UniMERNet 在 model-reader 阶段中止。见 [隔离报告](formula-openvino-isolated-20261008.md)。 |
| OpenCV DNN CPU | **动态 Batch 合同不支持** | 六个精确 batch=2 导出均为 `DS-OCV-8002`；见 [OpenCV 动态 Batch 边界](paddle-document-formula-dynamic-batch-opencv-20261007.md)。这不外推 batch=1 或其它 OpenCV 版本。 |
| TensorRT CUDA | **未验证** | 当前没有六模型公式 TensorRT 质量/性能矩阵；不能从版面、表格或 Chart2Table 的 TensorRT 证据外推。 |

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

从同一六份逐样本 JSON 还生成了[结构诊断索引](formula-realformula-structure-quality-20261011.md)及[机器可读结果](formula-realformula-structure-quality-20261011.json)：按参考公式长度分层，统计生成括号是否平衡、生成 LaTeX 命令数量、exact/CER/EOS。由于逐样本报告只保留参考标签哈希而不嵌入原始标签，索引不会伪造命令 precision/recall；这些仍是浅层字符串诊断，不是渲染或数学语义指标。

## 已知 FormulaNet-L 边界

一个真实样本在 raw token index `1,024` 前没有 EOS。复现表明官方 Paddle IR 与 ONNX Loop 均有 1,024 步生成上限；尝试把 Loop 延长会在 learned positional embedding `[1026,512]` 的 index `1,026` 越界。该现象不能通过在 DeploySharp Decoder 中伪造 EOS 修复，否则会静默截断公式；当前保留 `missing-eos` 警告。详见 [generation-limit](formula-formulanet-l-generation-limit-20261007.md) 与 [EOS/预处理敏感性](formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md)。

## 收尾门

1. 为公式模型补充有许可证、可追溯的多样本质量集，并增加渲染/结构语义指标；在此之前 ORT 结果只作诊断。
2. 继续定位 FormulaNet-L 的非 EOS 样本，但不得扩大 decoder 上限或合成终止 token 来掩盖模型导出边界。
3. 获得匹配 TensorRT 运行时和可复现 Engine 后，再单独建立六模型 TensorRT 质量/性能矩阵；OpenVINO 需要新的兼容导出或 importer 修复证据。
