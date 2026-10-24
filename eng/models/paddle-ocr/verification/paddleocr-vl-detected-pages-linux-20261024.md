# PaddleOCR-VL：真实版面检测与区域生成联合验证

本记录使用生产 `OpenCvPaddleOcrVlDocumentPipeline`，将 PP-DocLayout-L 的实际检测结果交给 PaddleOCR-VL-1.6。两张独立 FUNSD 测试页共检测到 8 个区域，6 个区域执行 VL、2 个图片区域明确跳过。六个 crop 的全部 **3,960,768 个 float32 Patch 值、653 个 Token 和文本**与相同裁剪的官方参考一致，全部 EOS。

这验证自动检测、单次解码、区域任务选择和真实生成的连接正确性。它不是标注集文档准确率、阅读顺序、结构化归一或正式性能结论。

## 固定资产和运行条件

- 输入清单：[页面 manifest](paddleocr-vl-detected-page-inputs-20261024.json)，SHA-256 `0fa3bdbe1151099c600bf2315660c5a12aec7d5aa48056df3a58d7d93630f0ff`。页面及模型二进制保存在验证目录，不提交到 Git。
- 数据：[FUNSD](https://guillaumejaume.github.io/FUNSD/) test `87147607.png`（771×1000）和 `86220490.png`（754×1000），页码依次为 7、3。根据既有版面记录，在 VL 执行前选择这两页作为规模有限的集成检查；不据此估计数据集精度。
- 检测器：`paddle-doc/pp-doclayout-l`，ONNX SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`；640×640、RGB、1/255、cubic resize、模型空间 `im_shape` 和单位 `scale_factor`，分数阈值 0.3。保留阈值以上全部区域，不使用 top-K 或阅读顺序排序；区域上限为 64。
- VL：固定 `PaddlePaddle/PaddleOCR-VL-1.6` revision `c5630abae1d940eafe0697512a0325494b02ab42`；动态 FP32 `vision/embedding/decoder/head` 四图，图 SHA 及实际程序集 SHA 在[机器报告](paddleocr-vl-detected-pages-linux-20261024.json)中。开发基线为 `fbe21b2` 加本文配套的检测器联动改动；以报告中的实际二进制身份复核。
- 设备：Intel Core i7-1165G7（4 核/8 线程）、16 GB RAM、Ubuntu 22.04.5、.NET 10.0.9、OpenCV 5.0.0、ONNX Runtime 1.28.0 CPU；intra-op=4、inter-op=1，页面并发=1、VL Session=1。
- 生成：greedy、最大 2048 新 Token、每区域超时 15 分钟，图像使用固定官方默认像素参数。官方参考为 CPU FP32、transformers 5.20.2、Pillow 12.3.0、显式动态 KV。

## 实际输出

| 页码 / 文件 | 原始区域索引 | 检测类别 | VL 任务 | Token | 官方对照 / 结束 |
| --- | ---: | --- | --- | ---: | --- |
| 7 / 87147607.png | 0 | table | TableRecognition | 613 | Patch、Token、文本完全一致 / EOS |
| 7 | 1 | number | TextRecognition | 2 | 完全一致 / EOS |
| 7 | 2 | image | 跳过 | — | 不准备 Patch、不生成 |
| 7 | 3 | table_title | TextRecognition | 18 | 完全一致 / EOS |
| 3 / 86220490.png | 0 | image | 跳过 | — | 不准备 Patch、不生成 |
| 3 | 1 | number | TextRecognition | 2 | 完全一致 / EOS |
| 3 | 2 | text | TextRecognition | 9 | 完全一致 / EOS |
| 3 | 3 | aside_text | TextRecognition | 9 | 完全一致 / EOS |

每页均为 `succeeded=3 failed=0 skipped=1`，返回页面顺序保持 7→3。两个图片候选和其余区域的原始索引均保留；`layoutModelRegionIndex` 与检测器输出一致。

官方一致不等于内容正确。例如页 7 的小 `number` 区域生成 `.`；页 3 的 `text` 和 `aside_text` 是重叠候选，两者都生成 `86220490`。正文被版面模型识别为图片时，默认路由会跳过该区域；这直接影响页面覆盖。应用可覆盖图片类为 OCR，但需单独评估重复解析、错误区域和质量，不能把本次一致性当成精度分数。表格结果保留原始 `<fcel>/<lcel>` 等标记，尚未转换为 HTML/单元格对象。

## 裁剪和来源校验

[准备报告](paddleocr-vl-detected-page-preparation-linux-20261024.json)由生产检测器和真实前处理产生，**不运行 VL**；其中没有把占位生成结果计作真实模型证据。每个路由区域保存源框、尺寸绑定的 RGB SHA 和 Patch SHA。

[`Prepare-PaddleOcrVlDetectedReference.py`](../scripts/Prepare-PaddleOcrVlDetectedReference.py)使用独立 Pillow 解码原始 PNG，按生产适配器的 float32 矩形成分、double 边界求和、floor/ceil 和页面 clipping 重建 crop。六个裁剪的尺寸与 RGB 身份全部一致；[crop manifest](paddleocr-vl-detected-crop-inputs-20261024.json)保留原图 SHA、原始索引、浮点/整数边界及独立 PNG SHA。随后用官方 Processor 和权重生成[参考结果](paddleocr-vl-detected-crop-official-reference-20261024.json)。最终 C# 联合执行再次匹配全部 Patch 和 Token/Text，并检查参考 crop 集合没有遗漏或重复。

页面源始终是调用方的 `OpenCvImageSource`；检测与 VL 裁剪复用同一张解码底图，返回对象不会携带已经释放的 Mat。页面输入 SHA 为编码文件 SHA，区域 SHA 为尺寸绑定的 RGB 内容 SHA。源框表示检测器提供的来源，`CoordinatesAvailable=false`，不是 VL 预测的文字坐标。

## 测试与独立示例

最终 `ActualLayoutDetectorAndVlMatchEveryOfficialCropToken` 为 `1 passed / 0 skipped / 0 failed`，约 2 分 10 秒。OpenCV/VL 专项为 `21/21`，包含文件移除后的解码复用、准确辅助几何、实际 Profile、尺寸/像素/区域预算、跳过/失败、取消后复用、有界页面顺序及一页失败后取消并等待其他任务；取消回调异常不会覆盖原始页面错误。OpenCV 库 15 个目标框架 Release 构建、Windows x64 示例构建均零警告/错误。

独立 .NET 8 / Ubuntu22.04 x64 发布目录也实际运行相同页面 manifest：通过 `DOTNET_ROLL_FORWARD=Major` 使用 .NET 10，显式 unset `LD_LIBRARY_PATH`，加载发布目录自带 native。两页均三成功/零失败/一跳过，退出码 0；JSON 的全部 653 Token/文本、源框、输入 SHA、任务及 Markdown 原文与集成报告一致。[示例核对摘要](paddleocr-vl-detected-document-example-linux-20261024.json)保存输出 SHA，且十个托管/native/运行配置文件的本地发布与远端 SHA 一致。

同一发布程序另跑原有的手工区域/固定 `TextRecognition` 模式：将官方 `page-7-region-1` crop 作为页面 42，输出一成功/零失败，两个 Token、原文及 RGB 输入身份与参考一致、EOS、退出码 0。该小范围兼容检查只确认新增检测器分支没有破坏原入口，不作为质量评估。

以上 elapsed 是正确性运行观察，没有 5+50、首 Token、内存或锁频协议，不用于 P50/P95 或速度排名。页面 `Elapsed` 是文档阶段计时，不含外层首次解码。

## 复现入口

将页面 manifest、匹配的 `pp-doclayout-l.onnx` 及两张原始 PNG 放在同一个独立验证目录；按 manifest 的 SHA 校验。准备匹配 ORT/OpenCV native 的测试目录、完整固定官方 checkpoint，以及四图动态 FP32 Bundle。

```bash
export DEPLOYSHARP_PADDLEOCR_VL_DETECTED_MANIFEST=/path/to/inputs/paddleocr-vl-detected-page-inputs-20261024.json
export DEPLOYSHARP_PADDLEOCR_VL_DETECTED_OUTPUT=/path/to/prepared
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:PrepareActualDetectorCropsForIndependentOfficialReference --logger:trx

python /path/to/repo/eng/models/paddle-ocr/scripts/Prepare-PaddleOcrVlDetectedReference.py \
  --pages "$DEPLOYSHARP_PADDLEOCR_VL_DETECTED_MANIFEST" \
  --prepared-report /path/to/prepared/report.json --output /path/to/official-crops

python /path/to/repo/eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlReference.py \
  --model-root /path/to/pinned-checkpoint --manifest /path/to/official-crops/inputs.json \
  --output /path/to/official-generation.json --use-cache --maximum-new-tokens 2048 --threads 4

export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/pinned-checkpoint
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_DETECTED_REFERENCE=/path/to/official-generation.json
export DEPLOYSHARP_PADDLEOCR_VL_DETECTED_OUTPUT=/path/to/generated
dotnet vstest /path/to/DeploySharp.Visual.OpenCV.Tests.dll \
  --Tests:ActualLayoutDetectorAndVlMatchEveryOfficialCropToken --logger:trx
```

应用示例使用相同 manifest，见[自动版面配置和运行](../../../../samples/02-visual/paddleocr-vl-document/README.md#自动检测版面再执行-vl)。该示例支持检测器或手工区域模式；检测器模式要求 `task=Auto`，禁止同时提供手工 `regions`。图/tokenizer 仍未进入可下载的 VL Release Bundle；其它 checkpoint、JPEG Decoder parity、独立标注质量、自动阅读顺序、结构化结果、CUDA/OpenVINO/TensorRT/OpenCV DNN 推理和正式性能保持未完成。
