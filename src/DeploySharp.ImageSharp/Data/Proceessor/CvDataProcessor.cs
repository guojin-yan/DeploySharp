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
    public static class CvDataProcessor
    {

        public static DataTensor ImageProcessToDataTensor(Image<Rgb24> img, IConfig config, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];

            MyLogger.Log.Debug($"配置输入尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                              $"缩放模式: {((YoloConfig)config).DataProcessor.ResizeMode}");

            // 记录归一化处理开始
            MyLogger.Log.Debug("开始图像归一化处理 (0-255 to 0-1)...");

            float[] normalizedData = CvDataProcessor.ProcessToFloat(
                img,
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                ((YoloConfig)config).DataProcessor);

            // 创建图像调整参数
            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(img.Size()),
                ((YoloConfig)config).DataProcessor.ResizeMode);

            MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                             $"原始尺寸: {img.Size()}, " +
                             $"目标尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                             $"缩放模式: {((YoloConfig)config).DataProcessor.ResizeMode}");

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


        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Image<Rgb24>)input, size, processorConfig.ResizeMode), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }


        /// <summary>
        /// 根据指定的 ResizeMode 调整图像尺寸
        /// </summary>
        /// <param name="image">原始图像</param>
        /// <param name="size">目标尺寸</param>
        /// <param name="resizeMode">调整模式</param>
        /// <returns>调整后的图像</returns>
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



        public static float[] Normalize(Image<Rgb24> image, float[] mean, float[] scale, bool isScale)
        {
            var normalizedData = ImageToFloatArray(image, isScale);

            int pixelCount = image.Width * image.Height;
            for (int c = 0; c < 3; c++)
            {
                int channelOffset = c * pixelCount;
                float channelScale = scale[c];
                float channelMean = mean[c];

                for (int i = 0; i < pixelCount; i++)
                {
                    normalizedData[channelOffset + i] = (normalizedData[channelOffset + i] - channelMean) * channelScale;
                }
            }

            return normalizedData;
        }

        public static float[] Normalize(Image<Rgb24> image, bool isScale)
        {
            return ImageToFloatArray(image, isScale);
        }

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
        /// 执行归一化 (伪代码示例)
        /// </summary>
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

