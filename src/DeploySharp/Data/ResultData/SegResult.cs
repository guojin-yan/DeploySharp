using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents semantic segmentation results including pixel-wise classification mask
    /// 表示语义分割结果，包含像素级分类掩码
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="DetResult"/> with additional segmentation mask capability.
    /// Used for dense prediction tasks where each pixel needs classification.
    /// </para>
    /// <para>
    /// 继承自<see cref="DetResult"/>并增加了分割掩码功能。
    /// 用于需要像素级分类的密集预测任务。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var segResult = new SegResult 
    /// {
    ///     Bounds = new Rect(100, 150, 200, 200),
    ///     Mask = new ImageDataF(width: 200, height: 200, channels: 1),
    ///     Confidence = 0.92f,
    ///     Category = "road"
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="DetResult"/>
    /// <seealso cref="ResultType.Segmentation"/>
    public class SegResult : DetResult
    {
        /// <summary>
        /// Pixel-wise segmentation mask represented as floating-point image data
        /// 像素级分割掩码，表示为浮点型图像数据
        /// </summary>
        /// <value>
        /// <para>
        /// For binary segmentation: single channel where values > 0.5 typically indicate positive class.
        /// </para>
        /// <para>
        /// For multi-class: channels typically represent class probabilities.
        /// </para>
        /// <para>
        /// 二值分割: 单通道，值>0.5通常表示正类。
        /// 多分类: 通道通常表示类别概率。
        /// </para>
        /// </value>
        public ImageDataF Mask { get; set; }

        /// <summary>
        /// Initializes a new segmentation result with proper type configuration
        /// 初始化一个新的分割结果，自动配置正确的结果类型
        /// </summary>
        /// <remarks>
        /// Automatically sets <see cref="Result.Type"/> to <see cref="ResultType.Segmentation"/>
        /// 自动将<see cref="Result.Type"/>设置为<see cref="ResultType.Segmentation"/>
        /// </remarks>
        public SegResult()
        {
            Type = ResultType.Segmentation;
        }


        /// <summary>
        /// Creates a deep copy of this segmentation result
        /// 创建此分割结果的深拷贝
        /// </summary>
        /// <returns>
        /// A new <see cref="SegResult"/> with copied properties and cloned mask
        /// 包含复制属性和克隆掩码的新<see cref="SegResult"/>对象
        /// </returns>
        public new SegResult Clone()
        {
            return new SegResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Category = Category,
                Bounds = Bounds,
                Mask = (ImageDataF)(Mask?.Clone())
            };
        }

        /// <summary>
        /// Returns formatted string representation including mask dimensions
        /// 返回包含掩码尺寸的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Combined string with base detection info and mask size
        /// 包含基础检测信息和掩码尺寸的组合字符串
        /// </returns>
        public override string ToString()
        {
            return $"{base.ToString()}, Mask: {(Mask != null ? $"{Mask.Width}x{Mask.Height}" : "null")}";
        }
    }

}
