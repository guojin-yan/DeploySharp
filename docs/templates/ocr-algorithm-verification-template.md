# OCR AlgorithmVerified admission template / OCR AlgorithmVerified 准入模板

Complete every field for each detector + recognizer + character-set suite. Missing evidence means `ContractVerified` or Preview, never `AlgorithmVerified`. / 每个检测器 + 识别器 + 字符表套件必须填写全部字段；证据缺失时只能标记 `ContractVerified` 或 Preview，绝不能标记 `AlgorithmVerified`。

## Identity and legal provenance / 身份与合法来源

- Suite/model names and versions / 套件、模型名称与版本：
- Detector upstream repository, immutable commit, release, download URL / 检测器上游仓库、不可变 commit、Release、下载地址：
- Recognizer upstream repository, immutable commit, release, download URL / 识别器上游仓库、不可变 commit、Release、下载地址：
- Character-set upstream source, version, ordering rule / 字符表上游来源、版本与排序规则：
- Model, character-set, test-image, and golden-result licenses / 模型、字符表、测试图与黄金结果许可证：
- Redistribution approval and attribution / 再分发批准与归属：
- Original and converted file sizes/SHA256 / 原始及转换文件大小/SHA256：

## Official semantics / 官方语义

- Reference preprocessing/export/postprocessing source files and lines / 官方前处理、导出、后处理源码文件与行号：
- Detector orientation, color, resize bounds/rounding, padding, mean/std/scale, dtype/layout / 检测器方向、颜色、resize 边界/取整、padding、归一化、dtype/layout：
- Detector output activation, threshold, pixel center, morphology/contours, score, unclip, filtering, reading order, polygon convention / 检测输出 activation、阈值、像素中心、形态学/轮廓、score、unclip、过滤、排序与 polygon 约定：
- Crop point order, perspective transform, orientation, target height, width policy/alignment, padding, interpolation / Crop 点序、透视变换、方向、目标高度、宽度策略/对齐、padding 与插值：
- Recognizer color, normalization, dtype/layout, logits layout, softmax, character-set version, blank/EOS/unknown/repeat/confidence semantics / 识别器颜色、归一化、dtype/layout、logits layout、softmax、字符表版本、blank/EOS/unknown/repeat/置信度语义：
- Exporter versions, opset, dynamic axes, conversion command and patch / 导出器版本、opset、动态轴、转换命令与补丁：

## Golden comparison / 黄金对照

- Runner OS/CPU/GPU, backend/runtime/package versions / Runner 与 backend/runtime/package 版本：
- Test image IDs, licenses, sizes/SHA256 / 测试图 ID、许可证、大小/SHA256：
- Detector input tensor SHA256 and key samples / 检测输入 tensor SHA256 与关键采样：
- Raw detector outputs, thresholded candidates, polygons and source polygons / 检测原始输出、阈值后候选、polygon 与 source polygon：
- Crop image/tensor SHA256 and key samples / Crop 图与 tensor SHA256、关键采样：
- Raw logits, selected indexes, token traces, text, confidence / 原始 logits、索引、token trace、文本与置信度：
- Final canonical result SHA256 / 最终规范结果 SHA256：
- Absolute/relative tolerances and reason for every tolerance / 每项绝对/相对容差及理由：
- ONNX Runtime/OpenVINO and applicable platform cross-check / ONNX Runtime/OpenVINO 及适用平台交叉检查：

## Performance / 性能

- Release commit/configuration, warmup, sample count, concurrency / Release commit/配置、warmup、样本数与并发：
- Decode, detector preprocess/backend/postprocess P50/P95 / Decode、检测预处理/backend/后处理 P50/P95：
- Crop/warp and recognition batch preparation P50/P95 / Crop/warp 与识别批次准备 P50/P95：
- Recognizer backend, CTC, end-to-end P50/P95 / 识别 backend、CTC 与端到端 P50/P95：
- Images/regions/characters per second and managed allocations / 图像、区域、字符吞吐与托管分配：
- Accuracy benchmark, metric implementation, dataset split, official baseline and delta / 精度基准、指标实现、数据集划分、官方基线与差值：

## Review / 审核

- Reviewer and date / 审核人及日期：
- Accepted backend/format/device matrix / 接受的后端/格式/设备矩阵：
- Known limitations / 已知限制：
- ModelFactory catalog entry and immutable Release assets approved / ModelFactory 条目与不可变 Release 资产批准：
