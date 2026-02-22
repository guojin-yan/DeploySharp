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
    /// YOLOv26 Oriented Bounding Box (OBB) model implementation
    /// YOLOv26定向边界框(OBB)模型实现
    /// </summary>
    /// <remarks>
    /// YOLOv26 OBB variant for rotated object detection in aerial and industrial imagery.
    /// YOLOv26 OBB变体，用于航拍和工业图像中的旋转目标检测。
    /// </remarks>
    /// <seealso cref="IYolov26ObbModel"/>
    public class Yolov26ObbModel : IYolov26ObbModel
    {
        /// <summary>
        /// Constructor initializes with OBB model configuration
        /// 构造函数使用OBB模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov26ObbModel(Yolov26ObbConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for OBB inference
        /// 为OBB推理预处理图像
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
        /// Preprocesses batch of images for OBB inference
        /// 为OBB推理预处理批量图像
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
