# PP-Structure 页面与表格随文示例

对应[系列第 06 篇](../../2026-10-29-06-paddle-structure.md)，使用正式 NuGet `2.0.0`，不引用本地源码项目。输入自己的本地模型路径，无须计算或填写模型、图片摘要。

## 环境与包

Windows x64、.NET SDK 10。项目同时保留 ORT CPU 与 OpenVINO CPU 两条路径：

| 包 | 版本 | 职责 |
| --- | --- | --- |
| `JYPPX.DeploySharp.Visual.OpenCV` | 2.0.0 | 图像、OCR 裁剪和文档输入适配 |
| `JYPPX.OpenCV.runtime.win-x64` | 5.0.0 | 图像原生运行时 |
| `JYPPX.DeploySharp.Backend.OnnxRuntime` | 2.0.0 | ORT 后端 |
| `Microsoft.ML.OnnxRuntime` | 1.28.0 | ORT CPU 运行时 |
| `JYPPX.DeploySharp.Backend.OpenVINO` | 2.0.0 | OpenVINO 后端 |
| `OpenVINO.runtime.win` | 2026.2.1 | OpenVINO Windows 运行时 |

只使用 ORT 时，可移除 OpenVINO 两个引用及 `StructureDemo.cs` 中对应 using、分支和注册调用。Linux 需要更换原生运行时包；本例在 Windows 验证。

## 准备模型与输入

从 [models-paddleocr Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) 按需下载并放入 `models/`。

- 页面：`pp-doclayout-l.onnx`、`ppocrv5-mobile-det.onnx`、`ppocrv5-mobile-rec.onnx`、`ppocrv5_dict.txt`。
- 有线表格：`pp-lcnet-x1-0-table-cls.onnx`、`rt-detr-l-wired-cell-det.onnx`、`ppocrv5-mobile-rec.onnx`、`ppocrv5_dict.txt`；ORT 加 `slanext-wired.onnx`，OpenVINO 加 `slanext-wired-openvino-compat.onnx`。
- 无线表格：分类后自动选择 `rt-detr-l-wireless-cell-det.onnx` 与 `slanext-wireless.onnx`；OpenVINO 选 `slanext-wireless-openvino-compat.onnx`。该模型分支未列入本篇实测结果。

准备官方示例图片：

```powershell
New-Item -ItemType Directory -Force images, models | Out-Null
Invoke-WebRequest 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' `
  -OutFile images/pp_structure_v3_demo.png
Invoke-WebRequest 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/table_recognition.jpg' `
  -OutFile images/table_recognition.jpg
```

也可直接传入自己的图片和匹配的本地 ONNX 目录。模型输入、标签和预处理需与示例 Profile 一致；PDF 请先渲染成页面图片。

## 运行

进入本目录执行：

```powershell
dotnet restore
dotnet run -c Release -- page images/pp_structure_v3_demo.png models ort-cpu
dotnet run -c Release --no-build -- table images/table_recognition.jpg models ort-cpu
dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models openvino
dotnet run -c Release --no-build -- table images/table_recognition.jpg models openvino
```

`Ctrl+C` 取消。成功退出码 `0`，取消 `2`，其他失败 `1`。

`page` 输出到 `results/<backend>/page/`：`page.json`、`page-lines.json`、`page.txt`、`layout.jpg`、`page-ocr.jpg`。`table` 输出到 `results/<backend>/table/`：`table.json`、`alignment.json`、`cells.jpg`、`table.html`，成功填充后另存 `cell-texts.json`。同模式重跑会覆盖同名文件。

## 代码职责与边界

- `Program.cs` 处理命令、取消和退出码。
- `StructureDemo.cs` 配置模型、编排阶段、保存结果、绘制框；模型 Session 在本次执行中复用，退出时释放。
- `PageRoiOcrInput.cs` 组合源图坐标中的 ROI 检测输入与共享原图识别视图。避免逐 ROI 编码解码；张量准备和文字行裁剪仍有计算与分配。
- 页面内多行区域使用 DET + REC；几何展示排序不等于完整阅读顺序。`page` 不自动执行表格任务。
- 表格从已裁出的单张表格开始；单行格子使用 REC-only，多行格子需要格内 DET + REC。
- 对齐失败保存结构、检测图、诊断和失败提示 HTML，返回非零退出码。自动对齐不覆盖未验证的跨行 `rowspan`。
- 本例 SLANeXt 有结构框越界警告，保留在 `table.json`；裁剪采用独立 RT-DETR 框。HTML 和文字保留模型误差，不自动改字。

2026-10-29，Windows 11、Ryzen 7 5800H、.NET SDK 10.0.401 上，ORT CPU 与 OpenVINO CPU 均跑通两个输入：页面 31 个版面区域、30 个 OCR 区域、142 行文字；有线表格 4 行、13 格，对齐通过。两后端的 142 行 OCR 字符串与 13 个单元格字符串分别相同；没有断言浮点张量、坐标或置信度逐位一致。本篇未做性能排名或数据集准确率评测，未将结果扩大到其他模型与后端。
