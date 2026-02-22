using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DeploySharp.Model
{
    /// <summary>
    /// PaddleOCR text detection model (DBNet-based) implementation.
    /// PaddleOCR文本检测模型(基于DBNet)实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the detection stage of PaddleOCR using DBNet (Differentiable Binarization)
    /// for text region detection in images.
    /// 此类使用DBNet(可微分二值化)实现PaddleOCR的检测阶段，用于图像中的文本区域检测。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Dynamic input size adjustment based on image dimensions
    ///   基于图像尺寸的动态输入大小调整
    /// - Supports batch processing for multiple images
    ///   支持多张图像的批处理
    /// - Optimized contour-based text region extraction
    ///   优化的基于轮廓的文本区域提取
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create text detection model
    /// // 创建文本检测模型
    /// var config = new PPOcrDetConfig("ch_PP-OCRv4_det.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     DBBoxThresh = 0.6f,
    ///     DBUnclipRatio = 2.0f,
    ///     LimitInputSize = 960
    /// };
    /// 
    /// using (var detector = new PPOcrDet(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("document.jpg"))
    ///     {
    ///         // Detect text regions
    ///         // 检测文本区域
    ///         ObbResult[] results = detector.Predict(image);
    ///         
    ///         foreach (var region in results)
    ///         {
    ///             Console.WriteLine($"Text region: {region.Bounds}, Score: {region.Confidence}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PPOcrDetConfig"/>
    /// <seealso cref="CvPPOcrDataProcessor"/>
    public class PPOcrDet : IPPOcrDet
    {
        private const int DefaultChannels = 3;
        private const int AlignBase = 32;

        /// <summary>
        /// Creates a new PaddleOCR detection model instance.
        /// 创建新的PaddleOCR检测模型实例。
        /// </summary>
        /// <param name="config">Detection model configuration / 检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not PPOcrDetConfig / 当config不是PPOcrDetConfig时抛出</exception>
        /// <remarks>
        /// The configuration must be of type PPOcrDetConfig with proper model path.
        /// 配置必须是PPOcrDetConfig类型，并具有正确的模型路径。
        /// </remarks>
        public PPOcrDet(IConfig config) : base(config)
        {
            // Type safety check
            if (!(config is PPOcrDetConfig))
                throw new InvalidCastException($"PPOcrDet must be initialized with {nameof(PPOcrDetConfig)}");
        }

        /// <summary>
        /// Performs text detection on a single image.
        /// 对单张图像执行文本检测。
        /// </summary>
        /// <param name="img">Input image containing text / 包含文本的输入图像</param>
        /// <returns>Array of detected text regions (as oriented bounding boxes) / 检测到的文本区域数组(作为有向边界框)</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Results are returned as ObbResult (oriented bounding box) to handle rotated text.
        /// 结果以ObbResult(有向边界框)返回，以处理旋转的文本。
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = detector.Predict(image);
        /// 
        /// // Draw detected regions
        /// // 绘制检测到的区域
        /// foreach (var result in results)
        /// {
        ///     var rect = CvDataExtensions.ToRotatedRect(result.Bounds);
        ///     Point2f[] points = rect.Points();
        ///     for (int i = 0; i &lt; 4; i++)
        ///     {
        ///         Cv2.Line(image, (Point)points[i], (Point)points[(i+1)%4], Scalar.Green, 2);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public ObbResult[] Predict(Mat img)
        {
            // Base class call
            return base.Predict(img) as ObbResult[];
        }

        /// <summary>
        /// Performs batch text detection on multiple images.
        /// 对多张图像执行批量文本检测。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of detection results for each image / 每张图像的检测结果列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        /// <remarks>
        /// Batch processing can improve throughput for multiple images.
        /// 批处理可以提高多张图像的吞吐量。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt; { image1, image2, image3 };
        /// var results = detector.PredictBatch(images);
        /// </code>
        /// </example>
        public List<ObbResult[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
                return new List<ObbResult[]>();

            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<ObbResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses a single image for text detection inference.
        /// 对单张图像进行预处理以进行文本检测推理。
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed tensor data / 预处理后的张量数据</returns>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Dynamic size calculation (aligns to 32-pixel multiples)
        ///    动态尺寸计算(对齐到32像素倍数)
        /// 2. Resize with padding if needed
        ///    如需要则使用填充调整尺寸
        /// 3. Normalize pixel values
        ///    归一化像素值
        /// 4. Convert to tensor format
        ///    转换为张量格式
        /// </para>
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            // Single image optimization path is kept to avoid List allocation overhead
            var detConfig = (PPOcrDetConfig)config;
            var mat = (Mat)img;

            try
            {
                // Dynamic size calculation
                if (config.DynamicByInput)
                {
                    int targetSize = CalculateDetTargetSize(mat.Width, mat.Height, detConfig.LimitInputSize);
                    UpdateInputShape(1, targetSize);
                }

                return CvDataProcessor.ImageProcessToDataTensor(mat, config, out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = new ImageAdjustmentParam(); // Exception safety
                MyLogger.Log.Error($"PPOcrDet Preprocess Exception: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses a batch of images for text detection inference.
        /// 对批量图像进行预处理以进行文本检测推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var detConfig = (PPOcrDetConfig)config;
            try
            {
                int batchSize = imgs.Count;

                if (config.DynamicByInput)
                {
                    int maxSideInBatch = imgs.Max(img => Math.Max(((Mat)img).Rows, ((Mat)img).Cols));
                    int targetSize = CalculateDetTargetSize(maxSideInBatch, maxSideInBatch, detConfig.LimitInputSize);
                    UpdateInputShape(batchSize, targetSize);
                }

                var matList = imgs.Cast<Mat>().ToList();
                return CvDataProcessor.ImageListProcessToDataTensor(matList, config, out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = null;
                MyLogger.Log.Error($"PPOcrDet PreprocessBatch Exception: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Postprocesses model output to extract text regions.
        /// 对模型输出进行后处理以提取文本区域。
        /// </summary>
        /// <param name="dataTensor">Model output tensor / 模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image adjustment parameters for coordinate mapping / 用于坐标映射的图像调整参数</param>
        /// <returns>Array of detected text regions / 检测到的文本区域数组</returns>
        /// <remarks>
        /// <para>
        /// Postprocessing steps:
        /// 后处理步骤：
        /// 1. Convert probability map to binary bitmap
        ///    将概率图转换为二值位图
        /// 2. Find contours from bitmap
        ///    从位图查找轮廓
        /// 3. Calculate confidence scores
        ///    计算置信度分数
        /// 4. Expand boxes using unclip
        ///    使用unclip膨胀框
        /// 5. Map coordinates back to original image
        ///    将坐标映射回原始图像
        /// </para>
        /// </remarks>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            var detConfig = (PPOcrDetConfig)config;

            // Get tensor shape [Batch, Channel, Height, Width]
            int h = dataTensor[0].Shape[2];
            int w = dataTensor[0].Shape[3];

            using (Mat predMap = Mat.FromPixelData(h, w, MatType.CV_32FC1, dataTensor[0].DataBuffer))
            using (Mat bitMap = new Mat())
            {
                // 1. Convert probability map (0.0 - 1.0) to grayscale (0 - 255)
                Cv2.ConvertScaleAbs(predMap, bitMap, 255.0, 0);

                // 2. Binarization (apply threshold)
                double threshValue = detConfig.ConfidenceThreshold * 255.0;
                Cv2.Threshold(bitMap, bitMap, threshValue, 255, ThresholdTypes.Binary);

                // 3. Extract boxes
                var boxes = CvPPOcrDataProcessor.BoxesFromBitmap(
                    predMap,
                    bitMap,
                    detConfig.DBBoxThresh,
                    detConfig.DBUnclipRatio,
                    detConfig.DBScoreMode);

                var ocrResults = new List<ObbResult>(boxes.Count);

                foreach (var boxItem in boxes)
                {
                    ocrResults.Add(new ObbResult
                    {
                        Type = ResultType.OcrResult,
                        Id = 0,
                        Confidence = boxItem.Item2,
                        Bounds = imageAdjustmentParam.AdjustRotatedRect(CvDataExtensions.ToCvRotatedRect(boxItem.Item1))
                    });
                }

                return ocrResults.ToArray();
            }
        }

        /// <summary>
        /// Calculates dynamic input size for detection model, aligned to multiples of 32.
        /// 计算检测模型的动态输入尺寸，需对齐到32的倍数。
        /// </summary>
        /// <param name="width">Image width / 图像宽度</param>
        /// <param name="height">Image height / 图像高度</param>
        /// <param name="limitSize">Maximum allowed size / 最大允许尺寸</param>
        /// <returns>Calculated target size / 计算的目标尺寸</returns>
        /// <remarks>
        /// DBNet models typically require input sizes that are multiples of 32.
        /// DBNet模型通常需要32倍数的输入尺寸。
        /// </remarks>
        private int CalculateDetTargetSize(int width, int height, int limitSize)
        {
            int maxSide = Math.Max(width, height);

            if (maxSide > limitSize)
            {
                return limitSize;
            }

            // Round down to multiple of 32 (bitwise optimization: x & ~31)
            int alignedSize = maxSide & ~(AlignBase-1);

            // Defensive check: prevent size 0 for very small images
            return Math.Max(alignedSize, AlignBase);
        }

        /// <summary>
        /// Updates input shape in configuration.
        /// 更新配置中的输入形状。
        /// </summary>
        /// <param name="batchSize">Batch size / 批处理大小</param>
        /// <param name="targetSize">Target input size / 目标输入尺寸</param>
        /// <remarks>
        /// NCHW format: [Batch, Channel, Height, Width]
        /// NCHW格式: [Batch, Channel, Height, Width]
        /// </remarks>
        private void UpdateInputShape(int batchSize, int targetSize)
        {
            config.InputSizes.Clear();
            config.InputSizes.Add(new int[] { batchSize, DefaultChannels, targetSize, targetSize });
        }
    }
}
