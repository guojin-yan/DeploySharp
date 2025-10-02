using DeploySharp.Data;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Processing;
using ResizeMode = DeploySharp.Data.ImageResizeMode;
using Size = DeploySharp.Data.Size;
using DeploySharp.Log;
using DeploySharp.Model;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides image processing utilities for computer vision tasks
    /// 提供计算机视觉任务的图像处理工具
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles essential image preprocessing operations including:
    /// 处理关键的图像预处理操作包括:
    /// - Resizing with various modes
    ///   多种模式调整尺寸
    /// - Normalization (multiple schemes)
    ///   标准化(多种方案)
    /// - Tensor conversion
    ///   张量转换
    /// </para>
    /// <para>
    /// Optimized implementations leveraging parallelism where possible
    /// 尽可能利用并行化的优化实现
    /// </para>
    /// </remarks>
    public static class CvDataProcessor
    {
        /// <summary>
        /// Processes image into DataTensor format
        /// 将图像处理为DataTensor格式
        /// </summary>
        /// <param name="img">Input RGB image/输入RGB图像</param>
        /// <param name="config">Model configuration/模型配置</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters/输出调整参数</param>
        /// <returns>Processed tensor data/处理后的张量数据</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input or config is null
        /// 当输入或配置为null时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when processing fails
        /// 当处理失败时抛出
        /// </exception>
        public static DataTensor ImageProcessToDataTensor(Image<Rgb24> img, IConfig config, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];

            MyLogger.Log.Debug($"配置输入尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                              $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");

            // 记录归一化处理开始
            MyLogger.Log.Debug("开始图像归一化处理 (0-255 to 0-1)...");

            float[] normalizedData = CvDataProcessor.ProcessToFloat(
                img,
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                ((IImgConfig)config).DataProcessor);

            // 创建图像调整参数
            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(img.Size()),
                ((IImgConfig)config).DataProcessor.ResizeMode);

            MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                             $"原始尺寸: {img.Size()}, " +
                             $"目标尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                             $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");

            // 构造数据张量
            MyLogger.Log.Debug("构造输入DataTensor...");
            DataTensor dataTensors = new DataTensor();
            dataTensors.AddNode(
                config.InputNames[0],
                0,
                TensorType.Input,
                normalizedData,
                config.InputSizes[0],
                typeof(float));

            MyLogger.Log.Debug($"DataTensor构造完成，输入名称: {config.InputNames[0]}, " +
                             $"数据类型: {typeof(float)}, " +
                             $"数据长度: {normalizedData.Length}");

            return dataTensors;
        }

        /// <summary>
        /// Full preprocessing pipeline (resize + normalize)
        /// 完整的预处理流程(调整尺寸 + 标准化)
        /// </summary>
        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Image<Rgb24>)input, size, processorConfig.ResizeMode), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }


        /// <summary>
        /// Resizes image using specified mode
        /// 使用指定模式调整图像尺寸
        /// </summary>
        /// <param name="image">Source image/源图像</param>
        /// <param name="size">Target dimensions/目标尺寸</param>
        /// <param name="resizeMode">Resizing strategy/尺寸调整策略</param>
        /// <returns>Resized image/调整后的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid resize mode/当调整模式无效时抛出</exception>
        public static Image<Rgb24> Resize(
            Image<Rgb24> image,
            Size size,
            ImageResizeMode resizeMode)
        {
            var options = new ResizeOptions
            {
                Size = CvDataExtensions.ToSize(size),
                Sampler = KnownResamplers.Lanczos3 // 高质量插值算法
            };

            Image<Rgb24> img = image.Clone();
            switch (resizeMode)
            {
                case ImageResizeMode.Stretch:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch;
                    break;

                case ImageResizeMode.Pad:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad;
                    options.PadColor = Color.Black; // 默认填充黑色
                    break;

                case ImageResizeMode.Max:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max;
                    break;
                case ImageResizeMode.Crop:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(resizeMode));
            }

            img.Mutate(x => x.Resize(options));
            return img;
        }


        /// <summary>
        /// Normalizes image with mean subtraction and scaling
        /// 使用均值减除和缩放标准化图像
        /// </summary>
        /// <param name="image">Source image/源图像</param>
        /// <param name="mean">Channel means/通道均值</param>
        /// <param name="scale">Channel scales/通道缩放</param>
        /// <param name="isScale">Whether to apply 0-1 scaling/是否应用0-1缩放</param>
        /// <returns>Normalized float array/标准化后的浮点数组</returns>
        /// <remarks>
        /// Uses parallel processing for better performance on large images
        /// 对大图像使用并行处理以提高性能
        /// </remarks>
        public static float[] Normalize(Image<Rgb24> image, float[] mean, float[] scale, bool isScale)
        {
            //var normalizedData = ImageToFloatArray(image, isScale);

            //int pixelCount = image.Width * image.Height;
            //for (int c = 0; c < 3; c++)
            //{
            //    int channelOffset = c * pixelCount;
            //    float channelScale = scale[c];
            //    float channelMean = mean[c];

            //    for (int i = 0; i < pixelCount; i++)
            //    {
            //        normalizedData[channelOffset + i] = (normalizedData[channelOffset + i] - channelMean) * channelScale;
            //    }
            //}

            //return normalizedData;

            int width = image.Width;
            int height = image.Height;
            int pixelCount = width * height;
            float[] result = new float[3 * pixelCount];
            float alpha = isScale ? 1.0f / 255.0f : 1.0f;
            // 先将完整图像复制到内存中
            Rgb24[] pixelArray = new Rgb24[pixelCount];
            image.CopyPixelDataTo(pixelArray);

            // 然后安全并行处理
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = y * width + x;
                    int pixelIndex = y * width + x;
                    result[pixelIndex] = pixelArray[srcIndex].R * alpha * scale[0] - mean[0] * scale[0];
                    result[pixelIndex + pixelCount] = pixelArray[srcIndex].G * alpha * scale[1] - mean[1] * scale[1];
                    result[pixelIndex + 2 * pixelCount] = pixelArray[srcIndex].B * alpha * scale[2] - mean[2] * scale[2];
                }
            });

            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        int srcIndex = y * width + x;
            //        int pixelIndex = y * width + x;
            //        result[pixelIndex] = pixelArray[srcIndex].R * alpha * scale[0] - mean[0] * scale[0];
            //        result[pixelIndex + pixelCount] = pixelArray[srcIndex].G * alpha * scale[1] - mean[1] * scale[1];
            //        result[pixelIndex + 2 * pixelCount] = pixelArray[srcIndex].B * alpha * scale[2] - mean[2] * scale[2];
            //    }
            //}


            return result;

        }
        /// <summary>
        /// Applies basic 0-1 or no normalization
        /// 应用基础的0-1标准化或不处理
        /// </summary>
        public static float[] Normalize(Image<Rgb24> image, bool isScale)
        {
            return ImageToFloatArray(image, isScale);
        }
        /// <summary>
        /// Converts image to float array with optional scaling
        /// 将图像转为浮点数组并可选缩放
        /// </summary>
        private static float[] ImageToFloatArray(Image<Rgb24> image, bool normalize)
        {
            int width = image.Width;
            int height = image.Height;
            int pixelCount = width * height;
            float[] result = new float[3 * pixelCount];
            float scale = normalize ? 1.0f / 255.0f : 1.0f;

            // 先将完整图像复制到内存中
            Rgb24[] pixelArray = new Rgb24[pixelCount];
            image.CopyPixelDataTo(pixelArray);

            // 然后安全并行处理
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = y * width + x;
                    int pixelIndex = y * width + x;
                    result[pixelIndex] = pixelArray[srcIndex].R * scale;
                    result[pixelIndex + pixelCount] = pixelArray[srcIndex].G * scale;
                    result[pixelIndex + 2 * pixelCount] = pixelArray[srcIndex].B * scale;
                }
            });

            return result;
        }

        /// <summary>
        /// Normalizes image using specified scheme
        /// 使用指定方案标准化图像
        /// </summary>
        /// <param name="image">Source image/源图像</param>
        /// <param name="type">Normalization type/标准化类型</param>
        /// <param name="customParams">Custom parameters when type is CustomStandard/当类型为CustomStandard时的自定义参数</param>
        /// <returns>Normalized float array/标准化后的浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
        public static float[] Normalize(Image<Rgb24> image, ImageNormalizationType type, NormalizationParams customParams = null)
        {
            var parameters = type == ImageNormalizationType.CustomStandard
                ? customParams
                : NormalizationParamsFactory.GetParams(type);

            switch (type)
            {
                case ImageNormalizationType.Scale_0_1:
                    return Normalize(image, true);

                case ImageNormalizationType.Scale_Neg1_1:
                    return null;

                case ImageNormalizationType.ImageNetStandard:
                case ImageNormalizationType.CustomStandard:
                    return Normalize(image, parameters.Mean, parameters.Std, true);

                default:
                    return Normalize(image, false);
            }
        }

    }
}

