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
    /// Provides image processing utilities for computer vision tasks using ImageSharp
    /// 使用ImageSharp提供计算机视觉任务的图像处理工具
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles essential image preprocessing operations required before model inference:
    /// 处理模型推理前所需的必要图像预处理操作:
    /// - Resizing with various modes (Stretch, Pad, Max, Crop)
    ///   多种模式调整尺寸(拉伸、填充、最大、裁剪)
    /// - Normalization (multiple schemes: Scale_0_1, ImageNetStandard, CustomStandard)
    ///   标准化(多种方案: 0-1缩放、ImageNet标准、自定义标准)
    /// - Tensor conversion for model input
    ///   模型输入的张量转换
    /// </para>
    /// <para>
    /// Optimized implementations leveraging parallelism for large images.
    /// All methods preserve the original image and return processed copies.
    /// 针对大图像利用并行化的优化实现。
    /// 所有方法都保留原始图像并返回处理后的副本。
    /// </para>
    /// <para>
    /// This class is thread-safe for read operations. Concurrent modifications to the same
    /// image instance should be avoided.
    /// 此类对读取操作是线程安全的。应避免对同一图像实例进行并发修改。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Load image and prepare for inference
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// 
    /// // Configure preprocessing
    /// var processorConfig = new DataProcessorConfig
    /// {
    ///     ResizeMode = ImageResizeMode.Pad,
    ///     NormalizationType = ImageNormalizationType.ImageNetStandard
    /// };
    /// 
    /// // Process to tensor
    /// float[] tensor = CvDataProcessor.ProcessToFloat(
    ///     image, 
    ///     new Size(640, 640), 
    ///     processorConfig);
    /// </code>
    /// </example>
    /// <seealso cref="DataProcessorConfig"/>
    /// <seealso cref="ImageNormalizationType"/>
    /// <seealso cref="ImageResizeMode"/>
    public static class CvDataProcessor
    {
        /// <summary>
        /// Processes a single image into DataTensor format for model inference
        /// 将单张图像处理为模型推理所需的DataTensor格式
        /// </summary>
        /// <param name="img">Input RGB image / 输入RGB图像</param>
        /// <param name="config">Model configuration containing input size and preprocessing parameters / 包含输入大小和预处理参数的模型配置</param>
        /// <param name="imageAdjustmentParam">
        /// Output parameter containing image adjustment information for post-processing / 
        /// 包含后处理图像调整信息的输出参数
        /// </param>
        /// <returns>Processed tensor data ready for model inference / 准备进行模型推理的处理后张量数据</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input image or config is null
        /// 当输入图像或配置为null时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when processing fails due to invalid configuration or image format
        /// 当处理因无效配置或图像格式失败时抛出
        /// </exception>
        /// <remarks>
        /// This is the primary entry point for single-image preprocessing.
        /// It combines resizing, normalization, and tensor construction in one operation.
        /// 这是单张图像预处理的主要入口点。
        /// 它将调整大小、标准化和张量构建结合在一个操作中。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// using var image = Image.Load&lt;Rgb24&gt;("photo.jpg");
        /// var config = new Yolov8DetConfig("model.onnx");
        /// 
        /// var tensor = CvDataProcessor.ImageProcessToDataTensor(
        ///     image, 
        ///     config, 
        ///     out var adjustment);
        /// 
        /// // Use tensor for inference
        /// var results = model.Infer(tensor);
        /// 
        /// // Adjust results back to original image coordinates
        /// // 将结果调整回原始图像坐标
        /// foreach (var result in results)
        /// {
        ///     var originalBounds = adjustment.AdjustBox(result.Bounds);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ImageAdjustmentParam"/>
        /// <seealso cref="DataTensor"/>
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
        /// Processes a batch of images into DataTensor format for batch inference
        /// 将一批图像处理为批量推理所需的DataTensor格式
        /// </summary>
        /// <param name="imgs">List of input RGB images / 输入RGB图像列表</param>
        /// <param name="config">Model configuration / 模型配置</param>
        /// <param name="imageAdjustmentParams">
        /// Output array of adjustment parameters for each image / 
        /// 每张图像的调整参数输出数组
        /// </param>
        /// <returns>Processed tensor data for batch inference / 用于批量推理的处理后张量数据</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when imgs or config is null
        /// 当imgs或config为null时抛出
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when imgs is empty
        /// 当imgs为空时抛出
        /// </exception>
        /// <remarks>
        /// Processes all images with the same configuration, stacking them into a batch tensor.
        /// Each image is processed independently and results are concatenated along the batch dimension.
        /// 使用相同配置处理所有图像，将它们堆叠成批量张量。
        /// 每张图像独立处理，结果沿批次维度连接。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var images = new List&lt;Image&lt;Rgb24&gt;&gt;
        /// {
        ///     Image.Load&lt;Rgb24&gt;("image1.jpg"),
        ///     Image.Load&lt;Rgb24&gt;("image2.jpg"),
        ///     Image.Load&lt;Rgb24&gt;("image3.jpg")
        /// };
        /// 
        /// var batchTensor = CvDataProcessor.ImageListProcessToDataTensor(
        ///     images, 
        ///     config, 
        ///     out var adjustments);
        /// 
        /// // Batch inference
        /// var batchResults = model.InferBatch(batchTensor);
        /// </code>
        /// </example>
        /// <seealso cref="ImageProcessToDataTensor"/>
        public static DataTensor ImageListProcessToDataTensor(List<Image<Rgb24>> imgs, IConfig config, out ImageAdjustmentParam[] imageAdjustmentParams)
        {
            int inputSize = config.InputSizes[0][2];

            MyLogger.Log.Debug($"配置输入尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
                              $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");
            List<float[]> normalizedDatas = new List<float[]>();
            List<ImageAdjustmentParam> imageAdjustmentParamList = new List<ImageAdjustmentParam>();
            int dataLength = 0;
            // 记录归一化处理开始
            MyLogger.Log.Debug("开始图像归一化处理 (0-255 to 0-1)...");
            for (int i = 0; i < imgs.Count; i++)
            {
                Image < Rgb24 > img = imgs[i];
                float[] normalizedData = CvDataProcessor.ProcessToFloat(
                img,
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                ((IImgConfig)config).DataProcessor);

                dataLength += normalizedData.Length;
                normalizedDatas.Add(normalizedData);

                // 创建图像调整参数
                imageAdjustmentParamList.Add(ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(img.Size()),
                ((IImgConfig)config).DataProcessor.ResizeMode));

                 MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                             $"原始尺寸: {img.Size()}, " +
                             $"目标尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][3]}, " +
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
        /// Complete preprocessing pipeline: resizes image then normalizes pixel values
        /// 完整的预处理流程: 先调整图像尺寸，然后归一化像素值
        /// </summary>
        /// <param name="input">Input image as Image&lt;Rgb24&gt; / 输入图像，类型为Image&lt;Rgb24&gt;</param>
        /// <param name="size">Target size for resizing / 调整大小的目标尺寸</param>
        /// <param name="processorConfig">Configuration specifying resize mode and normalization / 指定调整大小模式和归一化的配置</param>
        /// <returns>Normalized float array in CHW format (channels first) / CHW格式(通道优先)的归一化浮点数组</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input, size, or processorConfig is null
        /// 当input、size或processorConfig为null时抛出
        /// </exception>
        /// <remarks>
        /// Output format: CHW (Channel-Height-Width) with channels in RGB order.
        /// Data is normalized according to the specified normalization type.
        /// 输出格式: CHW(通道-高度-宽度)，通道按RGB顺序。
        /// 数据根据指定的归一化类型进行归一化。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var processorConfig = new DataProcessorConfig
        /// {
        ///     ResizeMode = ImageResizeMode.Pad,
        ///     NormalizationType = ImageNormalizationType.ImageNetStandard
        /// };
        /// 
        /// float[] tensor = CvDataProcessor.ProcessToFloat(
        ///     image, 
        ///     new Size(640, 640), 
        ///     processorConfig);
        /// 
        /// // tensor shape: [3, 640, 640] for RGB image
        /// </code>
        /// </example>
        /// <seealso cref="Resize(Image{Rgb24}, Size, ImageResizeMode)"/>
        /// <seealso cref="Normalize(Image{Rgb24}, ImageNormalizationType, NormalizationParams)"/>
        public static float[] ProcessToFloat(object input, Size size, DataProcessorConfig processorConfig)
        {
            return Normalize(Resize((Image<Rgb24>)input, size, processorConfig.ResizeMode), processorConfig.NormalizationType, processorConfig.CustomNormalizationParams);
        }


        /// <summary>
        /// Resizes image using specified mode with high-quality Lanczos3 interpolation
        /// 使用指定模式调整图像尺寸，采用高质量Lanczos3插值
        /// </summary>
        /// <param name="image">Source image to resize / 要调整大小的源图像</param>
        /// <param name="size">Target dimensions / 目标尺寸</param>
        /// <param name="resizeMode">Resizing strategy / 尺寸调整策略</param>
        /// <returns>Resized image (new instance) / 调整大小后的图像(新实例)</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image is null
        /// 当image为null时抛出
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when resizeMode is not a valid value
        /// 当resizeMode不是有效值时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Available resize modes:
        /// 可用的调整大小模式:
        /// - Stretch: Distorts image to exactly fit target dimensions
        ///   拉伸: 扭曲图像以精确适应目标尺寸
        /// - Pad: Scales to fit while maintaining aspect ratio, pads with black
        ///   填充: 保持宽高比缩放，用黑色填充
        /// - Max: Scales down if larger than target, no upscaling
        ///   最大: 如果大于目标则缩小，不放大
        /// - Crop: Scales to cover target then crops excess
        ///   裁剪: 缩放以覆盖目标然后裁剪多余部分
        /// </para>
        /// <para>
        /// Uses Lanczos3 resampling for high-quality results.
        /// 使用Lanczos3重采样以获得高质量结果。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Pad mode - maintains aspect ratio
        /// var padded = CvDataProcessor.Resize(
        ///     image, 
        ///     new Size(640, 640), 
        ///     ImageResizeMode.Pad);
        /// 
        /// // Stretch mode - exact dimensions
        /// var stretched = CvDataProcessor.Resize(
        ///     image, 
        ///     new Size(640, 640), 
        ///     ImageResizeMode.Stretch);
        /// </code>
        /// </example>
        /// <seealso cref="ImageResizeMode"/>
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
        /// Normalizes image with mean subtraction and per-channel scaling using parallel processing
        /// 使用均值减除和逐通道缩放对图像进行归一化，采用并行处理
        /// </summary>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="mean">Channel means [R, G, B] for subtraction / 用于减除的通道均值 [R, G, B]</param>
        /// <param name="scale">Channel scales [R, G, B] for multiplication / 用于乘法的通道缩放 [R, G, B]</param>
        /// <param name="isScale">Whether to apply 0-1 scaling before normalization / 是否先应用0-1缩放</param>
        /// <returns>Normalized float array in CHW format / CHW格式的归一化浮点数组</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image, mean, or scale is null
        /// 当image、mean或scale为null时抛出
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when mean or scale arrays don't have exactly 3 elements
        /// 当mean或scale数组不是正好3个元素时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Normalization formula when isScale is true:
        /// 当isScale为true时的归一化公式:
        ///     output = (input / 255.0 - mean) * scale
        /// </para>
        /// <para>
        /// Uses parallel processing with Parallel.For for better performance on large images.
        /// 对大图像使用Parallel.For并行处理以提高性能。
        /// </para>
        /// <para>
        /// Output is in CHW format: all R values first, then all G, then all B.
        /// 输出为CHW格式: 先所有R值，然后所有G，然后所有B。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // ImageNet normalization
        /// float[] mean = { 0.485f, 0.456f, 0.406f };
        /// float[] std = { 0.229f, 0.224f, 0.225f };
        /// 
        /// float[] normalized = CvDataProcessor.Normalize(
        ///     image, 
        ///     mean, 
        ///     std, 
        ///     isScale: true);
        /// </code>
        /// </example>
        /// <seealso cref="Normalize(Image{Rgb24}, ImageNormalizationType, NormalizationParams)"/>
        public static float[] Normalize(Image<Rgb24> image, float[] mean, float[] scale, bool isScale)
        {
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

            return result;

        }
        /// <summary>
        /// Applies basic 0-1 or no normalization
        /// 应用基础的0-1标准化或不处理
        /// </summary>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="isScale">If true, scales to 0-1 range; if false, keeps 0-255 range / 如果为true，缩放到0-1范围；如果为false，保持0-255范围</param>
        /// <returns>Float array in CHW format / CHW格式的浮点数组</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image is null
        /// 当image为null时抛出
        /// </exception>
        /// <remarks>
        /// Simple wrapper around ImageToFloatArray for basic normalization scenarios.
        /// 针对基础归一化场景的ImageToFloatArray简单包装。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Scale to 0-1
        /// float[] scaled = CvDataProcessor.Normalize(image, true);
        /// 
        /// // Keep 0-255
        /// float[] unscaled = CvDataProcessor.Normalize(image, false);
        /// </code>
        /// </example>
        /// <seealso cref="ImageToFloatArray"/>
        public static float[] Normalize(Image<Rgb24> image, bool isScale)
        {
            return ImageToFloatArray(image, isScale);
        }
        /// <summary>
        /// Converts image to float array with optional scaling using parallel processing
        /// 将图像转换为浮点数组，可选项是否缩放，采用并行处理
        /// </summary>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="normalize">If true, scales pixel values by 1/255; if false, keeps original values / 如果为true，将像素值缩放1/255；如果为false，保持原始值</param>
        /// <returns>Float array in CHW format [R..., G..., B...] / CHW格式的浮点数组 [R..., G..., B...]</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image is null
        /// 当image为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Output layout: CHW (Channel-Height-Width)
        /// 输出布局: CHW (通道-高度-宽度)
        /// - Indices [0, pixelCount): Red channel values
        ///   索引 [0, pixelCount): 红色通道值
        /// - Indices [pixelCount, 2*pixelCount): Green channel values
        ///   索引 [pixelCount, 2*pixelCount): 绿色通道值
        /// - Indices [2*pixelCount, 3*pixelCount): Blue channel values
        ///   索引 [2*pixelCount, 3*pixelCount): 蓝色通道值
        /// </para>
        /// <para>
        /// Uses Parallel.For for efficient processing of large images.
        /// 使用Parallel.For高效处理大图像。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// float[] tensor = CvDataProcessor.ImageToFloatArray(image, true);
        /// 
        /// // Access pixel (x, y) for each channel
        /// int idx = y * width + x;
        /// float r = tensor[idx];
        /// float g = tensor[idx + pixelCount];
        /// float b = tensor[idx + 2 * pixelCount];
        /// </code>
        /// </example>
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
        /// Normalizes image using predefined normalization schemes
        /// 使用预定义的归一化方案对图像进行归一化
        /// </summary>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="type">Normalization type from predefined schemes / 预定义方案中的归一化类型</param>
        /// <param name="customParams">
        /// Custom parameters when type is CustomStandard; ignored otherwise / 
        /// 当类型为CustomStandard时的自定义参数；其他情况忽略
        /// </param>
        /// <returns>Normalized float array / 归一化后的浮点数组</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image is null
        /// 当image为null时抛出
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when type is CustomStandard but customParams is null
        /// 当类型为CustomStandard但customParams为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Supported normalization types:
        /// 支持的归一化类型:
        /// - Scale_0_1: Simple division by 255
        ///   Scale_0_1: 简单除以255
        /// - Scale_Neg1_1: Scale to [-1, 1] range (currently returns null)
        ///   Scale_Neg1_1: 缩放到[-1, 1]范围(目前返回null)
        /// - ImageNetStandard: Mean=[0.485, 0.456, 0.406], Std=[0.229, 0.224, 0.225]
        ///   ImageNetStandard: 均值=[0.485, 0.456, 0.406], 标准差=[0.229, 0.224, 0.225]
        /// - CustomStandard: User-defined mean and std via customParams
        ///   CustomStandard: 通过customParams定义用户指定的均值和标准差
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // ImageNet normalization (standard for many pretrained models)
        /// float[] imagenet = CvDataProcessor.Normalize(
        ///     image, 
        ///     ImageNormalizationType.ImageNetStandard);
        /// 
        /// // Simple 0-1 scaling
        /// float[] scaled = CvDataProcessor.Normalize(
        ///     image, 
        ///     ImageNormalizationType.Scale_0_1);
        /// 
        /// // Custom normalization
        /// var custom = new NormalizationParams 
        /// { 
        ///     Mean = new[] { 0.5f, 0.5f, 0.5f },
        ///     Std = new[] { 0.5f, 0.5f, 0.5f }
        /// };
        /// float[] customNorm = CvDataProcessor.Normalize(
        ///     image, 
        ///     ImageNormalizationType.CustomStandard, 
        ///     custom);
        /// </code>
        /// </example>
        /// <seealso cref="ImageNormalizationType"/>
        /// <seealso cref="NormalizationParams"/>
        /// <seealso cref="NormalizationParamsFactory"/>
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
