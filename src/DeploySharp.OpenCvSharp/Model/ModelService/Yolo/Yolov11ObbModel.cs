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
    /// YOLOv11 Oriented Bounding Box (OBB) detection model implementation.
    /// YOLOv11旋转边界框(OBB)检测模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv11 OBB extends the standard detection capabilities to support oriented bounding boxes,
    /// which is particularly useful for detecting rotated objects like vehicles, ships, and buildings
    /// in aerial or satellite imagery.
    /// YOLOv11 OBB扩展了标准检测功能以支持旋转边界框，这对于检测航拍或卫星图像中的
    /// 旋转物体（如车辆、船舶和建筑物）特别有用。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Detects objects with rotation angle information
    ///   检测带有旋转角度信息的目标
    /// - Ideal for aerial/satellite imagery analysis
    ///   适用于航拍/卫星图像分析
    /// - Supports DOTA dataset format
    ///   支持DOTA数据集格式
    /// - High accuracy for oriented objects
    ///   对定向目标具有高精度
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create YOLOv11 OBB detector
    /// // 创建YOLOv11 OBB检测器
    /// var config = new Yolov11ObbConfig("yolov11n-obb.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var detector = new Yolov11ObbModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("aerial.jpg"))
    ///     {
    ///         var results = detector.Predict(image);
    ///         
    ///         foreach (var det in results)
    ///         {
    ///             // det contains rotated box coordinates and angle
    ///             // det包含旋转框坐标和角度
    ///             Console.WriteLine($"{det.Category}: {det.Confidence:P} at angle {det.Angle}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Yolov11ObbConfig"/>
    /// <seealso cref="Yolov11DetModel"/>
    public class Yolov11ObbModel : IYolov11ObbModel
    {
        /// <summary>
        /// Creates a new YOLOv11 OBB detection model instance.
        /// 创建新的YOLOv11 OBB检测模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not Yolov11ObbConfig / 当config不是Yolov11ObbConfig时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and NMS threshold.
        /// 配置指定模型路径、输入尺寸、置信度阈值和NMS阈值。
        /// </remarks>
        public Yolov11ObbModel(Yolov11ObbConfig config) : base(config) { }

        /// <summary>
        /// Performs oriented bounding box detection on a single image.
        /// 对单张图像执行旋转边界框检测。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of oriented bounding box detection results / 旋转边界框检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no objects are detected above the confidence threshold.
        /// Each result includes rotation angle information along with bounding box coordinates.
        /// 如果没有检测到高于置信度阈值的目标，则返回空数组。
        /// 每个结果包含旋转角度信息以及边界框坐标。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("aerial.jpg"))
        /// {
        ///     var detections = detector.Predict(image);
        ///     
        ///     foreach (var det in detections)
        ///     {
        ///         // Draw rotated rectangle
        ///         // 绘制旋转矩形
        ///         var points = det.GetRotatedBoxPoints();
        ///         Cv2.Polylines(image, points, true, Scalar.Red, 2);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public ObbResult[] Predict(Mat img)
        {
            return base.Predict(img) as ObbResult[];
        }

        /// <summary>
        /// Performs batch oriented bounding box detection on multiple images.
        /// 对多张图像执行批量旋转边界框检测。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of OBB detection results for each image / 每张图像的OBB检测结果列表</returns>
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
        public List<ObbResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<ObbResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for YOLOv11 OBB detection inference.
        /// 对图像进行预处理以进行YOLOv11 OBB检测推理。
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
        /// Preprocesses batch of images for YOLOv11 OBB detection inference.
        /// 对批量图像进行预处理以进行YOLOv11 OBB检测推理。
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
