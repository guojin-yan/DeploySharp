# 智能文档工作台：混合资料解析、分组归档与原图复核

基于正式 NuGet `2.0.0`，将方向校正、版面检测、区域 OCR、表格与公式组合为完整应用。默认处理 9 张公开示例图、4 组资料，输出可搜索 HTML、分组 Markdown、字段候选和复核清单。

## 环境与依赖

.NET 10 SDK、x64。工程自动选择 Windows x64 或 Ubuntu 22.04 x64 的原生包。

| 依赖 | 版本 / 用途 |
| --- | --- |
| `JYPPX.DeploySharp.Visual.OpenCV` | 2.0.0，图像与 ROI |
| `JYPPX.DeploySharp.Backend.OnnxRuntime` | 2.0.0，ORT 后端 |
| `Microsoft.ML.OnnxRuntime` | 1.28.0，CPU 推理 |
| `JYPPX.DeploySharp.Backend.OpenVINO` | 2.0.0，OpenVINO 后端 |
| `JYPPX.OpenCV.runtime.win-x64` | 5.0.0，Windows 图像运行时 |
| `JYPPX.OpenCV.runtime.ubuntu.22.04-x64` | 5.0.0，Ubuntu 图像运行时 |
| `OpenVINO.runtime.win` | 2026.2.1，Windows OpenVINO |
| `OpenVINO.runtime.ubuntu.22-x86_64` | 2026.3.1，Ubuntu OpenVINO |

`--backend ort-cpu` 对全部任务使用 ORT CPU；`--backend openvino` 对方向、版面、OCR、表格使用 OpenVINO CPU，**公式固定使用 ORT CPU**。因此完整工程保留两套后端；只用 ORT 的工程可删除 OpenVINO 引用、原生复制项和注册分支。

## 下载模型

在本工程目录执行。已有文件可以复制到 `models/`，无需检查模型或图片的散列。

Windows PowerShell：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
$release = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
$files = @(
  'pp-lcnet-x1-0-doc-ori.onnx','pp-doclayout-l.onnx',
  'ppocrv5-mobile-det.onnx','ppocrv5-mobile-rec.onnx','ppocrv5_dict.txt',
  'pp-lcnet-x1-0-table-cls.onnx',
  'rt-detr-l-wired-cell-det.onnx','rt-detr-l-wireless-cell-det.onnx',
  'slanext-wired.onnx','slanext-wireless.onnx',
  'slanext-wired-openvino-compat.onnx','slanext-wireless-openvino-compat.onnx',
  'pp-formulanet-plus-s.onnx'
)
foreach ($file in $files) {
  Invoke-WebRequest "$release/$file" -OutFile "models/$file"
}
Invoke-WebRequest 'https://huggingface.co/PaddlePaddle/PP-FormulaNet_plus-S/resolve/main/inference.yml' -OutFile 'models/pp-formulanet-plus-s.inference.yml'
```

Ubuntu Bash：

```bash
mkdir -p models
release='https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
for file in pp-lcnet-x1-0-doc-ori.onnx pp-doclayout-l.onnx \
  ppocrv5-mobile-det.onnx ppocrv5-mobile-rec.onnx ppocrv5_dict.txt \
  pp-lcnet-x1-0-table-cls.onnx \
  rt-detr-l-wired-cell-det.onnx rt-detr-l-wireless-cell-det.onnx \
  slanext-wired.onnx slanext-wireless.onnx \
  slanext-wired-openvino-compat.onnx slanext-wireless-openvino-compat.onnx \
  pp-formulanet-plus-s.onnx; do
  curl -fL "$release/$file" -o "models/$file"
done
curl -fL 'https://huggingface.co/PaddlePaddle/PP-FormulaNet_plus-S/resolve/main/inference.yml' \
  -o models/pp-formulanet-plus-s.inference.yml
```

ORT 用原始 SLANeXt，OpenVINO 用 `-openvino-compat.onnx`，只跑 ORT 时可以省去兼容图。公式 YAML 来自官方 Hugging Face 配置，用来创建 tokenizer；公式的原始 ONNX 在本例通过 ORT 运行。无线表格的分类分支已经接入，默认图片中是否完成结构对齐以实际结果为准。

## 运行

Windows：

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --backend ort-cpu --manifest documents.json
dotnet run -c Release --no-build -- --backend openvino --manifest documents.json
```

Ubuntu：

