using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeploySharp.Model
{
    /// <summary>
    /// PaddleOCR text direction classification model implementation.
    /// PaddleOCR文本方向分类模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the classification stage of PaddleOCR,
    /// which determines if text needs to be rotated 180 degrees for correct recognition.
    /// 此类实现PaddleOCR的分类阶段，确定文本是否需要旋转180度以进行正确识别。
    /// </para>
    /// <para>
    /// The classifier typically uses a lightweight MobileNet architecture
    /// for fast inference on mobile and edge devices.
    /// 分类器通常使用轻量级的MobileNet架构，用于在移动和边缘设备上快速推理。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create direction classifier
    /// // 创建方向分类器
    /// var config = new PPOcrClsConfig("ch_ppocr_mobile_v2.0_cls.onnx")
    /// {
    ///     ConfidenceThreshold = 0.9f
    /// };
    /// 
    /// using (var classifier = new PPOcrCls(config))
    /// {
    ///     using (Mat textImage = Cv2.ImRead("text.jpg"))
    ///     {
    ///         // Classify text direction
    ///         // 分类文本方向
    ///         var result = classifier.Predict(textImage);
    ///         
    ///         // result[0].Id: 0 = normal, 1 = rotated 180 degrees
    ///         // result[0].Id: 0 = 正常, 1 = 旋转180度
    ///         if (result[0].Id == 1)
    ///         {
    ///             Cv2.Rotate(textImage, textImage, RotateFlags.Rotate180);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PPOcrClsConfig"/>
    public class PPOcrCls : IPPOcrCls
    {
        /// <summary>
        /// Creates a new text direction classifier instance.
        /// 创建新的文本方向分类器实例。
        /// </summary>
        /// <param name="config">Classifier configuration / 分类器配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not PPOcrClsConfig / 当config不是PPOcrClsConfig时抛出</exception>
        public PPOcrCls(IConfig config) : base(config)
        {
            // Type safety check
            if (!(config is PPOcrClsConfig))
            {
                throw new InvalidCastException($"PPOcrCls must be initialized with {nameof(PPOcrClsConfig)}");
            }
        }

        /// <summary>
        /// Classifies text direction for a single image.
        /// 对单张图像分类文本方向。
        /// </summary>
        /// <param name="img">Input text image / 输入文本图像</param>
        /// <returns>Classification result: Id=0 for normal, Id=1 for rotated / 分类结果: Id=0表示正常, Id=1表示旋转</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <remarks>
        /// Returns empty array if image is null or empty.
        /// 如果图像为null或空则返回空数组。
        /// </remarks>
        public Result[] Predict(Mat img)
        {
            if (img == null || img.Empty())
                return Array.Empty<Result>();

            return base.Predict(img) as Result[];
        }

        /// <summary>
        /// Classifies text direction for multiple images in batch.
        /// 批量分类多张图像的文本方向。
        /// </summary>
        /// <param name="imgs">List of input text images / 输入文本图像列表</param>
        /// <returns>List of classification results / 分类结果列表</returns>
        /// <remarks>
        /// Returns empty list if imgs is null or empty.
        /// 如果imgs为null或空则返回空列表。
        /// </remarks>
        public List<Result[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
            {
                return new List<Result[]>();
            }

            var objectList = imgs.Cast<object>().ToList();
            var results = base.PredictBatch(objectList);
            return results.Cast<Result[]>().ToList();
        }

        /// <summary>
        /// Preprocesses image for classification.
        /// 对图像进行预处理以进行分类。
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed tensor data / 预处理后的张量数据</returns>
        /// <remarks>
        /// Cls model typically uses fixed size (e.g., 48x192) defined in config.
        /// Cls模型通常使用配置中定义的固定尺寸(如48x192)。
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            var mat = (Mat)img;
            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    mat,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = new ImageAdjustmentParam();
                MyLogger.Log.Error($"PPOcrCls Preprocess exception: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images for classification.
        /// 对批量图像进行预处理以进行分类。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var clsConfig = (PPOcrClsConfig)config;
            try
            {
                clsConfig.InferBatch = imgs.Count;
                var matList = imgs.Cast<Mat>().ToList();

                return CvDataProcessor.ImageListProcessToDataTensor(
                    matList,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = null;
                MyLogger.Log.Error($"PPOcrCls PreprocessBatch exception: {ex.Message}", ex);
                throw;
            }
        }
    }
}
