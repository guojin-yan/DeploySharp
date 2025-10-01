using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Configuration settings for data processing pipeline
    /// 数据预处理管线的配置设置
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls how input data (typically images) should be preprocessed before feeding to models.
    /// Includes resize behavior and normalization settings.
    /// </para>
    /// <para>
    /// 控制输入数据（通常是图像）在送入模型前应如何预处理。
    /// 包含缩放行为和归一化设置。
    /// </para>
    /// <example>
    /// Typical usage:
    /// <code>
    /// // Configure preprocessing with padding resize and imagenet normalization
    /// var config = new DataProcessorConfig(
    ///     resizeMode: ImageResizeMode.Pad,
    ///     normalizationType: ImageNormalizationType.ImageNet);
    /// 
    /// // Or configure with custom normalization parameters
    /// var customConfig = new DataProcessorConfig {
    ///     ResizeMode = ImageResizeMode.Crop,
    ///     NormalizationType = ImageNormalizationType.Custom,
    ///     CustomNormalizationParams = new NormalizationParams(mean: [0.5,0.5,0.5], std: [0.5,0.5,0.5])
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class DataProcessorConfig
    {
        /// <summary>
        /// Initializes a new instance with default settings
        /// (ResizeMode=Stretch, NormalizationType=None)
        /// 使用默认设置初始化新实例（缩放模式=拉伸，归一化类型=无）
        /// </summary>
        public DataProcessorConfig() { }

        /// <summary>
        /// Initializes a new instance with specified processing settings
        /// 使用指定的处理设置初始化新实例
        /// </summary>
        /// <param name="resizeMode">
        /// How input images should be resized
        /// 输入图像的缩放方式
        /// </param>
        /// <param name="normalizationType">
        /// Type of normalization to apply
        /// 应用的归一化类型
        /// </param>
        /// <param name="normalizationParams">
        /// Custom normalization parameters (required when NormalizationType=Custom)
        /// 自定义归一化参数（当归一化类型为Custom时需要）
        /// </param>
        public DataProcessorConfig(
            ImageResizeMode resizeMode,
            ImageNormalizationType normalizationType,
            NormalizationParams normalizationParams = null)
        {
            NormalizationType = normalizationType;
            ResizeMode = resizeMode;
            CustomNormalizationParams = normalizationParams;
        }

        /// <summary>
        /// Gets or sets the normalization method to apply
        /// 获取或设置应用的归一化方法
        /// </summary>
        /// <value>
        /// Default is <see cref="ImageNormalizationType.None"/>
        /// </value>
        /// <remarks>
        /// When set to Custom, must provide <see cref="CustomNormalizationParams"/>
        /// 当设置为Custom时，必须提供<see cref="CustomNormalizationParams"/>
        /// </remarks>
        public ImageNormalizationType NormalizationType { get; set; } = ImageNormalizationType.None;

        /// <summary>
        /// Gets or sets custom normalization parameters
        /// 获取或设置自定义归一化参数
        /// </summary>
        /// <value>
        /// Required when <see cref="NormalizationType"/> is Custom, null otherwise
        /// </value>
        /// <exception cref="InvalidOperationException">
        /// Thrown when CustomNormalizationParams is null but NormalizationType is Custom
        /// 当归一化类型为Custom但未提供自定义参数时抛出
        /// </exception>
        public NormalizationParams CustomNormalizationParams { get; set; } = null;

        /// <summary>
        /// Gets or sets how input images should be resized
        /// 获取或设置输入图像的缩放方式
        /// </summary>
        /// <value>
        /// Default is <see cref="ImageResizeMode.Stretch"/>
        /// </value>
        public ImageResizeMode ResizeMode { get; set; } = ImageResizeMode.Stretch;
    }

}
