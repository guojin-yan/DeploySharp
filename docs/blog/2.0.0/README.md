# DeploySharp 2.0.0 博客系列

本系列面向使用 C#/.NET 部署模型的开发者，围绕“安装与模型获取 → 完成任务 → 配置和扩展 → 并发与区域处理 → 性能验证”展开。基于 [DeploySharp 2.0.0](https://github.com/guojin-yan/DeploySharp/releases/tag/v2.0.0)，源码 Tag 为 `v2.0.0`，对应提交 `f07ed7f7d7e2e53ccb443e1517f34ebfc4f743c6`。

基础教程第 01—08 篇与应用实战第 01—03 篇已成稿，均未向博客平台发表。基础教程讲清接口与模型用法，应用实战将多个模型组合成可运行的业务程序；两组文章独立编号，随文工程使用正式 NuGet `2.0.0`。

## 基础教程

| 编号 | 文章 | 读者完成的事情 |
| --- | --- | --- |
| 01 | [DeploySharp 2.0.0 正式发布：多后端、多模型，让 C# 模型部署更简单](2026-10/2026-10-28-01-release-overview.md) | 主稿完成：了解项目定位、新版特色、模块用途与优势，并选择后续教程入口 |
| 02 | [在 C# 中使用 DeploySharp 部署 YOLO26：从 NuGet 安装到检测结果](2026-10/2026-10-28-02-yolo26-quickstart.md) | 主稿完成：按场景安装、使用本地模型、绘图，并查看 Windows 四种引擎、五条执行路径的结果与计时 |
| 03 | [让 C# 读懂整张图：DeploySharp 驱动 PaddleOCR v4/v5/v6，打通 OCR 全流程](2026-10/2026-10-28-03-paddleocr-pipeline.md) | 主稿完成：Windows/Ubuntu 运行完整 OCR，输出 TXT/JSON/文字框，对照 Ubuntu v5 Mobile 四后端 |
| 04 | [模型部署不再卡在环境上：DeploySharp 四大推理引擎与 Windows/Linux 离线交付实战](2026-10/2026-10-28-04-multi-backend-offline.md) | 主稿完成：按引擎准备依赖，Windows/Ubuntu 自包含目录实际推理，在线准备与严格离线读取模型缓存 |
| 05 | [一套 C# 接口，解锁四类视觉任务：DeploySharp 分类、分割、关键点与旋转框实战](2026-10/2026-10-29-05-visual-multitask.md) | 主稿完成：Windows ORT CPU 四任务实跑，配置预处理与结果类型，输出源图叠色、二值掩码、有效骨架、旋转四边形与 JSON |
| 06 | [让文档不止于文字：DeploySharp + PP-Structure，打通版面分析与表格重建](2026-10/2026-10-29-06-paddle-structure.md) | 主稿完成：Windows/Ubuntu 的 ORT、OpenVINO CPU 跑通多栏页面区域 DET+REC、有线表格分类、单元格检测、SLANeXt 对齐与 HTML 填充，附共享原图输入适配器、Linux 运行命令和真实结果 |
| 07 | [不止于内置模型：用 DeploySharp 接管新模型的前处理、推理与结果解码](2026-10/2026-10-29-07-custom-visual.md) | 主稿完成：单模型六输出解码、RGB/Letterbox/归一化、DFL、NMS、自定义结果；Windows/Ubuntu 的 ORT、OpenVINO CPU 实跑 |
| 08 | [从单帧到并发流水线：DeploySharp 批量与视频推理实战](2026-10/2026-10-30-08-batch-video-inference.md) | 主稿完成：真实视频、有界窗口、动态 Batch、独立 Session 池、异步预取与缓冲租约；Windows/Ubuntu 的 ORT CPU、OpenVINO CPU、ORT CUDA、TensorRT 共 40 组调度配置，附 GPU 安装、动态 Engine、逐帧一致性、吞吐和窗口耗时对照 |
| 09 | DeploySharp ROI 实战：配置检测区域、过滤结果与还原坐标 | 拟写：保存区域配置，执行区域任务并将结果映射到原图 |
| 10 | 使用 DeploySharp 处理大图：滑动窗口、重叠切片与结果合并 | 拟写：规划切片、完成检测、合并跨窗口重复结果并检查边缘目标 |
| 11 | DeploySharp TensorRT 优化实战：Engine、CUDA 前后处理与数据复用 | 拟写：在支持的模型路径上构建 Engine、减少传输并测量实际收益 |
| 12 | DeploySharp 2.0.0 性能实测：不同设备和后端应该怎样比较 | 拟写：阅读和复现具名设备结果，区分延迟、吞吐与完整流水线时间 |

随文源代码位于 `2026-10/examples/`，配图位于 `2026-10/assets/`。第二篇包含 Windows YOLO26 五条执行路径，第三篇包含 Ubuntu v5 Mobile 四后端完整 OCR，第八篇在 Windows/Ubuntu 对照四条 CPU/GPU 执行路径的五种连续帧调度方式。具体设备、模型和输出以各篇实测章节为准。

## 多模型应用实战

| 编号 | 文章 | 模型组合与最终产物 |
| --- | --- | --- |
| 01 | [智能巡检台：园区车辆与人员事件分析](applications/2026-10/2026-10-29-01-campus-inspection.md) | NanoDet-Plus + CLIP：六张截图、区域统计、车型候选、事件证据与 HTML 复核台 |
| 02 | [智能文档工作台：混合资料解析与复核](applications/2026-10/2026-10-29-02-document-archive.md) | 文档方向 + PP-DocLayout + PP-OCR + 表格 + FormulaNet：9 张输入、4 组资料、双坐标、公式、表格、字段候选与复核工作台 |
| 03 | [智能素材工厂：商品图一站式处理](applications/2026-10/2026-10-29-03-product-catalog.md) | RMBG + CLIP + PP-OCR：透明图、白底图、类别候选、文字筛选、CSV 与可搜索图册 |

三个应用均提供完整工程、真实输入和结果，并在 Windows、Ubuntu 的 ORT CPU 与 OpenVINO CPU 上运行；文档工作台选择 OpenVINO 时，公式单独使用 ORT CPU。入口、目录与结果说明见[应用系列索引](applications/README.md)。

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
