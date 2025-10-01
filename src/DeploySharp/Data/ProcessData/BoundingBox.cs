using System;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a bounding box with confidence score, spatial coordinates, and metadata
    /// 表示具有置信度分数、空间坐标和元数据的边界框
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for object detection results, containing detection information and spatial coordinates.
    /// Implements IComparable for sorting by confidence score (descending order).
    /// </para>
    /// <para>
    /// 用于物体检测结果，包含检测信息和空间坐标。
    /// 实现了IComparable接口以支持按置信度排序（降序）。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var box = new BoundingBox {
    ///     Confidence = 0.95f,
    ///     Box = new RectF(10.5f, 20.5f, 100f, 150f),
    ///     Index = 1,
    ///     NameIndex = 42
    /// };
    /// 
    /// // Sorting boxes
    /// List&lt;BoundingBox&gt; boxes = GetDetections();
    /// boxes.Sort(); // Sorts by confidence descending
    /// </code>
    /// </example>
    /// </remarks>
    public class BoundingBox : IComparable<BoundingBox>
    {
        /// <summary>
        /// Gets or sets the detection index (tracking/association ID)
        /// 获取或设置检测索引（跟踪/关联ID）
        /// </summary>
        /// <value>Unique identifier for tracking purposes</value>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the class/label index reference
        /// 获取或设置类别/标签索引引用
        /// </summary>
        /// <value>Index mapping to class names in a separate collection</value>
        public int NameIndex { get; set; }

        /// <summary>
        /// Gets or sets the detection confidence score (0-1)
        /// 获取或设置检测置信度分数（0-1）
        /// </summary>
        /// <value>
        /// Confidence value between 0 (no confidence) and 1 (certain detection)
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if set to value outside 0-1 range
        /// 当设置的值超出0-1范围时抛出
        /// </exception>
        public float Confidence { get; set; }

        /// <summary>
        /// Gets or sets the rectangular bounding coordinates
        /// 获取或设置矩形边界坐标
        /// </summary>
        /// <value>
        /// Floating-point rectangle defining bounds (position and dimensions)
        /// </value>
        public RectF Box { get; set; }

        /// <summary>
        /// Gets or sets the rotation angle (in degrees)
        /// 获取或设置旋转角度（以度为单位）
        /// </summary>
        /// <value>
        /// Rotation angle around the box center (0 = axis-aligned)
        /// </value>
        /// <remarks>
        /// Not all detection systems support rotated boxes (may default to 0)
        /// 并非所有检测系统都支持旋转框（可能默认为0）
        /// </remarks>
        public float Angle { get; set; }

        /// <summary>
        /// Compares this bounding box to another by confidence score
        /// 通过置信度分数将此边界框与另一个进行比较
        /// </summary>
        /// <param name="other">The bounding box to compare with 要比较的边界框</param>
        /// <returns>
        /// 1 if this instance precedes the other, -1 if it follows, 0 if equal
        /// (sorts boxes descending by confidence)
        /// </returns>
        /// <remarks>
        /// Sorting with this comparer orders boxes from highest to lowest confidence.
        /// 使用此比较器对框进行排序时，将按置信度从高到低的顺序排列。
        /// </remarks>
        public int CompareTo(BoundingBox other) => other.Confidence.CompareTo(this.Confidence);
    }

}
