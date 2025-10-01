using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Configuration class specifically designed for YOLOv9 Segmentation models
    /// 专为YOLOv9分割模型设计的配置类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presets optimal default values for YOLOv9 Segmentation models while allowing customization.
    /// Inherits all YOLO-specific configuration parameters from <see cref="YoloConfig"/>.
    /// </para>
    /// <para>
    /// 为YOLOv9分割模型预设了最优默认值，同时允许自定义配置。
    /// 从<see cref="YoloConfig"/>继承了所有YOLO特定的配置参数。
    /// </para>
    /// <example>
    /// Basic initialization:
    /// <code>
    /// var config = new Yolov9SegConfig("yolov9s-seg.onnx")
    /// {
    ///     TargetInferenceBackend = InferenceBackend.OpenVINO,
    ///     TargetDeviceType = DeviceType.CPU
    /// };
    /// </code>
    /// Advanced initialization:
    /// <code>
    /// var config = new Yolov9SegConfig(
    ///     modelPath: "yolov9s-seg.onnx",
    ///     inferenceBackend: InferenceBackend.OpenVINO,
    ///     deviceType: DeviceType.CPU,
    ///     confidenceThreshold: 0.7f
    /// );
    /// </code>
    /// </example>
    /// </remarks>
    public class Yolov9SegConfig : Yolov8SegConfig
    {
        /// <summary>
        /// Initializes a new instance with default values
        /// 使用默认值初始化新实例
        /// </summary>
        /// <remarks>
        /// The model path must be set separately before use.
        /// 使用前需要单独设置模型路径。
        /// </remarks>
        public Yolov9SegConfig() { }
        /// <summary>
        /// Initializes a new instance with model path and reasonable defaults
        /// 使用模型路径和合理的默认值初始化新实例
        /// </summary>
        /// <param name="modelPath">
        /// Path to the YOLOv9 segmentation model (.onnx/.ir)
        /// YOLOv9分割模型路径 (.onnx/.ir)
        /// </param>
        public Yolov9SegConfig(string modelPath)
        {
            this.ModelType = ModelType.YOLOv9Seg;
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;
            this.ConfidenceThreshold = 0.5f;
            this.NmsThreshold = 0.5f;
            this.InferBatch = 1;
            this.DataProcessor.ResizeMode = ImageResizeMode.Pad;
            this.DataProcessor.NormalizationType = ImageNormalizationType.Scale_0_1;
            NonMaxSuppression = new RectNonMaxSuppression();
        }
        /// <summary>
        /// Fully customizable constructor for advanced configuration
        /// 可完全自定义配置的高级构造函数
        /// </summary>
        /// <param name="modelPath">Path to segmentation model 分割模型路径</param>
        /// <param name="inferenceBackend">Inference backend (default: OpenVINO) 推理后端(默认:OpenVINO)</param>
        /// <param name="deviceType">Target device type (default: CPU) 目标设备类型(默认:CPU)</param>
        /// <param name="confidenceThreshold">Mask confidence threshold (default: 0.5) 掩码置信度阈值(默认:0.5)</param>
        /// <param name="nmsThreshold">NMS overlap threshold (default: 0.5) NMS重叠阈值(默认:0.5)</param>
        /// <param name="inferBatch">Batch size for inference (default: 1) 推理批量大小(默认:1)</param>
        /// <param name="resizeMode">Image resize mode (default: Stretch) 图像缩放模式(默认:拉伸)</param>
        /// <param name="normalizationType">Normalization method (default: Scale_0_1) 标准化方法(默认:0-1缩放)</param>
        public Yolov9SegConfig(string modelPath,
            InferenceBackend inferenceBackend = InferenceBackend.OpenVINO,
            DeviceType deviceType = DeviceType.CPU,
            float confidenceThreshold = 0.5f,
            float nmsThreshold = 0.5f,
            int inferBatch = 1,
            ImageResizeMode resizeMode = ImageResizeMode.Pad,
            ImageNormalizationType normalizationType = ImageNormalizationType.Scale_0_1)
        {
            this.ModelType = ModelType.YOLOv9Seg;
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = inferenceBackend;
            this.TargetDeviceType = deviceType;
            this.ConfidenceThreshold = confidenceThreshold;
            this.NmsThreshold = nmsThreshold;
            this.InferBatch = inferBatch;
            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
            NonMaxSuppression = new RectNonMaxSuppression();
        }
    }
}
