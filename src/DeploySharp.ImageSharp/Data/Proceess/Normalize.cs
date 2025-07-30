using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public static class Normalize
    {
        public static float[] Run(Image<Rgb24> image, float[] mean, float[] scale, bool isScale)
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

        public static float[] Run(Image<Rgb24> image, bool isScale)
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
