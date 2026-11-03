# RF-DETR 1.11.2 本地模型交付

当前源码新增 Keypoint Preview 内置支持，并补齐 Seg Nano 实例分割。上游固定为 [RF-DETR 1.11.2](https://github.com/roboflow/rf-detr/tree/7433d5919d0f7e72cc6cb78663e0a731afd499e0)。本目录提供可复用的官方权重下载、ONNX 导出、官方参考结果和本地 ModelPack 生成工具。没有上传模型 Release 或发布 NuGet；新模型 ID 尚不在 ModelFactory 下载目录中。

## 导出

从 Git 仓库根目录运行。Python 3.10+；建议使用独立环境，下面以 CPU PyTorch 生成参考结果。`.venv-rfdetr` 与模型放在仓库外：

```powershell
python -m venv ../.venv-rfdetr
& ../.venv-rfdetr/Scripts/python.exe -m pip install torch==2.10.0 torchvision==0.25.0 --index-url https://download.pytorch.org/whl/cpu
& ../.venv-rfdetr/Scripts/python.exe -m pip install -r eng/models/rf-detr/requirements.txt

& ../.venv-rfdetr/Scripts/python.exe eng/models/rf-detr/export_rfdetr.py --task keypoint --output ../models/rf-detr-1.11.2/keypoint
& ../.venv-rfdetr/Scripts/python.exe eng/models/rf-detr/export_rfdetr.py --task segment --variant nano --output ../models/rf-detr-1.11.2/segment-nano
```

未指定 `--image` 时，工具从现有测试资产目录下载并校验 `bus.jpg`。可传入 `--image <图片>`、`--weights <同合同的本地权重>`、`--threshold 0.4`。解码后按 EXIF 方向归正，与现有 OpenCV 图像读入约定一致，再交给官方张量前处理。默认关键点和 Nano 权重使用 [weights.json](weights.json) 中真实的官方 URL、大小及 SHA-256；已有缓存也会重新校验。替换权重会生成新的工件身份和参考，不能沿用本仓库基线的后端结论。工具采用官方类别和任务结构；微调后标签或结构不同，应提供相应的自定义 Profile 与导出配置，不会自动推断。

`--variant small|medium|large|xlarge|2xlarge` 可调用同版本官方分割导出器，这些变体使用上游下载逻辑，尚未在本次后端矩阵中验证。关键点仅支持 Preview 的固定 COCO-17 合同；其它关键点模式需要自定义 Profile。

每个输出目录包含：

| 文件 | 用途 |
| --- | --- |
| `model.onnx` | opset 17、固定 Batch=1 的单文件模型 |
| `rfdetr.json` | 模型大小/SHA-256、标签、精确端口、输入尺寸、测试图身份和依赖版本 |
| `modelpack.json`、`LICENSE.rf-detr.txt` | 可由 `ModelPackageLoader.Load` 校验的本地模型包，包含 ONNX、配置与许可证的大小和哈希 |
| `input.f32`、各输出 `.f32` | 官方浮点输入与 ORT CPU 原始张量基线，小端 float32 |
| `reference.json`、分割的 `reference-masks.u8` | 官方 PyTorch `PostProcess` 参考结果；mask 为源图大小、实例优先、0/1 字节 |
| `weights/`、`test-images/` | 默认下载缓存 |

只有前三项部署文件和许可证是推理必需内容；参考张量用于开发验证，不需要随应用分发。新导出记录 `local-export-unverified`，不会自动声称所有后端已验证。完整执行案例见 [RF-DETR 样例](../../../samples/06-models/rf-detr/README.md)，精确结果见[后端矩阵](../../../docs/model-backend-verification-matrix.md#rf-detr-1112-本地工件)。

已有 ONNX 和 `rfdetr.json` 时，可重新生成包而不重新导出：

```powershell
& ../.venv-rfdetr/Scripts/python.exe eng/models/rf-detr/export_rfdetr.py --task keypoint --output ../models/rf-detr-1.11.2/keypoint --package-only
```

## 基线工件与旧模型

[contracts/keypoint-preview](contracts/keypoint-preview/rfdetr.json) 和 [contracts/segment-nano](contracts/segment-nano/rfdetr.json) 保留本次实测的精确元数据及 ModelPack。大模型和参考输出不入库。导出环境变化可能改变 ONNX 字节；实际部署始终使用同一次导出产生的配置和 ModelPack。

已发布 `rf-detr/segment` 是 432×432、200 Query、mask 端口 `4245` 的另一份工件。它的本地推理配置为 [legacy-segment.json](legacy-segment.json)，标签沿用原始合同的 `class0..class89` 与末尾 no-object 列，不能与新 Nano 的 91 列稀疏 COCO 标签互换。旧模型的独立运行命令见[原案例](../../../samples/06-models/cases/rf-detr--segment/README.md)。现有 Release 字节与目录未改写。
