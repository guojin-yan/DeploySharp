# 从 DeploySharp V1 迁移

DeploySharp 2.0 是新的包结构和 API 设计，不提供 V1 的源码、二进制、类型或配置兼容层。迁移时请按“任务与模型合同”选择 V2 Profile，而不要按 V1 枚举名或旧文件名做字符串映射。

## 任务映射

| V1 使用场景 | V2 入口 | 说明 |
| --- | --- | --- |
| YOLO 分类、检测、分割、姿态、旋转框 | `DeploySharp.Visual` 的 YOLO Profile | 根据实际导出布局选择相应 Profile；标签、输入尺寸和输出名属于模型合同。检测模型见 [YOLO 检测](visual-yolo-detection.md)。 |
| DEIM、RF-DETR、RT-DETR、PP-YOLOE | `PortableDetectorProfiles` | 使用对应的便携检测器 Profile；多输入和原始 query 输出必须显式配置。 |
| PaddleOCR | `PaddleOcrPipeline` | 检测、方向分类、识别以完整流水线组织，可配置 batch 和会话池。 |
| PaDiM、PatchCore 等异常检测 | 异常检测 Profile | 输入尺寸、阈值、输出布局必须与模型导出保持一致。 |
| BRIA RMBG | 背景移除 Profile | 返回连续 alpha 蒙版；由调用方决定抠图、合成或保存格式。 |
| SAM 提示分割 | `PromptableSegmentationImageSession` | 对同一图像先 set-image，再多次提交点、框或掩码提示，完成后 clear/dispose。 |
| CLIP、SigLIP 图文匹配 | 视觉语言嵌入会话 | 需要与模型匹配的 tokenizer；CLIP 与 SigLIP 的评分方式不可互换。 |
| BLIP Caption | 生成式视觉语言会话 | Caption 不等同于 VQA，其他视觉语言模型请按自身支持状态评估。 |
| Donut、LayoutLMv3、Pix2Struct | 文档理解会话 | 页面、OCR、token、box 与 schema 仅能在同一模型合同内使用。 |
| Whisper 与本地 LLM | `DeploySharp.LLM` / LlamaSharp 后端 | 使用对应 tokenizer、模型格式和原生后端配置。 |

## 迁移步骤

1. 将旧代码的模型、标签、预处理和后处理参数整理为一个明确的模型合同。
2. 在 V2 中创建对应 Profile 与后端 Session，并用一张真实样例比对坐标、分类或文本结果。
3. 对批量或视频流改用 `RunManyAsync`、`RunPrefetchedAsync` 或 `InferenceBatchScheduler`；不要通过无控制的 `Task.Run` 共享单个推理 Session。
4. 确认部署后端可用后再切换生产流量。具体可用性以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 重要差异

- V2 的 Profile 与工件绑定：同一任务的不同导出模型可能拥有不同输入名、输出布局、标签和前后处理，不能随意替换。
- V2 的会话按资源所有权设计。图像、张量、缓存和设备缓冲的有效期应遵循 API 的 `Dispose`/`Clear` 约定。
- V2 的并发能力来自真实独立创建的 Session。GPU 内存和推理上下文有限，最佳会话数和 batch 需要在部署设备实测。

继续阅读[使用教程](usage-tutorial.md)、[模型支持指南](model-support.md)和各视觉任务页面，完成具体迁移。
