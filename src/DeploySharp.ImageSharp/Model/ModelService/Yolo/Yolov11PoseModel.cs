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
    /// YOLOv11 human pose estimation model implementation
    /// YOLOv11人体姿态估计模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pose estimation detects human body keypoints (joints) and their connections (skeleton).
    /// YOLOv11-pose predicts 17 keypoints following the COCO format for human pose estimation.
    /// 姿态估计检测人体关键点（关节）及其连接（骨架）。
    /// YOLOv11-pose预测17个关键点，遵循COCO人体姿态估计格式。
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
    /// - Fitness and exercise analysis
    ///   健身和运动分析
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
    /// var config = new Yolov11PoseConfig("yolov11-pose.onnx");
    /// using var model = new Yolov11PoseModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("person.jpg");
    /// var poses = model.Predict(image);
    /// foreach (KeyPointResult pose in poses)
    /// {
    ///     var nose = pose.KeyPoints[0];
    ///     Console.WriteLine($"Nose at ({nose.Point.X}, {nose.Point.Y})");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov11PoseModel"/>
    /// <seealso cref="KeyPointResult"/>
    public class Yolov11PoseModel : IYolov11PoseModel
    {
        /// <summary>
        /// Constructor initializes with pose model configuration
        /// 构造函数使用姿态模型配置初始化
        /// </summary>
        /// <param name="config">Pose model configuration / 姿态模型配置</param>
        public Yolov11PoseModel(Yolov11PoseConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for pose estimation
        /// 为姿态估计预处理图像
        /// </summary>
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
        /// Preprocesses batch of images for pose estimation
        /// 为姿态估计预处理批量图像
        /// </summary>
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
