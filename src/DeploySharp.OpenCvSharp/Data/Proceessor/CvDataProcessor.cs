using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Rect = OpenCvSharp.Rect;

namespace DeploySharp.Data
{
    public static class CvDataProcessor
    {
        /// <summary>
        /// 根据指定的ResizeMode调整图像尺寸（OpenCVSharp实现）
        /// </summary>
        public static Mat Resize(
            Mat img,
            Size size,
            ResizeMode resizeMode,
            InterpolationFlags interpolation = InterpolationFlags.Lanczos4)
        {
            if (img.Empty())
                throw new ArgumentException("Input image is empty");

            var targetSize = new OpenCvSharp.Size(size.Width, size.Height);
            Mat output = new Mat();

            switch (resizeMode)
            {
                case ResizeMode.Stretch:
                    Cv2.Resize(img, output, targetSize, 0, 0, interpolation);
                    break;

                case ResizeMode.Pad:
                    // 计算宽高比例并保持原比例
                    double scale = Math.Min(
                        (double)size.Width / img.Width,
                        (double)size.Height / img.Height);

                    var scaledSize = new OpenCvSharp.Size(
                        (int)(img.Width * scale),
                        (int)(img.Height * scale));

                    Mat resized = new Mat();
                    Cv2.Resize(img, resized, scaledSize, 0, 0, interpolation);

                    // 创建目标图像并填充黑色
                    output = new Mat(size.Height, size.Width, img.Type(), Scalar.Black);

                    // 计算粘贴位置（居中）
                    int x = (size.Width - resized.Width) / 2;
                    int y = (size.Height - resized.Height) / 2;

                    // ROI方式复制图像
                    Mat roi = new Mat(output, new OpenCvSharp.Rect(x, y, resized.Width, resized.Height));
                    resized.CopyTo(roi);
                    resized.Dispose();
                    break;

                case ResizeMode.Max:
                    double ratio = Math.Min(
                        (double)size.Width / img.Width,
                        (double)size.Height / img.Height);
                    Cv2.Resize(img, output, new OpenCvSharp.Size(), ratio, ratio, interpolation);
                    break;

                case ResizeMode.Crop:
                    double cropRatio = Math.Max(
                        (double)size.Width / img.Width,
                        (double)size.Height / img.Height);

                    Mat scaled = new Mat();
                    Cv2.Resize(img, scaled, new OpenCvSharp.Size(), cropRatio, cropRatio, interpolation);

                    // 计算裁剪区域（居中）
                    int cropX = (scaled.Width - size.Width) / 2;
                    int cropY = (scaled.Height - size.Height) / 2;
                    output = scaled[new OpenCvSharp.Rect(cropX, cropY, size.Width, size.Height)].Clone();
                    scaled.Dispose();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(resizeMode));
            }

            return output;
        }

        /// <summary>
        /// 图像归一化处理（OpenCVSharp实现）
        /// </summary>
        public static float[] Normalize(Mat image, float[] mean, float[] scale, bool isScale)
        {
            if (image.Channels() != 3)
                throw new ArgumentException("Input image must have 3 channels");
            if (mean == null || mean.Length < 3 || scale == null || scale.Length < 3)
                throw new ArgumentException("Mean and scale arrays must have 3 elements each");

            // 提前计算归一化因子
            double normFactor = isScale ? 1.0 / 255.0 : 1.0;

            // 转换为浮点型Mat（直接操作原图）
            image.ConvertTo(image, MatType.CV_32FC3, normFactor);

            // 分离通道（避免后续频繁提取）
            Mat[] channels = new Mat[3];
            Cv2.Split(image, out channels);

            int height = image.Rows;
            int width = image.Cols;
            float[] result = new float[3 * height * width];

            // 并行处理三个通道
            Parallel.For(0, 3, c =>
            {
                using Mat channel = channels[c];
                float currentMean = mean[c];
                float currentScale = scale[c];

                // 使用Mat的索引器高效访问数据
                var indexer = channel.GetGenericIndexer<float>();

                // 计算结果数组中的起始位置
                int channelOffset = c * height * width;

                // 处理当前通道的所有像素
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        result[channelOffset + y * width + x] =
                            (indexer[y, x] - currentMean) * currentScale;
                    }
                }
            });

            // 释放临时Mat资源
            foreach (var channel in channels) channel.Dispose();
            return result;
        }

        public static float[] Normalize(Mat image, bool isScale)
        {
            // 参数校验
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            // 转换像素值为浮点数
            double scaleFactor = isScale ? 1.0 / 255.0 : 1.0;
            image.ConvertTo(image, MatType.CV_32FC3, scaleFactor);

            int height = image.Rows;
            int width = image.Cols;
            int channels = image.Channels();
            float[] result = new float[height * width * channels];

            // 并行处理每个通道
            Parallel.For(0, channels, c =>
            {
                // 提取单通道数据
                using Mat channelMat = new Mat();
                Cv2.ExtractChannel(image, channelMat, c);

                // 直接访问Mat数据
                var indexer = channelMat.GetGenericIndexer<float>();
                int channelOffset = c * height * width;

                // 填充结果数组
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        result[channelOffset + y * width + x] = indexer[y, x];
                    }
                }
            });

            return result;
        }




    }

}
