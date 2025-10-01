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
    /// Configuration class for YOLO (You Only Look Once) models
    /// YOLO (You Only Look Once) 模型配置类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized configuration container for YOLO family models that extends
    /// base image processing configuration with YOLO-specific parameters.
    /// Implements <see cref="IImgConfig"/> interface ensuring serializability.
    /// </para>
    /// <para>
    /// 专用于YOLO系列模型的配置容器，在基本图像处理配置基础上扩展了YOLO特定参数。
    /// 实现<see cref="IImgConfig"/>接口确保可序列化。
    /// </para>
    /// <example>
    /// Basic YOLOv8 configuration:
    /// <code>
    /// var config = new YoloConfig 
    /// {
    ///     ModelPath = "yolov8n.onnx",
    ///     ConfidenceThreshold = 0.6f,
    ///     NmsThreshold = 0.45f,
    ///     InputSizes = new List<int[]> { new[] {1, 3, 640, 640} }
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class YoloConfig : IImgConfig
    {
        /// <summary>
        /// Confidence threshold for prediction filtering (0-1 range)
        /// 预测结果过滤的置信度阈值（0-1范围）
        /// </summary>
        /// <value>
        /// Predictions with confidence below this value will be discarded.
        /// Default: 0.5f (50% confidence).
        /// 置信度低于此值的预测结果将被丢弃。
        /// 默认值：0.5f (50%置信度)。
        /// </value>
        public float ConfidenceThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Non-Maximum Suppression (NMS) threshold (0-1 range)
        /// 非极大值抑制(NMS)阈值（0-1范围）
        /// </summary>
        /// <value>
        /// Used to eliminate overlapping bounding boxes.
        /// Higher values retain more overlapping detections.
        /// Default: 0.5f.
        /// 用于消除重叠的边界框。
        /// 值越高保留的重叠检测越多。
        /// 默认值：0.5f。
        /// </value>
        public float NmsThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Non-Maximum Suppression algorithm configuration
        /// 非极大值抑制算法配置
        /// </summary>
        /// <value>
        /// Allows customization of the NMS algorithm behavior.
        /// Can specify method type, coefficients and other parameters.
        /// 允许自定义NMS算法行为。
        /// 可以指定方法类型、系数和其他参数。
        /// </value>
        public NonMaxSuppression NonMaxSuppression { get; set; }

        /// <summary>
        /// Generates detailed configuration summary string
        /// 生成详细的配置摘要字符串
        /// </summary>
        /// <returns>
        /// Formatted configuration containing all non-default values
        /// 包含所有非默认值的格式化配置
        /// </returns>
        /// <remarks>
        /// <para>
        /// Only displays properties that have been explicitly set
        /// (different from default values).
        /// Combines output from base class configuration.
        /// </para>
        /// <para>
        /// 仅显示已显式设置的属性（与默认值不同）。
        /// 组合了基类配置的输出。
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            var sb = new StringBuilder();
            AppendIfSet(sb, "Confidence Threshold", ConfidenceThreshold, 0.5f);
            AppendIfSet(sb, "NMS Threshold", NmsThreshold, 0.5f);
            return base.ToString() + sb.ToString();
        }
    }
}
