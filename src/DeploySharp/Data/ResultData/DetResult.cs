using System;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents object detection results with bounding box information
    /// 表示包含边界框信息的物体检测结果
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends base Result class with spatial localization capabilities
    /// through bounding box coordinates.
    /// </para>
    /// <para>
    /// 通过边界框坐标扩展基础Result类的空间定位能力。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var detection = new DetResult {
    ///     Id = 42,
    ///     Category = "person",
    ///     Confidence = 0.87f,
    ///     Bounds = new Rect(100, 200, 50, 80)
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="Result"/>
    /// <seealso cref="ResultType.Detectiony"/>
    public class DetResult : Result
    {
        /// <summary>
        /// Bounding box coordinates relative to source image
        /// 相对于源图像的边界框坐标
        /// </summary>
        /// <value>
        /// Rectangle represented as (X, Y, Width, Height)
        /// 表示为(X, Y, Width, Height)的矩形
        /// </value>
        public Rect Bounds { get; set; }

        /// <summary>
        /// Initializes a new detection result with proper type configuration
        /// 初始化一个新的检测结果，自动配置正确的结果类型
        /// </summary>
        /// <remarks>
        /// Automatically sets ResultType to Detection
        /// 自动将ResultType设置为Detection
        /// </remarks>
        public DetResult()
        {
            Type = ResultType.Detection;
        }

        /// <summary>
        /// Returns formatted string representation including bounding box
        /// 返回包含边界框信息的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Combined string with base result info and bounding box coordinates
        /// 包含基础结果信息和边界框坐标的组合字符串
        /// </returns>
        public override string ToString()
        {
            return $"{base.ToString()}, Bounds: {Bounds}";
        }

        /// <summary>
        /// Creates a deep copy of this detection result
        /// 创建此检测结果的深拷贝
        /// </summary>
        public new DetResult Clone()
        {
            return new DetResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Bounds = Bounds,
                Category = Category
            };
        }
    }

}
