# DeploySharp 2.0.0 博客系列

本系列面向使用 C#/.NET 部署模型的开发者，围绕“安装与模型获取 → 完成任务 → 配置和扩展 → 并发与区域处理 → 性能验证”展开。基于 [DeploySharp 2.0.0](https://github.com/guojin-yan/DeploySharp/releases/tag/v2.0.0)，源码 Tag 为 `v2.0.0`，对应提交 `f07ed7f7d7e2e53ccb443e1517f34ebfc4f743c6`。

第 01—04 篇主稿已完成。01 介绍项目、特色、模块与优势；02 从按场景安装到 YOLO26 检测、绘图，并在 Windows 跑通 ORT CPU、ORT CUDA、OpenVINO CPU、OpenCV DNN 与 TensorRT；03 组合 PaddleOCR DET、裁剪、CLS 与 REC，解释字典、Batch 和独立通道，补充 Windows/Ubuntu 基础运行及 Ubuntu v5 Mobile 四后端对照；04 讲解后端依赖与离线交付，同一份 YOLO26 CPU 工程自包含发布后在 Windows/Ubuntu 运行，并演示 PaddleOCR 单模型缓存。随文代码均使用正式版 NuGet 验证。Windows ORT CPU 另外新实跑了 v4 Mobile 与 v6 Tiny，未将 Ubuntu v5 结果扩大到其他组合。其余文章仍为拟写主题，目前尚未向博客平台发表。各后端、模型与环境的范围分别说明。

## 主线选题

| 编号 | 拟定标题 | 读者完成的事情 |
| --- | --- | --- |
| 01 | [DeploySharp 2.0.0 正式发布：多后端、多模型，让 C# 模型部署更简单](2026-10/2026-10-28-01-release-overview.md) | 主稿完成：了解项目定位、新版特色、模块用途与优势，并选择后续教程入口 |
| 02 | [在 C# 中使用 DeploySharp 部署 YOLO26：从 NuGet 安装到检测结果](2026-10/2026-10-28-02-yolo26-quickstart.md) | 主稿完成：按场景安装、使用本地模型、绘图，并查看 Windows 四种引擎、五条执行路径的结果与计时 |
| 03 | [在 C# 中运行 PaddleOCR v4/v5/v6：从文字检测到完整识别结果](2026-10/2026-10-28-03-paddleocr-pipeline.md) | 主稿完成：Windows/Ubuntu 运行完整 OCR，输出 TXT/JSON/文字框，对照 Ubuntu v5 Mobile 四后端 |
| 04 | [DeploySharp 多后端部署：选择推理引擎，准备运行时与离线模型](2026-10/2026-10-28-04-multi-backend-offline.md) | 主稿完成：按引擎准备依赖，Windows/Ubuntu 自包含目录实际推理，在线准备与严格离线读取模型缓存 |
| 05 | DeploySharp 视觉多任务实战：分类、分割、关键点与旋转框 | 根据任务选择模型、预处理和结果类型，正确还原源图坐标 |
| 06 | 在 C# 中使用 PP-Structure：从页面区域到表格 HTML | 组合版面、区域 OCR 与表格阶段，理解其他文档任务的接入边界 |
| 07 | 为 DeploySharp 接入自定义模型：配置前处理、后处理与结果类型 | 接入自有模型，并选择缩放、颜色和归一化方式 |
| 08 | DeploySharp 批量与视频推理：Batch、Session 池和异步预取 | 选择调度方式，管理并发、输入所有权、取消和资源释放 |
| 09 | DeploySharp ROI 实战：配置检测区域、过滤结果与还原坐标 | 保存区域配置，执行区域任务并将结果映射到原图 |
| 10 | 使用 DeploySharp 处理大图：滑动窗口、重叠切片与结果合并 | 规划切片、完成检测、合并跨窗口重复结果并检查边缘目标 |
| 11 | DeploySharp TensorRT 优化实战：Engine、CUDA 前后处理与数据复用 | 在支持的模型路径上构建 Engine、减少传输并测量实际收益 |
| 12 | DeploySharp 2.0.0 性能实测：不同设备和后端应该怎样比较 | 阅读和复现具名设备结果，区分延迟、吞吐与完整流水线时间 |

01—04 主稿均创建于 2026-10-28，配图按编号保存在 `2026-10/assets/` 下。01 提供特色长图与流程图；02 的 8 张图讲解依赖、后端选择、端到端数据流、输出映射、坐标还原和 Windows 多后端实测；03 的 10 张图包含题图、官方多栏输入、OCR 流程、v4/v5/v6 组合、跨平台运行时、字典与 CTC、输出文件、Batch/Session 调度、四后端耗时及真实 v5 Mobile 结果，144 条区域输出只节选 10 条，完整结果由程序保存；04 的 8 张图讲解后端决策、运行时分层、离线目录、交付流程、缓存、换机排障，并展示自包含程序的真实检测结果。新增示意图均有 SVG 与 PNG，模型结果图由实际代码生成。后端对照、离线推理和缓存源代码位于 `2026-10/examples/`，性能表说明输入、设备、预热次数、计时边界和验证限制；Windows ORT CUDA 已使用官方同版本 CUDA 12 原生包跑通，并补充了独立发布步骤。下一篇继续展开 05：分类、分割、关键点与旋转框。

## 后续专题

主线之外，可按实际读者需求增加以下完整案例：

| 主题 | 案例范围 |
| --- | --- |
| PaDiM 异常检测 | MVTec bottle 模型、异常分数、热力图、阈值和自有数据适用性 |
| BRIA RMBG 背景移除 | RMBG 1.4/2.0、Alpha、透明背景导出和模型精度选择 |
| SAM v1 提示分割 | 点/框提示、Mask feedback、源图坐标和支持的 Bundle |
| CLIP 与 BLIP Caption Base | 图文匹配和图像描述两个案例，说明两类任务与输出的区别 |
| 本地 Qwen2.5 GGUF | LlamaSharp CPU、文本生成、流式输出、取消与资源管理 |

PaddleOCR-VL 1.6 可在文档处理文章中作为实验性补充介绍；是否单独成篇，以真实输入、质量结果和明确的后端范围为准。SAM2/SAM3 原生视频、Whisper 完整 Bundle 等未闭合路径暂不列为已完成的部署教程。

## 发表记录

目前尚未发表本系列文章。实际发表后在此登记：系列编号、平台、发表日期、文章 URL 和对应主稿；不以写作日期代替发表日期。

## 版本资料

- [2.0.0 版本说明](../../releases/2.0.0.md)
- [模型支持指南](../../articles/model-support.md)
- [模型与后端验证矩阵](../../model-backend-verification-matrix.md)
- [设备性能实测](../../articles/device-performance-benchmarks.md)
- [返回博客文章索引](../README.md)
