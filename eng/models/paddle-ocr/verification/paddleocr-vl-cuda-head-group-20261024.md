# PaddleOCR-VL-1.6：ORT CUDA 显存实验记录

本记录验证固定 PaddleOCR-VL-1.6 四图 Bundle 在 Linux RTX2060 6GB 上的 ORT CUDA 行为，并记录为实验性派生图的边界。它不把部分任务通过升级为 PaddleOCR-VL 的 CUDA 全流程支持，也不改变矩阵中 TensorRT/OpenCV DNN 的状态。

## 固定环境

- Ubuntu 22.04.5 x64、Intel i7-1165G7、16GB RAM、无 Swap；NVIDIA GeForce RTX2060 6144MiB，驱动 580.178.04，CUDA 12.9，cuDNN 9.22。
- C# 测试使用 CUDA 12 opt-in 构建，managed/native ONNX Runtime 均为 1.23.2；仓库默认 ORT 1.28.0 与本次运行严格分离。
- `use_tf32=0`、`enable_cuda_graph=0`、`do_copy_in_default_stream=1`、`arena_extend_strategy=kSameAsRequested`、cuDNN HEURISTIC、4 intra-op/1 inter-op、Batch=1。后续实验额外启用统一 stream 和视觉调用的 `memory.enable_memory_arena_shrinkage=gpu:0`。
- 五类任务通过时 GPU 处于 P0、1005/5500MHz；没有锁频或连续遥测，因此不能宣称正式 P50/P95。

## 结果

| 图版本 | OCR | Table | Formula | Chart | Spotting | Seal | 结论 |
|---|---:|---:|---:|---:|---:|---:|---|
| 原始四图 | 通过 | 通过 | 通过 | 通过 | 显存失败，申请 1,719,926,784 bytes | 未执行 | 原始 6GB Bundle 不能完成六任务 |
| 头分组 2/1、保留 ORT float32 缩放 | 通过 | 通过 | 通过 | 通过 | 显存失败 | 通过/受前序实验影响 | 未通过完整门禁；2 头版本 CPU Spotting 有 1 个超出原容差元素 |
| ONNX Loop 顺序头分组 | 通过 | 通过 | 通过 | 通过 | MLP 阶段申请 89,247,744 bytes 失败 | 通过或受残留显存影响 | 未通过完整门禁 |
| 128-query 分块 + 全 key | 通过 | 通过 | 通过 | 通过 | MLP 阶段申请 89,247,744 bytes 失败 | 同一进程随后申请 36,429,056 bytes 失败 | 未通过完整门禁 |

头分组、Loop 和 query 分块均保留全部输入、key、权重、FP32 精度和原始 query/key 关系。六个固定输入的 CPU 视觉特征均逐值 bit-exact；因此当前阻断定位为目标设备上的 CUDA 工作区/显存峰值和同一进程的分配保留，而不是派生图的特征误差。显存 arena 收缩将已完成任务后的采样降到约 4GiB，但不能在 Spotting 的单次视觉编码中释放活动缓冲区。

原始和派生 JSON、特征报告以及 C# TRX 保存在本目录外的 `E:\DeploySharpEvidence\paddleocr-vl-cuda-20261024`，仓库只保留可复现实验脚本与本摘要。派生图不是发布资产，也没有进入模型 catalog 或 Release。

## 当前支持结论

PaddleOCR-VL-1.6 当前可宣称：固定四图 Bundle 的 ORT CPU、Linux OpenVINO CPU 六任务单图生成；区域/页面编排、表格 OTSL 和 Spotting 解析按各自报告成立。ORT CUDA 在 RTX2060 6GB 上是 **部分任务通过、完整六任务阻断**，不能在矩阵中标记为全流程可用。Windows CUDA、TensorRT、OpenCV DNN、正式质量集和正式性能仍需独立证据。

## 复现入口

`Test-PaddleOcrVlCudaEvidence.ps1` 只在原始失败、派生六任务成功、六任务 CPU 特征和程序集/native 身份全部满足时通过；当前实验报告故意不能通过该完整门禁。远端运行脚本使用相同的 C# 测试程序集和 CUDA 12 native pairing，并为每次尝试创建新的运行目录。
