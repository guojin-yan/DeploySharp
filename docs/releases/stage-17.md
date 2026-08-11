# Stage 17 release note / 阶段 17 发布说明

Stage 17 completes the YOLO classification, instance-segmentation, Pose, and OBB model family slice in the existing Visual assembly. It closes twelve remaining V1 model rows with exact ONNX profiles and real CPU evidence on ONNX Runtime and OpenVINO, while keeping OpenCV behind `Visual.OpenCV` and keeping the official ModelFactory catalog empty. / 阶段 17 在现有 Visual 程序集中完成 YOLO 分类、实例分割、Pose 与 OBB 模型族切片。它用精确 ONNX Profile 和 ONNX Runtime/OpenVINO 真实 CPU 证据闭合 V1 剩余十二个模型行，同时保持 OpenCV 位于 `Visual.OpenCV`，并保持 ModelFactory 官方目录为空。

The package uses the repository English README and `nuget/logo.jpg`. No GitHub Release, tag, model asset, or GitHub Actions workflow was written by this stage. TensorRT remains outside the implementation until its API package is ready for verification. / 包使用仓库英文 README 和 `nuget/logo.jpg`。本阶段未写入 GitHub Release、tag、模型资产或 GitHub Actions 工作流。TensorRT 在其 API 包具备可核验版本前仍不实现。

The four-task OpenVINO IR conversion gate is documented as pending because the current Windows environment has no verified OVC/Model Optimizer converter. ONNX CPU evidence is complete for both ORT and OpenVINO; no pseudo-IR is claimed. / 四类任务 OpenVINO IR 转换门禁因当前 Windows 环境没有可核验的 OVC/Model Optimizer 转换器而记录为待完成。ORT 与 OpenVINO 的 ONNX CPU 证据已完成，不宣称伪造 IR。
