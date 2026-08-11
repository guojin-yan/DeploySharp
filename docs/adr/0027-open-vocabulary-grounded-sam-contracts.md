# ADR 0027: Open-vocabulary identity and Grounded-SAM composition / 开放词汇 Identity 与 Grounded-SAM 组合

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-08

Image-only YOLO-World can contain pre-export text embeddings, Grounding DINO consumes runtime text, and YOLOE has multiple prompt modes. Rank cannot reveal vocabulary, tokenizer, phrase alignment, or NMS ownership. / 仅图像 YOLO-World 可固化文本，Grounding DINO 使用运行时文本，YOLOE 有多种模式；Rank 不能说明词汇、Tokenizer、Phrase 或 NMS。

We bind vocabulary order/normalization, tokenizer, text encoder, prompt embedding, graph, ports, processing, thresholds, NMS, provenance, license and capacities in one immutable Profile. Fixed/runtime-text/visual/prompt-free modes are distinct; missing identity is a blocker. Canonical detection, transform, SAM, mask/RLE and ownership implementations are reused. One OpenCV decode produces both inputs; state installs atomically, rejects concurrent mutation, commits nothing on cancellation, and owns three sessions. Only traceable native artifacts execute; no Python daemon or handwritten prompt/memory replacement is allowed. / 单一不可变 Profile 绑定完整 Identity；四种模式互不冒充；缺 Identity 必须阻断。复用全部规范结果/几何。OpenCV 单次解码、状态原子安装、拒绝并发、取消不提交部分状态并拥有三条 Session；仅可追溯 native 工件执行。

The result is real ORT/OpenVINO YOLO-Worldv2 and Grounded-SAM evidence, while incomplete Grounding DINO/MMYOLO/YOLOE stay External. Assets, catalog, Release, Actions and TensorRT remain untouched. / 结果是双后端真实证据；不完整路径保持 External，资产、目录、Release、Actions 与 TensorRT 均不写入。
