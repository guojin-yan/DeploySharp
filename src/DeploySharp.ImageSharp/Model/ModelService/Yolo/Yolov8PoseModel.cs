using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.Log;


namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv8 human pose estimation model implementation
    /// YOLOv8人体姿态估计模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv8 pose estimation variant for detecting 17 human keypoints in COCO format.
    /// YOLOv8姿态估计变体，用于检测COCO格式的17个人体关键点。
    /// </para>
    /// <para>
    /// Keypoint indices (COCO format):
    /// 关键点索引（COCO格式）:
    /// 0: Nose, 1: Left Eye, 2: Right Eye, 3: Left Ear, 4: Right Ear,
    /// 5: Left Shoulder, 6: Right Shoulder, 7: Left Elbow, 8: Right Elbow,
    /// 9: Left Wrist, 10: Right Wrist, 11: Left Hip, 12: Right Hip,
    /// 13: Left Knee, 14: Right Knee, 15: Left Ankle, 16: Right Ankle
    /// </para>
    /// <para>
    /// Applications:
    /// 应用场景:
    /// - Fitness and exercise tracking
    ///   健身和运动跟踪
    /// - Gesture recognition
    ///   手势识别
    /// - Action recognition
    ///   动作识别
    /// - Human-computer interaction
    ///   人机交互
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov8PoseConfig("yolov8-pose.onnx");
    /// using var model = new Yolov8PoseModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("person.jpg");
    /// var poses = model.Predict(image);
    /// foreach (KeyPointResult pose in poses)
    /// {
    ///     var nose = pose.KeyPoints[0];
    ///     Console.WriteLine($"Nose at ({nose.Point.X}, {nose.Point.Y})");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov8PoseModel"/>
    /// <seealso cref="Yolov8PoseConfig"/>
    /// <seealso cref="KeyPointResult"/>
    public class Yolov8PoseModel : IYolov8PoseModel
    {
        /// <summary>
        /// Constructor initializes with pose model configuration
        /// 构造函数使用姿态模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv8 pose model configuration / YOLOv8姿态模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv8 pose model with specified configuration.
        /// 使用指定配置初始化YOLOv8姿态模型。
        /// </remarks>
        public Yolov8PoseModel(Yolov8PoseConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for pose estimation
        /// 为姿态估计预处理单张图像
        /// </summary>
        /// <param name="img">Input image (expected Image&lt;Rgb24&gt;) / 输入图像（预期为Image&lt;Rgb24&gt;）</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed DataTensor / 预处理后的DataTensor</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Image&lt;Rgb24&gt; / 当img不是Image&lt;Rgb24&gt;时抛出</exception>
        /// <remarks>
        /// Applies standard YOLO preprocessing: resize, normalize, and convert to tensor format.
        /// 应用标准YOLO预处理：调整大小、归一化并转换为张量格式。
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Image<Rgb24>)?.Size()}");

            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Image<Rgb24>)img,
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
        /// Preprocesses a batch of images for pose estimation
        /// 为姿态估计预处理批量图像
        /// </summary>
        /// <param name="img">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters array / 输出调整参数数组</param>
        /// <returns>Preprocessed DataTensor for batch / 批量预处理后的DataTensor</returns>
        /// <remarks>
        /// Processes multiple images as a batch for improved throughput.
        /// 将多张图像作为批次处理以提高吞吐量。
        /// </remarks>
        protected override DataTensor PreprocessBatch(List<object> img, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {img.Count}");

            try
            {
                return CvDataProcessor.ImageListProcessToDataTensor(
                    img.OfType<Image<Rgb24>>().ToList(),
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
