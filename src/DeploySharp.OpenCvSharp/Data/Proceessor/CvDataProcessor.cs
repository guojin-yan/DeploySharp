using DeploySharp.Log;
using DeploySharp.Model;
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
        public static DataTensor ImageProcessToDataTensor(Mat img, IConfig config, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Mat)img;

            MyLogger.Log.Debug($"配置输入尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                              $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");

            // 记录归一化处理开始
            MyLogger.Log.Debug("开始图像归一化处理 (0-255 to 0-1)...");

            float[] normalizedData = CvDataProcessor.ProcessToFloat(
                image,
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                ((IImgConfig)config).DataProcessor);

            // 创建图像调整参数
            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(image.Size()),
                ((IImgConfig)config).DataProcessor.ResizeMode);

            MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                             $"原始尺寸: {image.Size()}, " +
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

        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Mat)input, size, processorConfig.ResizeMode), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }


        /// <summary>
        /// 根据指定的ResizeMode调整图像尺寸（OpenCVSharp实现）
        /// </summary>
        public static Mat Resize(
            Mat img,
            Size size,
            ImageResizeMode resizeMode,
            InterpolationFlags interpolation = InterpolationFlags.Lanczos4)
        {
            if (img.Empty())
                throw new ArgumentException("Input image is empty");

            var targetSize = new OpenCvSharp.Size(size.Width, size.Height);
            Mat output = new Mat();

            switch (resizeMode)
            {
                case ImageResizeMode.Stretch:
                    Cv2.Resize(img, output, targetSize, 0, 0, interpolation);
                    break;

                case ImageResizeMode.Pad:
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

                case ImageResizeMode.Max:
                    double ratio = Math.Min(
                        (double)size.Width / img.Width,
                        (double)size.Height / img.Height);
                    Cv2.Resize(img, output, new OpenCvSharp.Size(), ratio, ratio, interpolation);
                    break;

                case ImageResizeMode.Crop:
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
        public static float[] Normalize(Mat im, float[] mean, float[] scale, bool isScale)
        {
            if (im.Channels() != 3)
                throw new ArgumentException("Input image must have 3 channels");
            if (mean == null || mean.Length < 3 || scale == null || scale.Length < 3)
                throw new ArgumentException("Mean and scale arrays must have 3 elements each");

            double e = 1.0;
            if (isScale)
            {
                e /= 255.0;
            }
            im.ConvertTo(im, MatType.CV_32FC3, e);
            Mat[] bgr_channels = new Mat[3];
            Cv2.Split(im, out bgr_channels);
            //for (var i = 0; i < bgr_channels.Length; i++)
            //{
            //    bgr_channels[i].ConvertTo(bgr_channels[i], MatType.CV_32FC1, 1.0 * scale[i],
            //        (0.0 - mean[i]) * scale[i]);
            //}

            Parallel.For(0, 3, i =>
            {
                bgr_channels[i].ConvertTo(bgr_channels[i], MatType.CV_32FC1, 1.0 * scale[i],
                          (0.0 - mean[i]) * scale[i]);
            });
            Mat re = new Mat();
            Cv2.Merge(bgr_channels, re);
            int rh = im.Rows;
            int rw = im.Cols;
            int rc = im.Channels();
            float[] res = new float[rh * rw * rc];

            GCHandle resultHandle = default;
            try
            {
                resultHandle = GCHandle.Alloc(res, GCHandleType.Pinned);
                IntPtr resultPtr = resultHandle.AddrOfPinnedObject();
                Parallel.For(0, rc, i =>
                {
                    using Mat dest = Mat.FromPixelData(rh, rw, MatType.CV_32FC1, resultPtr + i * rh * rw * sizeof(float));
                    Cv2.ExtractChannel(re, dest, i);
                });
                //    for (int i = 0; i < rc; ++i)
                //{
                //    using Mat dest = Mat.FromPixelData(rh, rw, MatType.CV_32FC1, resultPtr + i * rh * rw * sizeof(float));
                //    Cv2.ExtractChannel(im, dest, i);
                //}
            }
            finally
            {
                resultHandle.Free();
            }
            return res;
        }

        public static float[] Normalize(Mat im, bool isScale)
        {
            // 参数校验
            if (im == null)
                throw new ArgumentNullException(nameof(im));

            double e = 1.0;
            if (isScale)
            {
                e /= 255.0;
            }
            im.ConvertTo(im, MatType.CV_32FC3, e);
            int rh = im.Rows;
            int rw = im.Cols;
            int rc = im.Channels();
            float[] res = new float[rh * rw * rc];

            GCHandle resultHandle = default;
            try
            {
                resultHandle = GCHandle.Alloc(res, GCHandleType.Pinned);
                IntPtr resultPtr = resultHandle.AddrOfPinnedObject();
                Parallel.For(0, rc, i =>
                {
                    using Mat dest = Mat.FromPixelData(rh, rw, MatType.CV_32FC1, resultPtr + i * rh * rw * sizeof(float));
                    Cv2.ExtractChannel(im, dest, i);
                });
                //    for (int i = 0; i < rc; ++i)
                //{
                //    using Mat dest = Mat.FromPixelData(rh, rw, MatType.CV_32FC1, resultPtr + i * rh * rw * sizeof(float));
                //    Cv2.ExtractChannel(im, dest, i);
                //}
            }
            finally
            {
                resultHandle.Free();
            }
            return res;
        }



        /// <summary>
        /// 执行归一化 (伪代码示例)
        /// </summary>
        public static float[] Normalize(Mat image, ImageNormalizationType type, NormalizationParams customParams = null)
        {
            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);
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
