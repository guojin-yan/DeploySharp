using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// RFDETR (Real-time Feature Detection with Transformers) object detection model implementation.
    /// RFDETR(实时特征检测与变换器)目标检测模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFDETR combines the power of transformer architectures with real-time detection requirements,
    /// offering strong performance for complex scene understanding.
    /// RFDETR将变换器架构的强大功能与实时检测需求相结合，为复杂场景理解提供强大性能。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Transformer-based detection architecture
    ///   基于变换器的检测架构
    /// - Global context understanding
    ///   全局上下文理解
    /// - Real-time performance
    ///   实时性能
    /// - Excellent for complex scenes
    ///   对复杂场景表现出色
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create RFDETR detector
    /// // 创建RFDETR检测器
    /// var config = new RFDETRDetConfig("rfdetr.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var detector = new RFDETRDetModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("scene.jpg"))
    ///     {
    ///         var results = detector.Predict(image);
    ///         
    ///         foreach (var det in results)
    ///         {
    ///             Console.WriteLine($"{det.Category}: {det.Confidence:P}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="RFDETRDetConfig"/>
    /// <seealso cref="RFDETRSegModel"/>
    public class RFDETRDetModel : IRFDETRDetModel
    {
        /// <summary>
        /// Creates a new RFDETR detection model instance.
        /// 创建新的RFDETR检测模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and other parameters.
        /// 配置指定模型路径、输入尺寸、置信度阈值和其他参数。
        /// </remarks>
        public RFDETRDetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Performs object detection on a single image.
        /// 对单张图像执行目标检测。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of detection results with bounding boxes, classes, and confidence scores / 包含边界框、类别和置信度分数的检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no objects are detected above the confidence threshold.
        /// 如果没有检测到高于置信度阈值的目标，则返回空数组。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("photo.jpg"))
        /// {
        ///     var detections = detector.Predict(image);
        ///     
        ///     foreach (var det in detections)
        ///     {
        ///         Cv2.Rectangle(image, CvDataExtensions.ToRect(det.Bounds), Scalar.Red, 2);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public DetResult[] Predict(Mat img)
        {
            return base.Predict(img) as DetResult[];
        }

        /// <summary>
        /// Performs batch object detection on multiple images.
        /// 对多张图像执行批量目标检测。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of detection results for each image / 每张图像的检测结果列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        /// <remarks>
        /// Batch processing is more efficient than sequential single-image processing.
        /// 批处理比顺序单张图像处理更高效。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt; { image1, image2, image3 };
        /// var allResults = detector.PredictBatch(images);
        /// 
        /// for (int i = 0; i &lt; allResults.Count; i++)
        /// {
        ///     Console.WriteLine($"Image {i}: {allResults[i].Length} objects detected");
        /// }
        /// </code>
        /// </example>
        public List<DetResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<DetResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for RFDETR detection inference.
        /// 对图像进行预处理以进行RFDETR检测推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Resize to model input size
        ///    调整到模型输入尺寸
        /// 2. Normalize pixel values
        ///    归一化像素值
        /// 3. Convert to tensor format
        ///    转换为张量格式
        /// </para>
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");

            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Mat)img,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images for RFDETR detection inference.
        /// 对批量图像进行预处理以进行RFDETR检测推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");

            try
            {
                return CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }
    }
}
