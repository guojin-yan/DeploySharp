using System;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents the base result structure for model inference outputs
    /// 表示模型推理输出的基础结果结构
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains common properties for all result types including classification,
    /// detection, segmentation etc.
    /// </para>
    /// <para>
    /// 包含所有结果类型的通用属性，包括分类、检测、分割等。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var result = new Result {
    ///     Type = ResultType.Classification,
    ///     Confidence = 0.95f,
    ///     Category = "dog"
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class Result
    {
        /// <summary>
        /// Type of the inference result
        /// 推理结果的类型
        /// </summary>
        /// <value>
        /// Default: ResultType.Classification
        /// </value>
        public ResultType Type { get; set; } = ResultType.Classification;

        /// <summary>
        /// Size of the source input image
        /// 输入源图像的尺寸
        /// </summary>
        public Size ImageSize { get; set; }

        /// <summary>
        /// Numeric identifier of the result
        /// 结果的数字标识符
        /// </summary>
        public int Id { get; set; }

        private string _category;

        /// <summary>
        /// Category/class name of the result
        /// 结果的类别/分类名称
        /// </summary>
        /// <remarks>
        /// <para>
        /// Returns ID as string when category is null or empty.
        /// Automatically converts null/empty strings to ID representation.
        /// </para>
        /// <para>
        /// 当类别为空时返回ID的字符串形式。自动将null/空字符串转换为ID表示。
        /// </para>
        /// </remarks>
        public string Category
        {
            get => string.IsNullOrEmpty(_category) ? Id.ToString() : _category;
            set => _category = value;
        }

        /// <summary>
        /// Confidence score between 0 and 1
        /// 置信度分数(0到1之间)
        /// </summary>
        /// <value>
        /// Should be normalized to range [0,1]
        /// </value>
        public float Confidence { get; set; }

        /// <summary>
        /// Updates the category from a categories array
        /// 从类别数组中更新类别
        /// </summary>
        /// <param name="categories">
        /// Array of possible category names
        /// 可能的类别名称数组
        /// </param>
        /// <remarks>
        /// Uses first element if array is non-empty, otherwise sets to null
        /// 如果数组非空则使用第一个元素，否则设为null
        /// </remarks>
        public void UpdateCategory(string[] categories)
        {
            Category = categories.Length > 0 ? categories[0] : null;
        }

        /// <summary>
        /// Returns formatted string representation of the result
        /// 返回结果的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Formatted string with ID, Category, Confidence and ImageSize
        /// 包含ID、类别、置信度和图像尺寸的格式化字符串
        /// </returns>
        public override string ToString()
        {
            return $"ID: {Id}, Category: {Category}, Confidence: {Confidence:P2}, Image Size: {ImageSize}";
        }

        /// <summary>
        /// Creates a deep copy of this result
        /// 创建此结果的深拷贝
        /// </summary>
        public Result Clone()
        {
            return new Result
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                _category = _category,
                Confidence = Confidence
            };
        }
    }

    /// <summary>
    /// Defines types of model inference results
    /// 定义模型推理结果的类型
    /// </summary>
    public enum ResultType
    {
        /// <summary>
        /// Single-class or multi-class classification result
        /// 单分类或多分类结果
        /// </summary>
        Classification,

        /// <summary>
        /// Object detection with axis-aligned bounding boxes
        /// 带有轴对齐边界框的目标检测
        /// </summary>
        Detection,

        /// <summary>
        /// Detection with rotated bounding boxes (OBB)
        /// 带有旋转边界框的检测(OBB)
        /// </summary>
        OrientedBoundingBoxes,

        /// <summary>
        /// Pixel-wise segmentation mask
        /// 像素级分割掩码
        /// </summary>
        Segmentation,

        /// <summary>
        /// Keypoint detection or pose estimation
        /// 关键点检测或姿态估计
        /// </summary>
        KeyPoints,

        /// <summary>
        /// Anomaly segmentation results
        /// 异常分割结果
        /// </summary>
        AnomalySegmentation
    }

}
