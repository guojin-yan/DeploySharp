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
    /// YOLOv8 pose estimation variant for detecting 17 human keypoints in COCO format.
    /// YOLOv8姿态估计变体，用于检测COCO格式的17个人体关键点。
    /// </remarks>
    /// <seealso cref="IYolov8PoseModel"/>
    public class Yolov8PoseModel : IYolov8PoseModel
    {
        /// <summary>
        /// Constructor initializes with pose model configuration
        /// 构造函数使用姿态模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov8PoseModel(Yolov8PoseConfig config) : base(config) { }


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
