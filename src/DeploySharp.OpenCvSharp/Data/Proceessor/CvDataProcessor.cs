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
        /// <summary>
        /// Full preprocessing pipeline (resize + normalize)
        /// 完整的预处理流程(调整尺寸 + 标准化)
        /// </summary>
        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Mat)input, size, processorConfig.ResizeMode), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }


        /// <summary>
        /// Resizes image using specified mode
        /// 使用指定模式调整图像尺寸
        /// </summary>
        /// <param name="img">Source image/源图像</param>
        /// <param name="size">Target dimensions/目标尺寸</param>
        /// <param name="resizeMode">Resizing strategy/尺寸调整策略</param>
        /// <returns>Resized image/调整后的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid resize mode/当调整模式无效时抛出</exception>
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
        /// Normalizes image with mean subtraction and scaling
        /// 使用均值减除和缩放标准化图像
        /// </summary>
        /// <param name="im">Source image/源图像</param>
        /// <param name="mean">Channel means/通道均值</param>
        /// <param name="scale">Channel scales/通道缩放</param>
        /// <param name="isScale">Whether to apply 0-1 scaling/是否应用0-1缩放</param>
        /// <returns>Normalized float array/标准化后的浮点数组</returns>
        /// <remarks>
        /// Uses parallel processing for better performance on large images
        /// 对大图像使用并行处理以提高性能
        /// </remarks>
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
        /// <summary>
        /// Applies basic 0-1 or no normalization
        /// 应用基础的0-1标准化或不处理
        /// </summary>
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
        /// Normalizes image using specified scheme
        /// 使用指定方案标准化图像
        /// </summary>
        /// <param name="image">Source image/源图像</param>
        /// <param name="type">Normalization type/标准化类型</param>
        /// <param name="customParams">Custom parameters when type is CustomStandard/当类型为CustomStandard时的自定义参数</param>
        /// <returns>Normalized float array/标准化后的浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
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
