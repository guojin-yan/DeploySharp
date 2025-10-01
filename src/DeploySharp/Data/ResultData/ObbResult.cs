using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents object detection results with oriented bounding boxes (OBB)
    /// 表示带有方向边界框(OBB)的物体检测结果
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized result type for detecting rotated objects where axis-aligned
    /// bounding boxes would be suboptimal. Commonly used in aerial imagery,
    /// document analysis, and scene text detection.
    /// </para>
    /// <para>
    /// 专用于检测旋转物体的结果类型，适用于传统轴对齐边界框效果不佳的场景。
    /// 常用于航拍图像、文档分析和场景文本检测。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var obbResult = new ObbResult {
    ///     Bounds = new RotatedRect(
    ///         center: new PointF(100.5f, 200.5f),
    ///         size: new SizeF(80, 40), 
    ///         angle: 30),
    ///     Confidence = 0.95f,
    ///     Category = "vehicle"
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="DetResult"/>
    /// <seealso cref="ResultType.OrientedBoundingBoxes"/>
    public class ObbResult : Result
    {
        /// <summary>
        /// Oriented bounding box represented as rotated rectangle
        /// 表示为旋转矩形(OBB)的方向边界框
        /// </summary>
        /// <value>
        /// Contains center point, size, and rotation angle (degrees)
        /// 包含中心点、尺寸和旋转角度(度数)
        /// </value>
        public RotatedRect Bounds { get; set; }

        /// <summary>
        /// Initializes a new oriented bounding box result
        /// 初始化一个新的方向边界框结果
        /// </summary>
        /// <remarks>
        /// Automatically sets the <see cref="Result.Type"/> to 
        /// <see cref="ResultType.OrientedBoundingBoxes"/>
        /// 自动将<see cref="Result.Type"/>设置为
        /// <see cref="ResultType.OrientedBoundingBoxes"/>
        /// </remarks>
        public ObbResult()
        {
            Type = ResultType.OrientedBoundingBoxes;
        }

        /// <summary>
        /// Gets the axis-aligned bounding box (AABB) enclosing this OBB
        /// 获取包含此OBB的轴对齐边界框(AABB)
        /// </summary>
        /// <returns>
        /// The smallest axis-aligned rectangle containing all points of the OBB
        /// 包含OBB所有点的最小轴对齐矩形
        /// </returns>
        public Rect GetBoundingRect()
        {
            return Bounds.BoundingRect();
        }

        /// <summary>
        /// Returns formatted string representation including OBB parameters
        /// 返回包含OBB参数的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Combined string with base result info and OBB details
        /// 包含基础结果信息和OBB详情的组合字符串
        /// </returns>
        public override string ToString()
        {
            return $"{base.ToString()}, OBB: [Center: {Bounds.Center}, Size: {Bounds.Size}, Angle: {Bounds.Angle}°]";
        }

        /// <summary>
        /// Creates a deep copy of this OBB result
        /// 创建此OBB结果的深拷贝
        /// </summary>
        public new ObbResult Clone()
        {
            return new ObbResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Category = Category,
                Bounds = Bounds
            };
        }
    }

}
