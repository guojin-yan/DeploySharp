# 官方 SAM 视频 ROI 完整案例

该案例将 C# `VisualRoiVideoPromptRunner` 接到 Meta 官方 SAM2/SAM3 Predictor，
包括真实视频解码、ROI 点/框提示、逐帧传播、中途修正、基于跟踪掩码质心的区域事件、
重置后首帧一致性校验，以及 PNG 掩码和 JSON 结果输出。

Python 适配器由应用拥有，不会给 Visual NuGet 包新增 Python/PyTorch 依赖。
这里验证的是官方 PyTorch 路径，不能据此宣称 SAM 视频已支持全部部署后端。

## 准备与运行

完整的环境安装、官方源码版本和命令见 [英文操作说明](README.md)。
使用独立 Python 3.12 环境；GPU 安装与驱动匹配的 PyTorch，CPU 验证使用官方 CPU wheel。
SAM2 固定使用 `sam2.1_hiera_tiny.pt` 与对应 tiny 配置；SAM3 使用 `sam3.pt`。

```bash
dotnet run --project samples/02-visual/roi-official-sam/OfficialSamRoi.csproj -c Release -- \
  /absolute/path/.venv/bin/python sam2 /models/sam2.1_hiera_tiny.pt \
  /video/bedroom.mp4 /results/sam2-run-1 \
  2b90b9f5ceec907a1c18123530e92e794ad901a4 6 0.25,0.2,0.5,0.7
```

最后两个参数为帧数与归一化 ROI `x,y,width,height`。短测试允许 3–100 帧，输出目录
不能已有 `frames` 子目录。SAM3 按官方视频对象分割案例共享 tracker/backbone，支持
本例的点和框提示；这里不测试文本驱动概念检测。RTX 2060 不支持 BF16，案例会选用 FP16；
CPU 使用 FP32。首次模型加载时间与逐帧预测时间分别记录。

## 结果与生命周期

`report.json` 保存设备、运行时、源码版本、输入/权重 SHA-256、每帧掩码指纹与面积、
预测耗时、掩码回读与导出耗时、GPU 分配量、区域事件及重置一致性结果。
`00000-1.png` 等文件为源图尺寸的真实掩码。该结果属于短视频功能证据，不是数万帧
稳定性或最优吞吐性能结论。

同一个 Predictor 按帧串行执行。成功重置后可以复用已加载模型；预测失败或取消时
会终止子进程，避免复用部分推进的内存状态。重试须重新创建适配器和 Planner。
进程错误、模型不匹配或视频过短会明确失败，不会产出虚假的通过记录。

[ROI 总指南](../../../docs/articles/visual-roi-configuration.md) ·
[设备性能矩阵](../../../docs/articles/roi-backend-performance-matrix.md)

## 已验证范围（2026-09-16）

SAM2 tiny 已在 Ubuntu 22.04 / i7-1165G7、Python 3.12.14、PyTorch 2.10.0+cpu 上通过官方 `bedroom.mp4` 前 6 帧短测：初始化、传播、修正、源图掩码和重置后首帧掩码指纹一致。此结果不代表 GPU 吞吐或原生 SAM Bundle 可用。

固定版本的 SAM3 官方实现会在位置编码缓存构造时直接创建 CUDA Tensor，因此 CPU 实测失败。其适配入口仍需匹配 CUDA 的 PyTorch 与足够显存验证；不能因为安装了 CPU wheel 就宣称 SAM3 支持 CPU。长时 GPU soak 和 VLM 真模型验证不在本轮短测范围。

补充 CUDA 实测：Python 3.12.14 / PyTorch 2.10.0+cu128 已正确识别 RTX2060，但本例 SAM3 官方模型在首帧发生 CUDA OOM。尝试 FP16 权重存储仍超出当前 6GB 显存预算，因此未保留为默认优化。请在更充足显存设备上重新验证；本例仍不能标为 SAM3 端到端通过。
