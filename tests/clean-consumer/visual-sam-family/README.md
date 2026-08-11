# DeploySharp SAM-family clean consumer

This package-only consumer installs Core, Visual, Visual.OpenCV, one backend adapter, and application-selected Windows x64 native runtimes. It contains no project references, checkpoint, ONNX, image, Python, OpenVINO IR, video, or TensorRT asset. / 此仅包消费者只安装 Core、Visual、Visual.OpenCV、一个后端适配器与应用显式选择的 Windows x64 native runtime；不含项目引用、checkpoint、ONNX、图片、Python、IR、视频或 TensorRT 资产。

Set `DEPLOYSHARP_SAM_ENCODER_ONNX`, `DEPLOYSHARP_SAM_DECODER_ONNX`, and `DEPLOYSHARP_SAM_IMAGE`. A missing file prints `DEPLOYSHARP_VISUAL_SAM_FAMILY_CONSUMER_SKIP`; a real ORT CPU point + box + mask-feedback run prints `DEPLOYSHARP_VISUAL_SAM_FAMILY_CONSUMER_OK`. / 设置三个外部文件环境变量。缺文件时输出稳定 skip；真实 ORT CPU point + box + mask-feedback 成功后输出成功标记。
