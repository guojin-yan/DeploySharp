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
    /// YOLOv13 object detection model implementation.
    /// YOLOv13目标检测模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv13 represents the latest evolution in the YOLO series with state-of-the-art
    /// architecture improvements for superior detection performance.
    /// YOLOv13代表了YOLO系列中的最新演进，具有最先进的架构改进，可实现卓越的检测性能。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - State-of-the-art detection architecture
    ///   最先进的检测架构
    /// - Superior accuracy-speed trade-off
    ///   卓越的准确性和速度平衡
    /// - Advanced multi-scale feature fusion
    ///   先进的多尺度特征融合
    /// - Optimized for various deployment scenarios
    ///   针对各种部署场景优化
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create YOLOv13 detector
    /// // 创建YOLOv13检测器
    /// var config = new Yolov13DetConfig("yolov13n.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var detector = new Yolov13DetModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("street.jpg"))
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
    /// <seealso cref="Yolov13DetConfig"/>
    public class Yolov13DetModel : IYolov13DetModel
    {
        /// <summary>
        /// Creates a new YOLOv13 detection model instance.
        /// 创建新的YOLOv13检测模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not Yolov13DetConfig / 当config不是Yolov13DetConfig时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and NMS threshold.
        /// 配置指定模型路径、输入尺寸、置信度阈值和NMS阈值。
        /// </remarks>
        public Yolov13DetModel(Yolov13DetConfig config) : base(config) { }

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
        /// Preprocesses image for YOLOv13 detection inference.
        /// 对图像进行预处理以进行YOLOv13检测推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Letterbox resize to 640x640 (maintaining aspect ratio with padding)
        ///    Letterbox调整到640x640(保持宽高比并填充)
        /// 2. Normalize pixel values to 0-1 range
        ///    将像素值归一化到0-1范围
        /// 3. Convert BGR to RGB
        ///    将BGR转换为RGB
        /// 4. Convert to NCHW tensor format
        ///    转换为NCHW张量格式
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
        /// Preprocesses batch of images for YOLOv13 detection inference.
        /// 对批量图像进行预处理以进行YOLOv13检测推理。
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
