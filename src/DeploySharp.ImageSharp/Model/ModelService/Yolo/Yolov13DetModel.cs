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
    /// YOLOv13 object detection model implementation
    /// YOLOv13目标检测模型实现
    /// </summary>
    /// <remarks>
    /// YOLOv13 represents a future evolution in the YOLO family with expected improvements
    /// in detection accuracy, especially for challenging scenarios.
    /// YOLOv13代表YOLO家族的未来演进，预期在检测准确性方面有所改进，特别是在具有挑战性的场景中。
    /// </remarks>
    /// <seealso cref="IYolov13DetModel"/>
    public class Yolov13DetModel : IYolov13DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov13DetModel(Yolov13DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for YOLOv13 inference
        /// 为YOLOv13推理预处理图像
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
        /// Preprocesses batch of images for YOLOv13 inference
        /// 为YOLOv13推理预处理批量图像
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
