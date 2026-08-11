# Acquire and convert YOLO models / 获取与转换 YOLO 模型

Start from the exact row under `eng/models/yolo/manifests`; YOLO family names do not determine output layout. Keep each acquired checkpoint and conversion under `E:\DeploySharp-Models\<manifest-model-name>`. Record the upstream commit/release, exact checkpoint URI, size/SHA, code and weight licenses, exporter package lock, export arguments, opset, dynamic axes, NMS ownership, class order, and every output port. / 从 `eng/models/yolo/manifests` 的精确条目开始；模型族名不能决定输出布局。每个 checkpoint/转换独立放在统一仓库的模型名目录，并记录完整供应链、导出参数、端口、类别和 NMS 所有权。

For an Ultralytics-supported artifact, create an isolated environment pinned to the manifest exporter version, download the exact official release checkpoint, verify SHA, then export with explicit image size, batch/dynamic setting, opset, simplify, and NMS policy. Inspect the resulting ONNX graph and compare the official predictor on an authorized image before constructing a Profile. Do not reuse these steps for YOLOv6/v7, iMoonLab YOLOv13, or a custom end-to-end export. / 对 Ultralytics 工件可在隔离环境中按 Manifest 固定版本导出；YOLOv6/v7、iMoonLab YOLOv13 或自定义端到端图不得套用相同合同。

```powershell
$name = '<exact-manifest-model-name>'
$root = "E:\DeploySharp-Models\$name"
New-Item -ItemType Directory -Force "$root\original","$root\converted-opset17"
# Download only from the manifest sourceUrl, then verify:
Get-FileHash "$root\original\<checkpoint>" -Algorithm SHA256
# Run the pinned official exporter in a separate temporary checkout.
# Inspect names/types/shapes; never infer Profile selection from the filename.
```

Use `visual-yolo-detection.md`, `visual-yolo-multitask.md`, and the Stage 16/17 manifests for the exact candidate-major, attribute-major, end-to-end, prototype-mask, keypoint, OBB, threshold, and NMS contracts. All current YOLO manifests remain External and non-downloadable pending artifact-specific redistribution review. / 精确输出合同见对应指南和 Manifest；当前全部仍为 External，等待工件级再分发审核。
