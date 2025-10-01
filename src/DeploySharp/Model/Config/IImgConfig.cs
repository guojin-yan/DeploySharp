using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Base configuration interface for image processing models
    /// 图像处理模型的基础配置接口
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="IConfig"/> with image-specific processing parameters.
    /// Contains a <see cref="DataProcessorConfig"/> for common image preprocessing
    /// and postprocessing operations.
    /// </para>
    /// <para>
    /// 扩展了<see cref="IConfig"/>，包含图像处理专用参数。
    /// 提供了<see cref="DataProcessorConfig"/>用于常见的图像预处理和后处理操作。
    /// </para>
    /// <example>
    /// Typical usage:
    /// <code>
    /// var config = new IImgConfig 
    /// {
    ///     ModelPath = "model.onnx",
    ///     DataProcessor = new DataProcessorConfig
    ///     {
    ///         Normalize = true,
    ///         Mean = new[] { 0.485f, 0.456f, 0.406f },
    ///         Std = new[] { 0.229f, 0.224f, 0.225f }
    ///     }
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class IImgConfig : IConfig
    {
        /// <summary>
        /// Configuration for image data processing pipeline
        /// 图像数据处理管道的配置
        /// </summary>
        /// <value>
        /// Contains settings for normalization, resizing, color conversion
        /// and other common computer vision operations.
        /// 
        /// 包含标准化、调整大小、颜色转换和其他常见计算机视觉操作的设置。
        /// </value>
        public DataProcessorConfig DataProcessor { get; set; } = new DataProcessorConfig();
    }

}
