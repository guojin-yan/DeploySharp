# ADR 0026: Promptable segmentation multi-session and state ownership / 可提示分割多 Session 与状态所有权

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-08

## Context / 背景

SAM-family files with similar names do not imply the same pipeline. SAM v1 separates image encoder from prompt/mask decoder; SAM 2 adds multiscale features and, for video, memory encoder/bank/attention; SAM 3 combines vision, text/geometry, detector, tracker, and gated checkpoints. Tensor rank and old SAM experience cannot identify these contracts. / 同名近似工件并不代表相同流水线。SAM v1 分离图像与提示/Mask 图；SAM 2 视频增加记忆组件；SAM 3 组合 Vision、Text/Geometry、Detector、Tracker 与受限 checkpoint，不能按 Rank 或旧经验判断。

## Decision / 决策

1. One immutable Profile binds every sub-artifact SHA/opset/source/export/license, exact named ports, prompt space, threshold/quality semantics, processing versions, and capacities. / 单一不可变 Profile 绑定全部子工件与执行语义。
2. A stateful image session owns both backend sessions and one embedding keyed by exact image/Profile/artifact identity. Typed prompt/feedback tensors are generated once from the cached transform; backends do not compute geometry. / 状态图像 Session 拥有两个 Backend Session 与精确 Identity 缓存；Typed Prompt/Feedback 只从缓存 Transform 生成一次。
3. Results wrap existing owned `InstanceSegmentationResult`, mask, RLE, and geometry contracts. Only generic quality, provenance, and low-resolution feedback data are added. / 结果复用既有自有 Mask/RLE/Geometry，只增加通用 Quality、Provenance 与低分辨率反馈。
4. Stateful operations reject concurrency. Cancellation commits no partial state; dispose cancels, waits, clears, and releases each child session once. / 状态操作拒绝并发；取消不提交部分状态；Dispose 取消、等待、清理并仅一次释放子 Session。
5. Video runtime APIs are withheld until a complete official native memory/tracker bundle executes. A blocker contract is documentation, not an executable fallback. / 完整官方 native Memory/Tracker Bundle 执行前不公开视频 Runtime API；Blocker 合同不是可执行回退。

## Consequences / 后果

SAM v1 image prompting is complete on ORT/OpenVINO without duplicate DTO or coordinate code. SAM 2/SAM 3 local graphs can carry exact External evidence without being mistaken for an official family implementation. Multi-artifact ModelFactory selection becomes explicit and rejects mixed or incomplete bundles. Model/native assets remain outside packages/catalog/Release, and TensorRT is still absent. / SAM v1 图像提示完整执行；SAM 2/3 局部图可记录精确 External 证据但不冒充官方完整实现。ModelFactory 显式选择完整 Bundle 并拒绝混合/缺失工件；模型/native 不进入包、catalog 或 Release，TensorRT 仍缺席。
