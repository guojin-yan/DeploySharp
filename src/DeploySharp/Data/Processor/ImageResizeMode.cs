using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Specifies how images should be resized during preprocessing
    /// 指定预处理期间图像应该如何调整大小
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls the aspect ratio behavior when resizing images to target dimensions.
    /// Different modes affect how bounding boxes should be interpreted post-resize.
    /// </para>
    /// <para>
    /// 控制将图像调整到目标尺寸时的宽高比行为。
    /// 不同模式会影响调整大小后边框的解释方式。
    /// </para>
    /// <example>
    /// Usage example:
    /// <code>
    /// // Configure processor with padding resize mode
    /// var config = new DataProcessorConfig {
    ///     ResizeMode = ImageResizeMode.Pad
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public enum ImageResizeMode
    {
        /// <summary>
        /// Stretches the resized image to fit the bounds of its container.
        /// Both width and height will exactly match target dimensions,
        /// potentially distorting the original aspect ratio.
        /// 
        /// 拉伸调整后的图像以适应容器边界。
        /// 宽度和高度将完全匹配目标尺寸，
        /// 可能会扭曲原始宽高比。
        /// </summary>
        Stretch,

        /// <summary>
        /// Pads the resized image to fit the bounds of its container.
        /// Maintains original aspect ratio by adding padding bars when necessary.
        /// If only one dimension is specified, will maintain original aspect ratio.
        /// 
        /// 填充调整后的图像以适应容器边界。
        /// 必要时通过添加填充条保持原始宽高比。
        /// 如果仅指定一个尺寸，将保持原始宽高比。
        /// </summary>
        /// <remarks>
        /// Commonly used in object detection pipelines to preserve aspect ratio.
        /// Padding areas are typically filled with zeros or normalization values.
        /// 
        /// 通常用于物体检测管线以保持宽高比。
        /// 填充区域通常用零或归一化值填充。
        /// </remarks>
        Pad,

        /// <summary>
        /// Constrains the resized image to fit the bounds of its container
        /// while maintaining the original aspect ratio.
        /// The resulting image will not exceed target dimensions in either axis,
        /// but may be smaller than target dimensions in one axis.
        /// 
        /// 约束调整后的图像以适应容器边界，
        /// 同时保持原始宽高比。
        /// 生成的图像在任何轴上都不会超过目标尺寸，
        /// 但可能在一个轴上小于目标尺寸。
        /// </summary>
        Max,

        /// <summary>
        /// Crops the resized image to fit the bounds of its container.
        /// Maintains aspect ratio by centering and cropping overflow areas.
        /// 
        /// 裁剪调整后的图像以适应容器边界。
        /// 通过居中并裁剪溢出区域保持宽高比。
        /// </summary>
        /// <remarks>
        /// Useful for classification tasks where preserving center content is important.
        /// 
        /// 适用于分类任务，其中保留中心内容很重要。
        /// </remarks>
        Crop
    }

}
