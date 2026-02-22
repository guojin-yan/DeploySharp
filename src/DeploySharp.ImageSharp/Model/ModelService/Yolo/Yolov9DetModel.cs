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
    /// YOLOv9 object detection model implementation
    /// YOLOv9目标检测模型实现
    /// </summary>
    /// <remarks>
    /// YOLOv9 introduces programmable gradient information (PGI) and generalized efficient
    /// layer aggregation network (GELAN) for improved learning and inference.
    /// YOLOv9引入了可编程梯度信息(PGI)和广义高效层聚合网络(GELAN)以改进学习和推理。
    /// </remarks>
    /// <seealso cref="IYolov9DetModel"/>
    public class Yolov9DetModel : IYolov9DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov9DetModel(Yolov9DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for YOLOv9 inference
        /// 为YOLOv9推理预处理图像
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
        /// Preprocesses batch of images for YOLOv9 inference
        /// 为YOLOv9推理预处理批量图像
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
