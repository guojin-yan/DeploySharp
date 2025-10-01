using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Specifies image normalization methods
    /// 指定图像归一化处理方法
    /// </summary>
    /// <remarks>
    /// Different normalization schemes are used depending on model requirements.
    /// 根据模型要求使用不同的归一化方案。
    /// </remarks>
    public enum ImageNormalizationType
    {
        /// <summary>
        /// No normalization applied (keeps original pixel values 0-255)
        /// 不做归一化 (保持原始像素值 0-255)
        /// </summary>
        None,

        /// <summary>
        /// Simple scaling to [0,1] range (divide by 255)
        /// 简单缩放到 [0,1] 范围 (除以255)
        /// </summary>
        Scale_0_1,

        /// <summary>
        /// Scaling to [-1,1] range (divide by 127.5 then subtract 1)
        /// 缩放到 [-1,1] 范围 (除以127.5减1)
        /// </summary>
        Scale_Neg1_1,

        /// <summary>
        /// ImageNet standard normalization (mean=[0.485,0.456,0.406], std=[0.229,0.224,0.225])
        /// ImageNet标准归一化 (mean=[0.485,0.456,0.406], std=[0.229,0.224,0.225])
        /// </summary>
        ImageNetStandard,

        /// <summary>
        /// Custom mean and standard deviation normalization
        /// 自定义均值和标准差归一化
        /// </summary>
        CustomStandard
    }

    /// <summary>
    /// Contains configuration parameters for normalization operations
    /// 包含归一化操作的配置参数
    /// </summary>
    /// <remarks>
    /// Used to specify preprocessing parameters before feeding images to models.
    /// 用于指定图像输入模型前的预处理参数。
    /// </remarks>
    public class NormalizationParams
    {
        /// <summary>
        /// Channel-wise mean values (length must match number of channels)
        /// 各通道的均值 (长度必须与通道数匹配)
        /// </summary>
        public float[] Mean { get; set; }

        /// <summary>
        /// Channel-wise standard deviations (length must match number of channels)
        /// 各通道的标准差 (长度必须与通道数匹配)
        /// </summary>
        public float[] Std { get; set; }

        /// <summary>
        /// Maximum pixel value (used for Min-Max normalization)
        /// Default: 255f
        /// 最大像素值 (Min-Max归一化时使用)
        /// 默认值: 255f
        /// </summary>
        public float? MaxPixelValue { get; set; } = 255f;

        /// <summary>
        /// Small constant to prevent division by zero
        /// Default: 1e-5f
        /// 小常数 (防止除零)
        /// 默认值: 1e-5f
        /// </summary>
        public float Epsilon { get; set; } = 1e-5f;

        /// <summary>
        /// Creates a deep copy of these parameters
        /// 创建这些参数的深拷贝
        /// </summary>
        public NormalizationParams Clone()
        {
            return new NormalizationParams
            {
                Mean = Mean?.ToArray(),
                Std = Std?.ToArray(),
                MaxPixelValue = MaxPixelValue,
                Epsilon = Epsilon
            };
        }
    }

    /// <summary>
    /// Factory class providing preset normalization parameters
    /// 提供预设归一化参数的工厂类
    /// </summary>
    /// <remarks>
    /// Simplifies configuration by providing common normalization presets.
    /// 通过提供常见的归一化预设简化配置。
    /// </remarks>
    public static class NormalizationParamsFactory
    {
        private static readonly Dictionary<ImageNormalizationType, NormalizationParams> _presets =
            new Dictionary<ImageNormalizationType, NormalizationParams>
            {
                // ImageNet standard normalization parameters
                // ImageNet标准归一化参数
                [ImageNormalizationType.ImageNetStandard] = new NormalizationParams
                {
                    Mean = new[] { 0.485f, 0.456f, 0.406f },
                    Std = new[] { 0.229f, 0.224f, 0.225f }
                },

                // Scaling presets
                // 缩放预设
                [ImageNormalizationType.Scale_0_1] = new NormalizationParams
                {
                    MaxPixelValue = 255f
                },

                [ImageNormalizationType.Scale_Neg1_1] = new NormalizationParams
                {
                    MaxPixelValue = 127.5f
                }
            };

        /// <summary>
        /// Gets normalization parameters for specified type
        /// 获取指定类型的归一化参数
        /// </summary>
        /// <param name="type">Normalization type 归一化类型</param>
        /// <param name="customMean">
        /// Custom mean values (required for CustomStandard)
        /// 自定义均值 (CustomStandard必需)
        /// </param>
        /// <param name="customStd">
        /// Custom std values (required for CustomStandard)
        /// 自定义标准差 (CustomStandard必需)
        /// </param>
        /// <returns>Configured normalization parameters 配置好的归一化参数</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when custom parameters are required but not provided
        /// 当需要自定义参数但未提供时抛出
        /// </exception>
        public static NormalizationParams GetParams(
            ImageNormalizationType type,
            float[] customMean = null,
            float[] customStd = null)
        {
            if (type == ImageNormalizationType.CustomStandard)
            {
                if (customMean == null || customStd == null)
                    throw new ArgumentException(
                        "Custom normalization requires both mean and std parameters",
                        nameof(type));

                return new NormalizationParams { Mean = customMean, Std = customStd };
            }

            return _presets.TryGetValue(type, out var preset)
                ? preset
                : new NormalizationParams();
        }
    }

}
