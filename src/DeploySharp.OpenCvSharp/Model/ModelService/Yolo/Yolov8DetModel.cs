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
    /// YOLOv8 object detection model implementation.
    /// YOLOv8目标检测模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv8 is the latest version of the YOLO (You Only Look Once) family,
    /// offering improved accuracy and speed for object detection tasks.
    /// YOLOv8是YOLO(You Only Look Once)系列的最新版本，为目标检测任务提供了改进的准确性和速度。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Anchor-free detection architecture
    ///   无锚点检测架构
    /// - Improved backbone and neck design
    ///   改进的骨干网络和颈部设计
    /// - Better small object detection
    ///   更好的小目标检测
    /// - Support for both CPU and GPU inference
    ///   支持CPU和GPU推理
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create YOLOv8 detector
    /// // 创建YOLOv8检测器
    /// var config = new Yolov8DetConfig("yolov8n.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var detector = new Yolov8DetModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("street.jpg"))
    ///     {
    ///         // Detect objects
    ///         // 检测目标
    ///         var results = detector.Predict(image);
    ///         
    ///         foreach (var det in results)
    ///         {
    ///             Console.WriteLine($"{det.Category}: {det.Confidence:P} at {det.Bounds}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Yolov8DetConfig"/>
    /// <seealso cref="Yolov8SegModel"/>
    /// <seealso cref="Yolov8PoseModel"/>
    public class Yolov8DetModel : IYolov8DetModel
    {
        /// <summary>
        /// Creates a new YOLOv8 detection model instance.
        /// 创建新的YOLOv8检测模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not Yolov8DetConfig / 当config不是Yolov8DetConfig时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, thresholds, and other parameters.
        /// 配置指定模型路径、输入尺寸、阈值和其他参数。
        /// </remarks>
        public Yolov8DetModel(Yolov8DetConfig config) : base(config) { }

        /// <summary>
        /// Performs object detection on a single image.
        /// 对单张图像执行目标检测。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of detection results / 检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no objects are detected.
        /// 如果未检测到目标则返回空数组。
        /// </remarks>
        /// <example>
        /// <code>
    ///     using (Mat image = Cv2.ImRead("photo.jpg"))
    ///     {
    ///         var detections = detector.Predict(image);
    ///         
    ///         // Draw results
    ///         foreach (var det in detections)
    ///         {
    ///             Cv2.Rectangle(image, CvDataExtensions.ToRect(det.Bounds), Scalar.Red, 2);
    ///         }
    ///     }
    ///     </code>
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
        /// Batch processing improves throughput for multiple images.
        /// 批处理提高了多张图像的吞吐量。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = Directory.GetFiles("images", "*.jpg")
        ///     .Select(f => Cv2.ImRead(f))
        ///     .ToList();
        /// 
        /// var allResults = detector.PredictBatch(images);
        /// </code>
        /// </example>
        public List<DetResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<DetResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for YOLOv8 detection inference.
        /// 对图像进行预处理以进行YOLOv8检测推理。
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data / 预处理后的张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Resize to 640x640 (or configured input size)
        ///    调整到640x640(或配置的输入尺寸)
        /// 2. Normalize pixel values to 0-1 range
        ///    将像素值归一化到0-1范围
        /// 3. Convert to NCHW tensor format
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
        /// Preprocesses batch of images for YOLOv8 detection inference.
        /// 对批量图像进行预处理以进行YOLOv8检测推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
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
