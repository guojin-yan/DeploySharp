using DeploySharp.Data;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Processing;
using ResizeMode = DeploySharp.Data.ResizeMode;
using Size = DeploySharp.Data.Size;

namespace DeploySharp.Data
{
    public static class CvDataProcessor
    {
        /// <summary>
        /// 根据指定的 ResizeMode 调整图像尺寸
        /// </summary>
        /// <param name="img">原始图像</param>
        /// <param name="targetWidth">目标宽度</param>
        /// <param name="targetHeight">目标高度</param>
        /// <param name="resizeMode">调整模式</param>
        /// <returns>调整后的图像</returns>
        public static Image<Rgb24> Resize(
            Image<Rgb24> image,
            Size size,
            ResizeMode resizeMode)
        {
            var options = new ResizeOptions
            {
                Size = CvDataExtensions.ToSize(size),
                Sampler = KnownResamplers.Lanczos3 // 高质量插值算法
            };

            Image<Rgb24> img = image.Clone();
            switch (resizeMode)
            {
                case ResizeMode.Stretch:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch;
                    break;

                case ResizeMode.Pad:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad;
                    options.PadColor = Color.Black; // 默认填充黑色
                    break;

                case ResizeMode.Max:
                    options.Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max;
                    break;
                case ResizeMode.Crop:
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
    }
}

