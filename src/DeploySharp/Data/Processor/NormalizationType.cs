using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// 图像归一化处理方式
    /// </summary>
    public enum ImageNormalizationType
    {
        /// <summary>
        /// 不做归一化 (保持原始像素值 0-255)
        /// </summary>
        None,

        /// <summary>
        /// 简单缩放到 [0,1] 范围 (除以255)
        /// </summary>
        Scale_0_1,

        /// <summary>
        /// 缩放到 [-1,1] 范围 (除以127.5减1)
        /// </summary>
        Scale_Neg1_1,

        /// <summary>
        /// ImageNet标准归一化 (mean=[0.485,0.456,0.406], std=[0.229,0.224,0.225])
        /// </summary>
        ImageNetStandard,

        /// <summary>
        /// 自定义均值和标准差归一化
        /// </summary>
        CustomStandard
    }


    /// <summary>
    /// 归一化参数配置
    /// </summary>
    public class NormalizationParams
    {
        /// <summary>
        /// 均值 (长度对应通道数)
        /// </summary>
        public float[] Mean { get; set; }

        /// <summary>
        /// 标准差 (长度对应通道数)
        /// </summary>
        public float[] Std { get; set; }

        /// <summary>
        /// 最大像素值 (Min-Max归一化时使用)
        /// </summary>
        public float? MaxPixelValue { get; set; } = 255f;

        /// <summary>
        /// 小常数 (防止除零)
        /// </summary>
        public float Epsilon { get; set; } = 1e-5f;

    }



    /// <summary>
    /// 归一化器工厂
    /// </summary>
    public static class NormalizationParamsFactory
    {
        private static readonly Dictionary<ImageNormalizationType, NormalizationParams> _presets =
            new Dictionary<ImageNormalizationType, NormalizationParams>
            {
                [ImageNormalizationType.ImageNetStandard] = new NormalizationParams
                {
                    Mean = new[] { 0.485f, 0.456f, 0.406f },
                    Std = new[] { 0.229f, 0.224f, 0.225f }
                },

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
        /// 获取预设参数
        /// </summary>
        public static NormalizationParams GetParams(ImageNormalizationType type, float[] customMean = null, float[] customStd = null)
        {
            if (type == ImageNormalizationType.CustomStandard)
            {
                if (customMean == null || customStd == null)
                    throw new ArgumentException("CustomStandard requires mean and std parameters");

                return new NormalizationParams { Mean = customMean, Std = customStd };
            }

            return _presets.TryGetValue(type, out var preset)
                ? preset
                : new NormalizationParams();
        }
    }
}
