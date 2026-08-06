# Visual YOLO detection package-only consumer

This consumer has no project references. It installs DeploySharp Core, Visual, Visual.OpenCV, the ONNX Runtime backend, and the application-selected native runtimes from NuGet packages.

Set `DEPLOYSHARP_YOLO_MODEL` to the audited YOLOv8n ONNX file with SHA256 `50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4`, and set `DEPLOYSHARP_YOLO_IMAGE` to an external validation image. A successful real inference prints `DEPLOYSHARP_VISUAL_YOLO_DETECTION_CONSUMER_OK`.

This consumer does not download or redistribute either asset. / 本 consumer 不下载或再分发模型与图片；两者均通过环境变量显式提供。
