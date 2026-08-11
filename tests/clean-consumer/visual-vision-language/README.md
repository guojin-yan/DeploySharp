# DeploySharp vision-language clean consumer

This package-only consumer installs Core, Visual, Visual.OpenCV, one ONNX Runtime backend adapter, and application-selected Windows x64 native runtimes. It has no project reference, model, checkpoint, tokenizer asset, image, Python, OpenVINO IR, TensorRT, or native runtime hidden in a DeploySharp package. / 此仅包消费者安装 Core、Visual、Visual.OpenCV、一个 ONNX Runtime Backend Adapter 与应用显式选择的 Windows x64 Native Runtime；不包含项目引用、模型、Checkpoint、Tokenizer 资产、图片、Python、OpenVINO IR、TensorRT 或由 DeploySharp 包隐式携带的 Native Runtime。

Set `DEPLOYSHARP_VLM_CLIP_IMAGE_ONNX`, `DEPLOYSHARP_VLM_CLIP_TEXT_ONNX`, and `DEPLOYSHARP_VLM_IMAGE`. Missing files print `DEPLOYSHARP_VISUAL_VLM_EMBEDDING_CONSUMER_SKIP`; a real CLIP image/text encode plus zero-shot classification and retrieval prints `DEPLOYSHARP_VISUAL_VLM_EMBEDDING_CONSUMER_OK`. / 设置三个外部文件环境变量。缺文件时输出稳定 Skip；真实 CLIP 图像/文本编码、零样本分类与检索成功后输出成功标记。
