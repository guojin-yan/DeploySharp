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
    /// YOLOv8 object detection model implementation from Ultralytics
    /// 来自Ultralytics的YOLOv8目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv8 is a state-of-the-art YOLO model with anchor-free detection head,
    /// redesigned architecture, and improved training strategies.
    /// YOLOv8是最先进的YOLO模型，具有无锚点检测头、重新设计的架构和改进的训练策略。
    /// </para>
    /// <para>
    /// YOLOv8 improvements:
    /// YOLOv8改进:
    /// - Anchor-free detection head
    ///   无锚点检测头
    /// - New backbone and neck architecture
    ///   新的骨干和颈部架构
    /// - C2f blocks replacing C3 blocks
    ///   C2f块替代C3块
    /// - Decoupled head design
    ///   解耦头设计
    /// </para>
    /// </remarks>
    /// <seealso cref="IYolov8DetModel"/>
    public class Yolov8DetModel : IYolov8DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov8DetModel(Yolov8DetConfig config) : base(config) { }

        /// <summary>
        /// Predicts on a batch of images with type-safe return
        /// 对一批图像进行预测，返回类型安全的结果
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of detection result arrays / 检测结果数组的列表</returns>
        public List<Result[]> PredictBatch(List<Image<Rgb24>> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<Result[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for YOLOv8 inference
        /// 为YOLOv8推理预处理图像
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
        /// Preprocesses batch of images for YOLOv8 inference
        /// 为YOLOv8推理预处理批量图像
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
