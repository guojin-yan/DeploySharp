using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using OpenCvSharp;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeploySharp.Model
{
    /// <summary>
    /// PaddleOCR text recognition model (CRNN-based) implementation.
    /// PaddleOCR文本识别模型(基于CRNN)实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the recognition stage of PaddleOCR using CRNN
    /// (Convolutional Recurrent Neural Network) for converting text images to characters.
    /// 此类使用CRNN(卷积循环神经网络)实现PaddleOCR的识别阶段，用于将文本图像转换为字符。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Dynamic width calculation based on aspect ratio
    ///   基于宽高比的动态宽度计算
    /// - Support for variable-length text recognition
    ///   支持可变长度文本识别
    /// - Batch processing for improved throughput
    ///   批处理以提高吞吐量
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create text recognizer with character dictionary
    /// // 使用字符字典创建文本识别器
    /// var config = new PPOcrRecConfig("ch_PP-OCRv4_rec.onnx")
    /// {
    ///     LabelPath = "ppocr_keys_v1.txt",
    ///     MaxImageWidth = 1024,
    ///     InferImageHeight = 48
    /// };
    /// 
    /// using (var recognizer = new PPOcrRec(config))
    /// {
    ///     using (Mat textImage = Cv2.ImRead("text_line.jpg"))
    ///     {
    ///         // Recognize text
    ///         // 识别文本
    ///         var result = recognizer.Predict(textImage);
    ///         
    ///         Console.WriteLine($"Text: {result[0].Text}");
    ///         Console.WriteLine($"Confidence: {result[0].Confidence}");
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PPOcrRecConfig"/>
    public class PPOcrRec : IPPOcrRec
    {
        private const int DefaultImageChannels = 3;

        /// <summary>
        /// Creates a new text recognition model instance.
        /// 创建新的文本识别模型实例。
        /// </summary>
        /// <param name="config">Recognition model configuration / 识别模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not PPOcrRecConfig / 当config不是PPOcrRecConfig时抛出</exception>
        public PPOcrRec(IConfig config) : base(config)
        {
            if (!(config is PPOcrRecConfig))
            {
                throw new InvalidCastException($"PPOcrRec must be initialized with {nameof(PPOcrRecConfig)}");
            }
        }

        /// <summary>
        /// Recognizes text in a single image.
        /// 识别单张图像中的文本。
        /// </summary>
        /// <param name="img">Input text line image / 输入文本行图像</param>
        /// <returns>Recognition result containing text and confidence / 包含文本和置信度的识别结果</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <remarks>
        /// Input image should be a cropped text line (not full document).
        /// 输入图像应该是裁剪的文本行(不是完整文档)。
        /// </remarks>
        public TextRecResult[] Predict(Mat img)
        {
            return base.Predict(img) as TextRecResult[];
        }

        /// <summary>
        /// Recognizes text in multiple images in batch.
        /// 批量识别多张图像中的文本。
        /// </summary>
        /// <param name="imgs">List of input text line images / 输入文本行图像列表</param>
        /// <returns>List of recognition results / 识别结果列表</returns>
        /// <remarks>
        /// Returns empty list if imgs is null or empty.
        /// 如果imgs为null或空则返回空列表。
        /// </remarks>
        public List<TextRecResult[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
                return new List<TextRecResult[]>();

            var objectList = imgs.Cast<object>().ToList();
            var results = base.PredictBatch(objectList);
            return results.Cast<TextRecResult[]>().ToList();
        }

        /// <summary>
        /// Preprocesses image for recognition.
        /// 对图像进行预处理以进行识别。
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed tensor data / 预处理后的张量数据</returns>
        /// <remarks>
        /// Single image preprocessing delegates to batch preprocessing.
        /// 单张图像预处理委托给批处理预处理。
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            var matList = new List<object>(1) { img };
            ImageAdjustmentParam[] paramsArray;
            var data = PreprocessBatch(matList, out paramsArray);
            imageAdjustmentParam = paramsArray[0];
            return data;
        }

        /// <summary>
        /// Preprocesses batch of images for recognition.
        /// 对批量图像进行预处理以进行识别。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        /// <remarks>
        /// <para>
        /// Dynamic width calculation:
        /// 动态宽度计算：
        /// 1. Calculate max aspect ratio (width/height) in batch
        ///    计算批次中的最大宽高比
        /// 2. Determine target width based on fixed height
        ///    基于固定高度确定目标宽度
        /// 3. Clamp to maximum allowed width
        ///    钳制到最大允许宽度
        /// </para>
        /// </remarks>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var recConfig = (PPOcrRecConfig)config;
            var sw = Stopwatch.StartNew();

            try
            {
                int batchSize = imgs.Count;
                recConfig.InferBatch = batchSize;

                // Dynamic size calculation
                if (recConfig.DynamicByInput)
                {
                    float maxWhRatio = 0f;
                    for (int i = 0; i < batchSize; i++)
                    {
                        var mat = (Mat)imgs[i];
                        if (mat.Empty()) continue;
                        float ratio = mat.Width / (float)mat.Height;
                        if (ratio > maxWhRatio) maxWhRatio = ratio;
                    }

                    int targetWidth = (int)(maxWhRatio * recConfig.InferImageHeight);
                    if (targetWidth > recConfig.MaxImageWidth)
                    {
                        targetWidth = recConfig.MaxImageWidth;
                    }

                    recConfig.InputSizes.Clear();
                    recConfig.InputSizes.Add(new int[] { batchSize, DefaultImageChannels, recConfig.InferImageHeight, targetWidth });
                }

                var matList = imgs.Cast<Mat>().ToList();
                var tensor = CvDataProcessor.ImageListProcessToDataTensor(
                    matList,
                    config,
                    out imageAdjustmentParam);

                sw.Stop();
                MyLogger.Log.Debug($"Rec Preprocess Finished. Batch: {batchSize}, Time: {sw.ElapsedMilliseconds}ms");

                return tensor;
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = null;
                MyLogger.Log.Error($"PPOcrRec Preprocess Exception: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Converts Mat to byte array safely.
        /// 安全地将Mat转换为字节数组。
        /// </summary>
        /// <param name="mat">Input Mat (3-channel) / 输入Mat(3通道)</param>
        /// <returns>Byte array in BGR format / BGR格式的字节数组</returns>
        /// <remarks>
        /// Iterates through pixels to ensure correct memory layout.
        /// 遍历像素以确保正确的内存布局。
        /// </remarks>
        public byte[] MatToBytesSafe(Mat mat)
        {
            int width = mat.Cols;
            int height = mat.Rows;
            int channels = mat.Channels();
            byte[] data = new byte[width * height * channels];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vec3b color = mat.Get<Vec3b>(y, x);
                    data[index++] = color.Item0; // B
                    data[index++] = color.Item1; // G
                    data[index++] = color.Item2; // R
                }
            }
            return data;
        }
    }
}
