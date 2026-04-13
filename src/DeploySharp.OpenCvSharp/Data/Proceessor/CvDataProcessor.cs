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
    /// Provides image processing utilities for computer vision tasks.
    /// 提供计算机视觉任务的图像处理工具。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles essential image preprocessing operations including:
    /// 处理关键的图像预处理操作包括:
    /// - Resizing with various modes (多种模式调整尺寸)
    /// - Normalization (multiple schemes) (标准化，多种方案)
    /// - Tensor conversion (张量转换)
    /// </para>
    /// <para>
    /// Optimized implementations leveraging parallelism where possible.
    /// 尽可能利用并行化的优化实现。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Load and preprocess image for model inference
    /// // 加载并预处理图像用于模型推理
    /// using OpenCvSharp;
    /// 
    /// Mat image = Cv2.ImRead("input.jpg");
    /// var config = new Yolov8DetConfig("model.onnx");
    /// 
    /// // Process to tensor format
    /// // 处理为张量格式
    /// DataTensor tensor = CvDataProcessor.ImageProcessToDataTensor(
    ///     image, 
    ///     config, 
    ///     out var adjustParam);
    /// </code>
    /// </example>
    public static class CvDataProcessor
    {
        /// <summary>
        /// Processes a single image into DataTensor format for model inference.
        /// 将单张图像处理为DataTensor格式用于模型推理。
        /// </summary>
        /// <param name="img">Input RGB image (OpenCvSharp Mat) / 输入RGB图像(OpenCvSharp Mat)</param>
        /// <param name="config">Model configuration containing input size and preprocessing parameters / 包含输入尺寸和预处理参数的模型配置</param>
        /// <param name="imageAdjustmentParam">Output image adjustment parameters for post-processing coordinate mapping / 输出图像调整参数，用于后处理坐标映射</param>
        /// <returns>Processed tensor data ready for model input / 准备好用于模型输入的处理后张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when input or config is null / 当输入或配置为null时抛出</exception>
        /// <exception cref="InvalidOperationException">Thrown when processing fails / 当处理失败时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image format is invalid / 当图像格式无效时抛出</exception>
        /// <remarks>
        /// <para>
        /// This method performs the complete preprocessing pipeline:
        /// 此方法执行完整的预处理流程:
        /// 1. Resize image to model input dimensions
        ///    调整图像尺寸到模型输入维度
        /// 2. Normalize pixel values (e.g., 0-255 to 0-1)
        ///    归一化像素值(如0-255到0-1)
        /// 3. Convert to channel-first tensor format [N,C,H,W]
        ///    转换为通道优先张量格式[N,C,H,W]
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("photo.jpg"))
        /// {
        ///     var tensor = CvDataProcessor.ImageProcessToDataTensor(
        ///         image, 
        ///         config, 
        ///         out var param);
        ///     
        ///     // Use tensor for inference
        ///     var results = model.Infer(tensor);
        ///     
        ///     // Adjust results back to original image coordinates
        ///     var adjustedResults = param.AdjustResults(results);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ImageListProcessToDataTensor"/>
        /// <seealso cref="ProcessToFloat"/>
        public static DataTensor ImageProcessToDataTensor(Mat img, IConfig config, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Mat)img;
            //Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

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
        /// Processes a batch of images into DataTensor format for model inference.
        /// 将批量图像处理为DataTensor格式用于模型推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="config">Model configuration / 模型配置</param>
        /// <param name="imageAdjustmentParams">Output array of adjustment parameters for each image / 每张图像的调整参数输出数组</param>
        /// <returns>Processed tensor data containing all images / 包含所有图像的处理后张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs or config is null / 当imgs或config为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when imgs is empty / 当imgs为空时抛出</exception>
        /// <remarks>
        /// <para>
        /// Batch processing improves throughput for multiple images.
        /// 批处理提高了多张图像的吞吐量。
        /// </para>
        /// <para>
        /// All images in the batch are processed with the same configuration.
        /// 批次中的所有图像使用相同的配置进行处理。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt;
        /// {
        ///     Cv2.ImRead("image1.jpg"),
        ///     Cv2.ImRead("image2.jpg"),
        ///     Cv2.ImRead("image3.jpg")
        /// };
        /// 
        /// var batchTensor = CvDataProcessor.ImageListProcessToDataTensor(
        ///     images, 
        ///     config, 
        ///     out var adjustParams);
        /// </code>
        /// </example>
        /// <seealso cref="ImageProcessToDataTensor"/>
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

                imageAdjustmentParamList.Add(ImageAdjustmentParam.CreateFromImageInfo(
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
        /// Full preprocessing pipeline (resize + normalize) for single image.
        /// 完整的预处理流程(调整尺寸 + 标准化)用于单张图像。
        /// </summary>
        /// <param name="input">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="size">Target size for resizing / 调整尺寸的目标大小</param>
        /// <param name="processorConfig">Data processor configuration / 数据处理器配置</param>
        /// <returns>Normalized float array in NCHW format / NCHW格式的归一化浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when input or processorConfig is null / 当输入或处理器配置为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when input is empty / 当输入为空时抛出</exception>
        /// <remarks>
        /// This method combines resize and normalize operations for efficiency.
        /// 此方法结合调整尺寸和归一化操作以提高效率。
        /// </remarks>
        /// <seealso cref="Resize"/>
        /// <seealso cref="Normalize(Mat, ImageNormalizationType, NormalizationParams)"/>
        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Mat)input, size, processorConfig.ResizeMode, InterpolationFlags.Linear), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }

        /// <summary>
        /// Resizes image using specified mode.
        /// 使用指定模式调整图像尺寸。
        /// </summary>
        /// <param name="img">Source image (OpenCvSharp Mat) / 源图像(OpenCvSharp Mat)</param>
        /// <param name="size">Target dimensions / 目标尺寸</param>
        /// <param name="resizeMode">Resizing strategy / 尺寸调整策略</param>
        /// <param name="interpolation">Interpolation algorithm for resizing / 调整尺寸使用的插值算法</param>
        /// <returns>Resized image / 调整后的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null / 当图像为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image is empty / 当图像为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid resize mode / 当调整模式无效时抛出</exception>
        /// <remarks>
        /// <para>
        /// Available resize modes:
        /// 可用的调整尺寸模式:
        /// - Stretch: Directly resize to target dimensions (may distort aspect ratio)
        ///            直接调整到目标尺寸(可能扭曲宽高比)
        /// - Pad: Resize while maintaining aspect ratio, pad with black
        ///        保持宽高比调整尺寸，用黑色填充
        /// - Max: Resize to fit within target dimensions while maintaining aspect ratio
        ///        在保持宽高比的前提下调整到适合目标尺寸
        /// - Crop: Resize to cover target dimensions, then center crop
        ///         调整尺寸以覆盖目标尺寸，然后中心裁剪
        /// - CrnnPad: Special padding mode for CRNN text recognition
        ///            用于CRNN文本识别的特殊填充模式
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("photo.jpg"))
        /// {
        ///     // Resize with padding to maintain aspect ratio
        ///     // 使用填充调整尺寸以保持宽高比
        ///     Mat resized = CvDataProcessor.Resize(
        ///         image, 
        ///         new Size(640, 640), 
        ///         ImageResizeMode.Pad);
        ///     
        ///     Cv2.ImShow("Resized", resized);
        ///     Cv2.WaitKey(0);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ImageResizeMode"/>
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
                    // 1. 默认填充颜色为中灰色
                    Scalar gray = new Scalar(128, 128, 128);
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
        /// Normalizes image with mean subtraction and scaling (ImageNet-style normalization).
        /// 使用均值减除和缩放标准化图像(ImageNet风格归一化)。
        /// </summary>
        /// <param name="im">Source image (3-channel) / 源图像(3通道)</param>
        /// <param name="mean">Channel means [R,G,B] or [B,G,R] / 通道均值 [R,G,B] 或 [B,G,R]</param>
        /// <param name="scale">Channel scales [R,G,B] or [B,G,R] / 通道缩放 [R,G,B] 或 [B,G,R]</param>
        /// <param name="isScale">Whether to apply 0-1 scaling before normalization / 是否在归一化前应用0-1缩放</param>
        /// <returns>Normalized float array in NCHW format / NCHW格式的归一化浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when im, mean, or scale is null / 当im、mean或scale为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image doesn't have 3 channels / 当图像没有3通道时抛出</exception>
        /// <remarks>
        /// <para>
        /// Formula: output = (input / 255.0 - mean) / scale
        /// 公式: output = (input / 255.0 - mean) / scale
        /// </para>
        /// <para>
        /// Uses parallel processing for better performance on large images.
        /// 对大图像使用并行处理以提高性能。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // ImageNet normalization
        /// // ImageNet归一化
        /// float[] mean = { 0.485f, 0.456f, 0.406f };
        /// float[] std = { 0.229f, 0.224f, 0.225f };
        /// 
        /// float[] normalized = CvDataProcessor.Normalize(image, mean, std, true);
        /// </code>
        /// </example>
        /// <seealso cref="Normalize(Mat, ImageNormalizationType, NormalizationParams)"/>
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
            }
            finally
            {
                resultHandle.Free();
            }
            return res;
        }

        /// <summary>
        /// Applies basic normalization (0-1 scaling or no scaling).
        /// 应用基础归一化(0-1缩放或不处理)。
        /// </summary>
        /// <param name="im">Source image / 源图像</param>
        /// <param name="isScale">Whether to scale to 0-1 range / 是否缩放到0-1范围</param>
        /// <returns>Float array in NCHW format / NCHW格式的浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when im is null / 当im为null时抛出</exception>
        /// <remarks>
        /// Simple normalization for models that don't require mean subtraction.
        /// 用于不需要均值减除的模型的简单归一化。
        /// </remarks>
        /// <example>
        /// <code>
        /// // Simple 0-1 normalization
        /// // 简单的0-1归一化
        /// float[] normalized = CvDataProcessor.Normalize(image, true);
        /// </code>
        /// </example>
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
            }
            finally
            {
                resultHandle.Free();
            }
            return res;
        }

        /// <summary>
        /// Normalizes image using specified normalization scheme.
        /// 使用指定的归一化方案标准化图像。
        /// </summary>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="type">Normalization type / 归一化类型</param>
        /// <param name="customParams">Custom parameters when type is CustomStandard / 当类型为CustomStandard时的自定义参数</param>
        /// <returns>Normalized float array in NCHW format / NCHW格式的归一化浮点数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null / 当图像为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image format is invalid / 当图像格式无效时抛出</exception>
        /// <remarks>
        /// <para>
        /// Predefined normalization schemes:
        /// 预定义的归一化方案:
        /// - Scale_0_1: Simple 0-1 scaling / 简单的0-1缩放
        /// - ImageNetStandard: ImageNet mean/std normalization / ImageNet均值/标准差归一化
        /// - CustomStandard: User-defined mean and std / 用户自定义均值和标准差
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Use ImageNet normalization
        /// // 使用ImageNet归一化
        /// float[] normalized = CvDataProcessor.Normalize(
        ///     image, 
        ///     ImageNormalizationType.ImageNetStandard);
        /// 
        /// // Use custom normalization
        /// // 使用自定义归一化
        /// var customParams = new NormalizationParams 
        /// { 
        ///     Mean = new[] { 0.5f, 0.5f, 0.5f },
        ///     Std = new[] { 0.5f, 0.5f, 0.5f }
        /// };
        /// float[] normalized2 = CvDataProcessor.Normalize(
        ///     image, 
        ///     ImageNormalizationType.CustomStandard, 
        ///     customParams);
        /// </code>
        /// </example>
        /// <seealso cref="ImageNormalizationType"/>
        /// <seealso cref="NormalizationParams"/>
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
                case ImageNormalizationType.Scale_Neg05_05:
                case ImageNormalizationType.ImageNetStandard:
                case ImageNormalizationType.CustomStandard:
                    return Normalize(image, parameters.Mean, parameters.Std, true);

                default:
                    return Normalize(image, false);
            }
        }
    }
}
