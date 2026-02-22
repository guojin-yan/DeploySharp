using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Model;
using DeploySharp.Data;
using Size = DeploySharp.Data.Size;
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv5 instance segmentation model implementation
    /// YOLOv5实例分割模型实现
    /// </summary>
    /// <remarks>
    /// YOLOv5 segmentation extends detection capabilities with mask prediction,
    /// providing pixel-level instance masks alongside bounding boxes.
    /// YOLOv5分割通过掩膜预测扩展了检测能力，提供像素级实例掩膜以及边界框。
    /// </remarks>
    /// <seealso cref="IYolov5SegModel"/>
    public class Yolov5SegModel : IYolov5SegModel
    {
        /// <summary>
        /// Constructor initializes with segmentation model configuration
        /// 构造函数使用分割模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov5SegModel(Yolov5SegConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses image for YOLOv5 segmentation
        /// 为YOLOv5分割预处理图像
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
        /// Preprocesses batch of images for YOLOv5 segmentation
        /// 为YOLOv5分割预处理批量图像
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
