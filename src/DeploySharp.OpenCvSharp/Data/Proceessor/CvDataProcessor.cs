using DeploySharp.Log;
using DeploySharp.Model;
using iTextSharp.text.pdf;
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

        public static DataTensor ImageListProcessToDataTensor(List<Mat> imgs, IConfig config, out ImageAdjustmentParam[] imageAdjustmentParams)
        {
            MyLogger.Log.Debug($"配置输入尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                  $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");

            // 记录归一化处理开始
            MyLogger.Log.Debug("开始图像归一化处理 (0-255 to 0-1)...");
            List<float[]> normalizedDatas = new List<float[]>();
            List<ImageAdjustmentParam> imageAdjustmentParamList = new List<ImageAdjustmentParam>();
            int dataLength = 0;
            for (int i = 0; i < imgs.Count; i++)
            {
                var image = (Mat)imgs[i];
                float[] normalizedData = CvDataProcessor.ProcessToFloat(
                image,
                new Data.Size(config.InputSizes[0][3], config.InputSizes[0][2]),
                ((IImgConfig)config).DataProcessor);

                dataLength += normalizedData.Length;
                normalizedDatas.Add(normalizedData);

                imageAdjustmentParamList.Add( ImageAdjustmentParam.CreateFromImageInfo(
                    new Data.Size(config.InputSizes[0][3], config.InputSizes[0][2]),
                    CvDataExtensions.ToCvSize(image.Size()),
                    ((IImgConfig)config).DataProcessor.ResizeMode));


                MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                     $"原始尺寸: {image.Size()}, " +
                     $"目标尺寸: {config.InputSizes[0][3]}x{config.InputSizes[0][2]}, " +
                     $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");
            }
            imageAdjustmentParams = imageAdjustmentParamList.ToArray();
            List<float> imageDatas = new List<float>(dataLength);
            foreach (var item in normalizedDatas)
            {
                imageDatas.AddRange(item);
            }

            // 构造数据张量
            MyLogger.Log.Debug("构造输入DataTensor...");
            DataTensor dataTensors = new DataTensor();
            dataTensors.AddNode(
                config.InputNames[0],
                0,
                TensorType.Input,
                imageDatas.ToArray(),
                config.InputSizes[0],
                typeof(float));

            MyLogger.Log.Debug($"DataTensor构造完成，输入名称: {config.InputNames[0]}, " +
                             $"数据类型: {typeof(float)}, " +
                             $"数据长度: {imageDatas.Count}");

            return dataTensors;

        }
        /// <summary>
        /// Full preprocessing pipeline (resize + normalize)
        /// 完整的预处理流程(调整尺寸 + 标准化)
        /// </summary>
        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Mat)input, size, processorConfig.ResizeMode, InterpolationFlags.Linear), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
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
            InterpolationFlags interpolation = InterpolationFlags.Linear)
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
                    Cv2.Resize(img, resized, scaledSize, 0f, 0f, interpolation);

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
                case ImageResizeMode.CrnnPad:
                    // --- 模式：CRNN 专用 (右侧灰色填充) ---
                    // 逻辑复用自 PaddleOCR 的预处理函数

                    //// 1. 确定固定高度
                    //int imgH = size.Height;

                    //// 2. 根据宽高比动态计算目标宽度 (如果不传 whRatio，默认使用 size 的宽度)
                    //int imgW = (whRatio > 0) ? (int)(imgH * whRatio) : size.Width;
                    //// 3. 计算原图宽高比
                    //float ratio_f = (float)(img.Cols) / (float)(img.Rows);

                    //// 4. 计算缩放后的宽度
                    //// 限制：缩放后的宽度不能超过 imgW (这是模型输入的最大限制)
                    //int resize_w;
                    //if (Math.Ceiling(imgH * ratio_f) > imgW)
                    //    resize_w = imgW;
                    //else
                    //    resize_w = (int)(Math.Ceiling(imgH * ratio_f));
                    //// 5. 执行缩放 (高度固定为 imgH，宽度按比例)
                    //Mat crnnResized = new Mat();
                    //Cv2.Resize(img, crnnResized, new OpenCvSharp.Size(resize_w, imgH), 0.0f, 0.0f, interpolation);
                    //// 6. 右侧填充
                    //// 填充颜色：灰色 (127, 127, 127)，这是 CRNN 训练时常用的 Padding 值
                    //Cv2.CopyMakeBorder(
                    //    crnnResized,
                    //    crnnResized,
                    //    0, 0, 0,
                    //    (int)(imgW - crnnResized.Cols), // 只在右侧补齐
                    //    BorderTypes.Constant,
                    //    new Scalar(127, 127, 127));
                    //output = crnnResized;
                    //Cv2.ImShow("11", output);
                    //Cv2.WaitKey(0);



                    // 1. 默认填充颜色为中灰色
                    Scalar gray =  new Scalar(128, 128, 128);
                    // 2. 计算按高度缩放的比例
                    double scale1 = (double)size.Height / img.Height;

                    // 3. 第一次缩放：仅根据高度调整
                    using (Mat tempResized = new Mat())
                    {
                        Cv2.Resize(img, tempResized, new OpenCvSharp.Size(0, 0), scale1, scale1, InterpolationFlags.Linear);
                        int currentWidth = tempResized.Width;
                        int currentHeight = tempResized.Height; // 应该等于 targetHeight
                        Mat result = new Mat();
                        // 4. 判断宽度情况
                        if (currentWidth < size.Width)
                        {
                            // --- 情况 A: 宽度不足，右侧填充灰色 ---

                            // 创建一个目标大小的灰色背景
                            result = new Mat(size.Height, size.Width, img.Type(), gray);
                            // 将缩放后的图像拷贝到背景的左侧
                            // Rect(起始X, 起始Y, 宽, 高)
                            OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(0, 0, currentWidth, currentHeight);
                            tempResized.CopyTo(new Mat(result, roi1));
                        }
                        else if (currentWidth > size.Width)
                        {
                            // --- 情况 B: 宽度过大，压缩宽度 ---

                            // 计算宽度的缩放比例 (高度保持不变，所以高度比例为 1.0)
                            double widthScale = (double)size.Width / currentWidth;

                            // 强制将宽度压缩到目标宽度
                            Cv2.Resize(tempResized, result, new OpenCvSharp.Size(size.Width, size.Height), 0, 0, InterpolationFlags.Linear);
                        }
                        else
                        {
                            // --- 情况 C: 宽度正好相等 ---
                            // 直接返回
                            return tempResized.Clone();
                        }

                        output = result;
                    }
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
            //Mat[] rgbChannels = new Mat[] { bgr_channels[2], bgr_channels[1], bgr_channels[0] };
            Parallel.For(0, 3, i =>
            {
                bgr_channels[i].ConvertTo(bgr_channels[i], MatType.CV_32FC1, 1.0f / scale[i],
                          (0.0f - mean[i]) / scale[i]);
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
                    //return null;

                case ImageNormalizationType.ImageNetStandard:
                case ImageNormalizationType.CustomStandard:
                    return Normalize(image, parameters.Mean, parameters.Std, true);

                default:
                    return Normalize(image, false);
            }
        }


    }

}
