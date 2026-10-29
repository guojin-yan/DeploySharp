# PP-Structure 页面与表格随文示例

对应[系列第 06 篇](../../2026-10-29-06-paddle-structure.md)，使用正式 NuGet `2.0.0`，不引用本地源码项目。输入自己的本地模型路径，无须计算或填写模型、图片摘要。

## 环境与包

Windows x64 或 Ubuntu 22.04 x64、.NET SDK 10。相同 C# 源码同时保留 ORT CPU 与 OpenVINO CPU 两条路径，`.csproj` 按当前系统选择原生包：

| 包 | 版本 | 职责 |
| --- | --- | --- |
| `JYPPX.DeploySharp.Visual.OpenCV` | 2.0.0 | 图像、OCR 裁剪和文档输入适配 |
| `JYPPX.OpenCV.runtime.win-x64` | 5.0.0 | Windows 图像原生运行时 |
| `JYPPX.OpenCV.runtime.ubuntu.22.04-x64` | 5.0.0 | Ubuntu 22.04 图像原生运行时 |
| `JYPPX.DeploySharp.Backend.OnnxRuntime` | 2.0.0 | ORT 后端 |
| `Microsoft.ML.OnnxRuntime` | 1.28.0 | ORT CPU 运行时 |
| `JYPPX.DeploySharp.Backend.OpenVINO` | 2.0.0 | OpenVINO 后端 |
| `OpenVINO.runtime.win` | 2026.2.1 | OpenVINO Windows 运行时 |
| `OpenVINO.runtime.ubuntu.22-x86_64` | 2026.3.1 | OpenVINO Ubuntu 22.04 运行时 |

Windows 和 Ubuntu 的原生包按条件分别引用，不要求同时安装两套系统的运行时。显式传入 `RuntimeIdentifier` 时仅配置 `win-x64` / `linux-x64`；这里的 Linux 包面向 Ubuntu 22.04，不扩大为所有 Linux 发行版支持。

只使用 ORT 时，可移除 OpenVINO 后端、对应系统的运行时引用及 `StructureDemo.cs` 中对应 using、分支和注册调用，并删除 Linux OpenVINO 原生文件复制项。完整工程已将 Linux 原生文件复制到输出目录的 `native/opencv` 与 `native/openvino`，不要仅添加包而遗漏 `.csproj` 中的复制配置。

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

Ubuntu Bash：

```bash
mkdir -p images models
curl -fL 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' \
  -o images/pp_structure_v3_demo.png
curl -fL 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/table_recognition.jpg' \
  -o images/table_recognition.jpg
```

也可直接传入自己的图片和匹配的本地 ONNX 目录。模型输入、标签和预处理需与示例 Profile 一致；PDF 请先渲染成页面图片。

## 运行

Windows PowerShell，进入本目录执行：

```powershell
dotnet restore
dotnet run -c Release -- page images/pp_structure_v3_demo.png models ort-cpu
dotnet run -c Release --no-build -- table images/table_recognition.jpg models ort-cpu
dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models openvino
dotnet run -c Release --no-build -- table images/table_recognition.jpg models openvino
```

Ubuntu Bash，进入同一份工程目录，构建后设置当前进程的原生库搜索路径：

```bash
dotnet restore
dotnet build -c Release --no-restore
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"

dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models ort-cpu
dotnet run -c Release --no-build -- table images/table_recognition.jpg models ort-cpu
dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models openvino
dotnet run -c Release --no-build -- table images/table_recognition.jpg models openvino
```

按用户目录安装 SDK 且找不到 `dotnet` 时，先执行 `export PATH="$HOME/.dotnet:$PATH"`。改变构建目录或部署到服务后，相应调整原生库搜索路径。

Ubuntu 构建可能出现 `NETSDK1206`：.NET 8+ 不默认选择包中的 `ubuntu.22.04-x64` 发行版 RID。本例通过显式复制原生文件完成部署；本次保留该警告并成功执行四条路径。若仍提示无法加载 `.so`，检查输出目录和 `LD_LIBRARY_PATH`，再用 `ldd` 查找缺失依赖。

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

## 本次实测

2026-10-29，以下两个系统、两种 CPU 后端均运行了 `page` 和 `table`：

| 系统 | 设备与 .NET SDK | 后端 |
| --- | --- | --- |
| Windows 11 x64 | Ryzen 7 5800H；SDK 10.0.401 | ORT CPU 1.28.0；OpenVINO CPU 2026.2.1 |
| Ubuntu 22.04.5 LTS x86_64 | Core i7-1165G7；SDK 10.0.301 | ORT CPU 1.28.0；OpenVINO CPU 2026.3.1 |

四组结果均为：页面 31 个版面区域、30 个 OCR 区域、142 行文字；有线表格 4 行、13 格，对齐通过并填充 HTML。142 行 OCR 字符串与 13 个单元格字符串均与 Windows ORT CPU 基线逐项相同；没有断言浮点张量、坐标或置信度逐位一致。结构框越界警告在四组中均保留。本篇未做性能排名或数据集准确率评测，未将结果扩大到其他模型与后端。
