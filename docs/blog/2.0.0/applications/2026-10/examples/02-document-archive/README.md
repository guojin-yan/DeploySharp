# 文档结构化归档

对应[应用系列第 02 篇](../../2026-10-29-02-document-archive.md)。使用正式 NuGet 2.0.0，将版面、OCR、表格分类、单元格和 SLANeXt 组合为多页归档程序，生成可搜索的静态 HTML、逐页文字、表格 HTML 和整页坐标 JSON。

## 环境与依赖

.NET 10 SDK、x64；`.csproj` 按 Windows 或 Ubuntu 22.04 x64 选原生库。

| 职责 | 包 |
| --- | --- |
| 图像与视觉接口 | `JYPPX.DeploySharp.Visual.OpenCV 2.0.0` |
| ORT CPU | `JYPPX.DeploySharp.Backend.OnnxRuntime 2.0.0`、`Microsoft.ML.OnnxRuntime 1.28.0` |
| OpenVINO | `JYPPX.DeploySharp.Backend.OpenVINO 2.0.0` |
| Windows 原生库 | `JYPPX.OpenCV.runtime.win-x64 5.0.0`、`OpenVINO.runtime.win 2026.2.1` |
| Ubuntu 原生库 | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0`、`OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1` |

只保留 ORT 时，同时移除 OpenVINO 包、原生库复制项和 Program 注册分支。完整工程无需调整，直接还原。

## 下载模型

两张图片随工程提供，来源见 [demo-inputs](demo-inputs/README.md)。默认任务所需模型按文件下载：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
$release = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
$files = @('pp-doclayout-l.onnx','ppocrv5-mobile-det.onnx','ppocrv5-mobile-rec.onnx','ppocrv5_dict.txt',
  'pp-lcnet-x1-0-table-cls.onnx','rt-detr-l-wired-cell-det.onnx','slanext-wired.onnx','slanext-wired-openvino-compat.onnx')
foreach ($file in $files) { Invoke-WebRequest "$release/$file" -OutFile "models/$file" }
```

Ubuntu：

```bash
mkdir -p models
release='https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
for file in pp-doclayout-l.onnx ppocrv5-mobile-det.onnx ppocrv5-mobile-rec.onnx ppocrv5_dict.txt \
  pp-lcnet-x1-0-table-cls.onnx rt-detr-l-wired-cell-det.onnx slanext-wired.onnx slanext-wired-openvino-compat.onnx; do
  curl -fL "$release/$file" -o "models/$file"
done
```

ORT 用原始 SLANeXt，OpenVINO 用 `-openvino-compat.onnx`，只跑一个后端时可以省去另一个文件。无线表格需另外下载 `rt-detr-l-wireless-cell-det.onnx` 和相应的 `slanext-wireless[增补 -openvino-compat].onnx`，程序按分类选择。不要用符号链接代替单个 ONNX 文件。

## 运行

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --backend ort-cpu --manifest documents.json
dotnet run -c Release --no-build -- --backend openvino --manifest documents.json
```

Ubuntu 在构建后执行：

首次构建可能出现 `NETSDK1206`（旧 Ubuntu RID）警告；工程已显式复制原生文件，下面的搜索路径指向这些文件。

```bash
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/DocumentArchive.dll --backend ort-cpu --manifest documents.json
dotnet bin/Release/net10.0/DocumentArchive.dll --backend openvino --manifest documents.json
```

其他参数：`--models models`、`--out results`。打开 `results/<backend>/index.html`。正常 `0`、运行失败 `1`、取消 `2`、表格需要复核 `3`。单页失败保留诊断，继续处理其他页；`Ctrl+C` 结束整个任务。

## 修改自己的任务

`documents.json` 中 `Pages` 按输入顺序定义 `Id/Title/Image/Kind`。ID 唯一，只用小写字母、数字和短横线。相对图片路径从配置文件目录解析。

- `Kind: auto` 先运行版面模型，再按类别分流文字、表格与其他区域。
- `Kind: table` 将整张输入视为已裁好的表格，跳过版面模型。

会话按模型文件缓存，多页复用。每页只在推理入口解码一次，ROI 前处理共享像素；保存预览另读取原图。表格和文字框保留整页坐标，结构与单元格按同页来源对齐。

## 输出与限制

`archive.json` 是整批索引；各页的 `page.json/page.txt/regions.jpg/source.jpg` 保存可复核结果。每个表格保存 `crop.png/structure.json/cells.jpg/cells.json/table.html`；对齐失败时保留结构与诊断，跳过文字填充。

默认两页在 Windows 与 Ubuntu 的 ORT、OpenVINO CPU 均完成：32 个版面区域、142 行区域 OCR、1 张表格的 13 个单元格。SLANeXt 结构框的越界诊断仍保留，裁剪用独立单元格框。阅读顺序、跨页表格合并、PDF 渲染和人工审核回写由应用扩展；当前单元格对齐不支持 `rowspan`。
