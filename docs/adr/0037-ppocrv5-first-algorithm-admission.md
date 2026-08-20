# PP-OCRv5 first AlgorithmVerified admission boundary / PP-OCRv5 首个算法准入边界

## Status / 状态

Accepted for 2.0.0-alpha.1 review. The first candidate is paddleocr/ppocrv5/mobile-cls, but it remains Preview; this ADR does not promote a catalog entry. / 本 ADR 在 2.0.0-alpha.1 审核阶段接受。首个候选为 paddleocr/ppocrv5/mobile-cls，但仍保持 Preview，本 ADR 不会晋级目录条目。

## Candidate selection / 候选选择

mobile-cls is the smallest and least ambiguous PP-OCRv5 path: one ONNX input, a two-class orientation output, an explicit 0_degree,180_degree label order, and a recorded official-image class/confidence golden. Its ModelPack binds the source archive SHA-256, paddle2onnx 2.0.2rc3 export contract, ONNX SHA-256, opset 7, prepared-tensor SHA-256, rejection threshold, and release asset identity. / mobile-cls 是六个 PP-OCRv5 路径中最小且语义最简单的候选：单个 ONNX 输入、二分类方向输出、明确的 0_degree,180_degree 标签顺序，以及已记录的官方图像类别/置信度 golden。其 ModelPack 绑定了源归档 SHA-256、paddle2onnx 2.0.2rc3 导出合同、ONNX SHA-256、opset 7、prepared tensor SHA-256、拒识阈值和 Release 工件身份。

The evidence is recorded in:

- eng/models/ocr-anomaly-rmbg/paddleocr-release-admission.json
- eng/models/ocr-anomaly-rmbg/paddleocr-license-redistribution-review.json
- eng/models/ocr-anomaly-rmbg/manifests/ppocrv5-mobile-cls.modelpack.json
- the six-entry models-20260818.ppocrv5.1 catalog Release

这些证据位于上述准入文件、ppocrv5-mobile-cls.modelpack.json 和 models-20260818.ppocrv5.1 六条目 catalog Release。

## Decision / 决策

The candidate is not AlgorithmVerified. The release-admission record now closes the immutable Preview Release identity, release-bound ORT/OpenVINO local golden, and independent CPU output from the pinned official Paddle Predictor. It intentionally keeps this blocker open:

1. license-and-redistribution: the upstream code license is observed, but the official model archives contain no model-specific license/NOTICE payload and no attributable model/dictionary redistribution approval is recorded.

候选不能标记为 AlgorithmVerified。准入记录现已关闭不可变 Preview Release 身份、release-bound ORT/OpenVINO 本机 golden，以及固定版本官方 Paddle Predictor 生成的独立 CPU 输出，并有意保留以下 blocker：

1. license-and-redistribution：已观察到上游代码许可证，但官方模型归档没有模型专属许可证/NOTICE，且没有可归属的模型/字典再分发批准记录。

Local ORT/OpenVINO execution, exporter reproducibility, SHA-256, decoder tests, or the independent official Predictor golden cannot close the remaining legal blocker. The catalog and released ModelPack declare redistribution for the existing Preview publication, while the stricter AlgorithmVerified legal review remains unapproved; the gate records those facts separately. / 本机 ORT/OpenVINO 执行、导出复现、SHA-256、解码测试或独立官方 Predictor golden 都不能关闭剩余的法律 blocker。catalog 与已发布 ModelPack 为现有 Preview 发布声明了可再分发，而更严格的 AlgorithmVerified 法律审核仍未批准；门禁分别记录这些事实。

## Exit criteria / 退出条件

Only after all of the following are committed to the evidence chain may the candidate be considered for promotion:

- explicit, attributable redistribution terms covering the model archive and ppocrv5_dict.txt;
- a hashed NOTICE/license bundle bound to the exact model and dictionary assets;
- an immutable Release asset set whose hashes match the ModelPack and catalog;
- official predictor and DeploySharp outputs compared on fixed, licensed input images with recorded tolerances;
- ORT/OpenVINO results checked against the same semantic golden;
- a deterministic admission test that fails on identity, preprocessing, label order, threshold, output, or tolerance drift.

只有以下证据全部进入链路后，候选才可考虑晋级：

- 覆盖模型归档和 ppocrv5_dict.txt 的明确、可归属再分发条款；
- 与精确模型和字典工件绑定并带哈希的 NOTICE/许可证包；
- 哈希与 ModelPack、catalog 一致的不可变 Release 工件集合；
- 在固定且获许可的输入图像上比较官方 Predictor 与 DeploySharp 输出，并记录容差；
- 使用相同语义 golden 检查 ORT/OpenVINO 结果；
- 在身份、前处理、标签顺序、阈值、输出或容差发生漂移时失败的确定性准入测试。

Until then, the catalog entry must remain Preview, and V1 AlgorithmVerified completion remains unchanged at 0/32. / 在此之前，目录条目必须保持 Preview，V1 AlgorithmVerified 完成数继续为 0/32。
