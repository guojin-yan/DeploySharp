# DeploySharp RT-DETR clean consumer

This package-only sample installs Core, Visual, Visual.OpenCV, one backend adapter, and application-selected Windows x64 native runtimes. It contains no project references, models, images, or TensorRT assets. / 此仅包消费者只安装 Core、Visual、Visual.OpenCV、一个后端适配器及应用选择的 Windows x64 native runtime；不含项目引用、模型、图片或 TensorRT 资产。

Set `DEPLOYSHARP_RTDETR_ONNX` to the audited decoded vector-count ONNX and `DEPLOYSHARP_RTDETR_IMAGE` to an external image. Missing files print `DEPLOYSHARP_VISUAL_RTDETR_CONSUMER_SKIP`; a real ORT CPU result prints `DEPLOYSHARP_VISUAL_RTDETR_CONSUMER_OK`. / 环境变量指向已审核的外部 ONNX 与图片。缺少文件时输出稳定 skip；真实 ORT CPU 成功后输出成功标记。
