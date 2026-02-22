using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Collections.Concurrent;
using System.Numerics;
using System.Diagnostics;
using System.Configuration;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Buffers;
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv6 object detection model implementation from Meituan
    /// 来自美团的YOLOv6目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv6 is an industrial-grade object detector designed for production deployment
    /// with optimized inference speed and hardware-friendly architecture.
    /// YOLOv6是工业级目标检测器，专为生产部署设计，具有优化的推理速度和对硬件友好的架构。
    /// </para>
    /// <para>
    /// Key improvements:
    /// 主要改进:
    /// - Hardware-friendly backbone and neck design
    ///   对硬件友好的骨干和颈部设计
    /// - Efficient decoupled head
    ///   高效的解耦头
    /// - Optimized for various deployment targets
    ///   针对各种部署目标进行优化
    /// </para>
    /// </remarks>
    /// <seealso cref="IYolov6DetModel"/>
    public class Yolov6DetModel : IYolov6DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov6DetModel(Yolov6DetConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses image for YOLOv6 inference
        /// 为YOLOv6推理预处理图像
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
        /// Preprocesses batch of images for YOLOv6 inference
        /// 为YOLOv6推理预处理批量图像
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
