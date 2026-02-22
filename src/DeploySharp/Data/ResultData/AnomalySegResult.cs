using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents anomaly segmentation results including raw anomaly mask data
    /// 表示异常分割结果，包含原始异常掩码数据
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="SegResult"/> with additional raw mask capability for industrial anomaly detection.
    /// Used for pixel-level anomaly detection tasks where precise anomaly localization is required.
    /// </para>
    /// <para>
    /// 继承自<see cref="SegResult"/>并增加了原始掩码功能，用于工业异常检测。
    /// 用于需要精确异常定位的像素级异常检测任务。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var anomalyResult = new AnomalySegResult 
    /// {
    ///     Bounds = new Rect(100, 150, 200, 200),
    ///     Mask = new ImageDataF(width: 200, height: 200, channels: 1),
    ///     RawMask = new ImageDataF(width: 200, height: 200, channels: 1),
    ///     Confidence = 0.92f,
    ///     Category = "defect"
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="SegResult"/>
    /// <seealso cref="ResultType.AnomalySegmentation"/>
    public class AnomalySegResult : SegResult
    {
        /// <summary>
        /// Raw anomaly mask before thresholding, containing continuous anomaly scores
        /// 阈值处理前的原始异常掩码，包含连续的异常分数
        /// </summary>
        /// <value>
        /// <para>
        /// Each pixel value represents the anomaly score (typically 0-1 range).
        /// Higher values indicate higher likelihood of anomaly.
        /// </para>
        /// <para>
        /// 每个像素值表示异常分数（通常为0-1范围）。
        /// 较高的值表示异常可能性更大。
        /// </para>
        /// </value>
        /// <remarks>
        /// Unlike <see cref="SegResult.Mask"/> which may be binary after thresholding,
        /// this property preserves the raw anomaly scores for further analysis.
        /// 与<see cref="SegResult.Mask"/>（可能是二值化的）不同，
        /// 此属性保留原始异常分数以供进一步分析。
        /// </remarks>
        public ImageDataF RawMask { get; set; }

        /// <summary>
        /// Initializes a new anomaly segmentation result with proper type configuration
        /// 初始化一个新的异常分割结果，自动配置正确的结果类型
        /// </summary>
        /// <remarks>
        /// Automatically sets <see cref="Result.Type"/> to <see cref="ResultType.AnomalySegmentation"/>
        /// 自动将<see cref="Result.Type"/>设置为<see cref="ResultType.AnomalySegmentation"/>
        /// </remarks>
        public AnomalySegResult()
        {
            Type = ResultType.AnomalySegmentation;
        }

        /// <summary>
        /// Creates a deep copy of this anomaly segmentation result
        /// 创建此异常分割结果的深拷贝
        /// </summary>
        /// <returns>
        /// A new <see cref="AnomalySegResult"/> with copied properties and cloned masks
        /// 包含复制属性和克隆掩码的新<see cref="AnomalySegResult"/>对象
        /// </returns>
        public new AnomalySegResult Clone()
        {
            return new AnomalySegResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Category = Category,
                Bounds = Bounds,
                Mask = (ImageDataF)(Mask?.Clone()),
                RawMask = (ImageDataF)(RawMask?.Clone())
            };
        }
    }
}
