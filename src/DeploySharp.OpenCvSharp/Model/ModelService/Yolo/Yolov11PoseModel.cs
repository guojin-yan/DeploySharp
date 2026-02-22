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
    /// YOLOv11 pose estimation model implementation.
    /// YOLOv11姿态估计模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv11 Pose extends the detection capabilities to estimate human body keypoints,
    /// enabling applications like action recognition, motion analysis, and fitness tracking.
    /// YOLOv11 Pose扩展了检测功能以估计人体关键点，支持动作识别、运动分析和健身追踪等应用。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Detects 17 keypoints per person (COCO format)
    ///   每人检测17个关键点(COCO格式)
    /// - Real-time pose estimation
    ///   实时姿态估计
    /// - Multi-person pose detection
    ///   多人姿态检测
    /// - High accuracy for keypoint localization
    ///   关键点定位高精度
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create YOLOv11 pose estimator
    /// // 创建YOLOv11姿态估计器
    /// var config = new Yolov11PoseConfig("yolov11n-pose.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var estimator = new Yolov11PoseModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("person.jpg"))
    ///     {
    ///         var results = estimator.Predict(image);
    ///         
    ///         foreach (var pose in results)
    ///         {
    ///             // Draw keypoints
    ///             foreach (var kp in pose.KeyPoints)
    ///             {
    ///                 Cv2.Circle(image, new Point(kp.X, kp.Y), 3, Scalar.Red, -1);
    ///             }
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Yolov11PoseConfig"/>
    /// <seealso cref="Yolov11DetModel"/>
    public class Yolov11PoseModel : IYolov11PoseModel
    {
        /// <summary>
        /// Creates a new YOLOv11 pose estimation model instance.
        /// 创建新的YOLOv11姿态估计模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not Yolov11PoseConfig / 当config不是Yolov11PoseConfig时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and NMS threshold.
        /// 配置指定模型路径、输入尺寸、置信度阈值和NMS阈值。
        /// </remarks>
        public Yolov11PoseModel(Yolov11PoseConfig config) : base(config) { }

        /// <summary>
        /// Performs pose estimation on a single image.
        /// 对单张图像执行姿态估计。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of pose estimation results with keypoints / 包含关键点的姿态估计结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no persons are detected above the confidence threshold.
        /// Each result contains 17 keypoints representing body joints.
        /// 如果没有检测到高于置信度阈值的人物，则返回空数组。
        /// 每个结果包含17个表示身体关节的关键点。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("person.jpg"))
        /// {
        ///     var poses = estimator.Predict(image);
        ///     
        ///     foreach (var pose in poses)
        ///     {
        ///         // Draw skeleton
        ///         DrawSkeleton(image, pose.KeyPoints);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public KeyPointResult[] Predict(Mat img)
        {
            return base.Predict(img) as KeyPointResult[];
        }

        /// <summary>
        /// Performs batch pose estimation on multiple images.
        /// 对多张图像执行批量姿态估计。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of pose estimation results for each image / 每张图像的姿态估计结果列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        /// <remarks>
        /// Batch processing is more efficient than sequential single-image processing.
        /// 批处理比顺序单张图像处理更高效。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt; { image1, image2, image3 };
        /// var allResults = estimator.PredictBatch(images);
        /// 
        /// for (int i = 0; i &lt; allResults.Count; i++)
        /// {
        ///     Console.WriteLine($"Image {i}: {allResults[i].Length} persons detected");
        /// }
        /// </code>
        /// </example>
        public List<KeyPointResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<KeyPointResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for YOLOv11 pose estimation inference.
        /// 对图像进行预处理以进行YOLOv11姿态估计推理。
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
        /// Preprocesses batch of images for YOLOv11 pose estimation inference.
        /// 对批量图像进行预处理以进行YOLOv11姿态估计推理。
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