```bash
dotnet restore
dotnet build -c Release
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:$LD_LIBRARY_PATH"
dotnet bin/Release/net10.0/DocumentArchive.dll --backend ort-cpu --manifest documents.json
dotnet bin/Release/net10.0/DocumentArchive.dll --backend openvino --manifest documents.json
```

Ubuntu 首次构建可能出现 `NETSDK1206` 旧 RID 警告；工程显式复制原生文件，上面的搜索路径指向这些文件。

参数：`--models models`、`--manifest documents.json`、`--out results`、`--backend ort-cpu|openvino`。打开 `results/<backend>/index.html`，可按文档组、状态和关键词筛选；展开区域并点击文字或区域按钮，在原始方向的图片中定位。

退出码：`0` 全部流程完成，`1` 有失败页，`2` 用户取消，`3` 有待复核结果。默认 9 张图返回 `3`，结果仍完整保存。单页或表格/公式任务异常保留诊断并继续，`Ctrl+C` 取消批次。

## 配置自己的输入

`documents.json` 的相对图片路径从清单文件目录解析。1—100 页，页面 `Id` 唯一；`Id/DocumentId` 只用小写字母、数字和短横线，长度不超过 64。

| 字段 | 含义 |
| --- | --- |
| `DocumentId` | 分组标识，相同组导出同一份 Markdown |
| `Kind` | `auto` 版面分流；`text/table/formula` 直接执行整图任务 |
| `AutoOrient` | 默认 true，方向分数达到 0.8 才校正，否则保留方向并记录复核 |
| `CellTextMode` | `rec` 批量单格识别；`lines` 每格 DET + REC 后拼接多行 |
| `MinimumTextConfidence` | 批次级阈值，默认 0.8；空文字或低分结果进入复核 |

默认报刊、复杂版面和论文走自动分流；公式专页直接识别；倒置页自动校正后 OCR；两张现场图和印章走普通 OCR。费用表使用格内多行 OCR。印章输入用于检查曲线文字的失败路径，不是专用印章流水线。

## 输出

| 位置 | 内容 |
| --- | --- |
| `archive.json` | 批次、页面、区域、文字、公式、双坐标、后端与状态 |
| `review.json/review.csv` | 复核原因、页面/区域/行索引、文字及分数；JSON 另含原图多边形 |
| `fields.json/fields.csv` | 日期/两位小数正则候选；JSON 另含原图位置 |
| `documents/*.md` | 按资料组汇总文字、公式与表格链接 |
| `<page>/source.png` | OpenCV 解码、处理 EXIF 后的原始方向图片 |
| `<page>/upright.png/regions.jpg` | 正向页、正向页结果框 |
| `<page>/page.json/page.txt` | 页结果；TXT 包含 OCR 和表格文字 |
| `<page>/table-XX/` | 裁图、结构/对齐 JSON、检测图、HTML；成功时另有 cells.json |
| `<page>/formula-XX/` | 裁图、formula.tex、formula.json |

`page.json/archive.json` 中 `Polygon` 属于正向页，`SourcePolygon` 属于 `source.png`；`cells.json` 中格子坐标属于正向页。映射使用连续像素边界，支持 0/90/180/270 度。同一输出目录重跑会更新对应文件，建议每批使用新的 `--out`。

模型会话按后端和文件复用；页内 ROI 共享解码图像。方向阶段、正向页准备和预览仍有独立图像读写，不能把整个应用当成零拷贝。

## 实际结果和边界

Windows 11 x64 与 Ubuntu 22.04.5 x64 各运行 ORT CPU、OpenVINO 主流程 + ORT 公式，两种路径均完成默认 9 页：

- 4 组、72 区域、324 条文字行/单元格结果（含 13 格）。
- 费用表填字 HTML 1 份；复杂版面两张表格保存诊断 HTML，分别为格数不匹配和结构未完成。
- 7 份公式产物，论文的一处公式触发花括号不平衡；基础语法检查不保证公式内容正确。
- 倒置页校正 180°，识别 5 行文字；复核清单 17 条，小数候选 3 条。
- 5 页待复核、4 页流程完成；空文字和低分印章结果没有手工修正。

日期/小数是规则候选，不是语义字段抽取。图片没有人工准确率标注。当前不含 PDF 渲染、完整多栏阅读顺序、跨页表格合并、`rowspan` 单元格对齐和人工结果回写；这些能力可在现有页码、坐标和结构记录上扩展。
