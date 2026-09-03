# OpenVINO 兼容性与故障排查

`DeploySharp.Backend.OpenVINO` 是 OpenVINO Runtime 的适配层，不携带 Intel 原生运行时。当前公开验证基线是 Windows x64 的 CPU 设备；其他操作系统、架构和 OpenVINO 设备需要在目标环境独立验证后再用于生产。

## 运行时匹配

- 安装与 DeploySharp 适配包匹配的 `OpenVINO.runtime.win` 原生运行时。
- 托管包装与原生 Runtime 需要使用兼容版本组合；不要混用不同安装目录中的 DLL。
- 进程位数必须与 Runtime 一致。Windows x64 应使用 x64 的 .NET 进程和 x64 Runtime。
- CPU、AUTO、GPU、NPU 是 OpenVINO 的设备概念，并不表示 DeploySharp 对每个设备都已经完成相同级别的验证。

安装步骤和最小会话示例见[OpenVINO 入门](openvino-getting-started.md)。

## 模型格式与动态形状

适配层支持 ONNX 以及 OpenVINO IR（`.xml` 与同目录 `.bin`）加载。动态输入模型在每次推理前必须使用与当前形状一致的张量；当模型包含不受当前 OpenVINO 前端支持的动态算子或辅助输入时，应使用固定形状导出，或选择已验证的 ONNX Runtime 后端。

部署前建议执行以下检查：

1. 在目标 Runtime 中加载模型，并打印输入、输出名称、元素类型与维度。
2. 使用一张真实样例做端到端推理，核对前处理、输出布局与坐标还原。
3. 对动态形状模型分别验证每一种实际输入尺寸，不能只验证首次成功的形状。
4. 为多输入模型显式绑定每个输入，不要依赖输入顺序或名称猜测。

## 常见错误

| 现象 | 优先检查 |
| --- | --- |
| `DS-NATIVE-6001` | Runtime DLL 是否存在、位数是否匹配、托管与原生版本是否兼容。 |
| 模型加载失败 | ONNX/IR 是否完整、`.xml` 与 `.bin` 是否成对、算子是否受当前 Runtime 支持。 |
| 设备创建失败 | 设备名、设备插件与目标硬件；先以 CPU 设备排除模型问题。 |
| 动态形状或辅助输入失败 | 实际输入名、rank、数据类型和具体维度；优先使用固定形状或已验证导出。 |
| 结果不一致 | 检查颜色空间、letterbox、归一化、输出布局及后处理阈值。 |

## 支持边界

OpenVINO 的“可加载”不等于模型任务已通过端到端验证。请使用[模型后端验证矩阵](../model-backend-verification-matrix.md)确认具体模型、任务和后端状态；性能比较请使用统一条件下的[设备性能实测](device-performance-benchmarks.md)。
