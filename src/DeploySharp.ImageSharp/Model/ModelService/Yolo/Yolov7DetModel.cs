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
    /// YOLOv7 object detection model implementation
    /// YOLOv7目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv7 introduces architectural improvements including extended efficient layer aggregation
    /// networks (E-ELAN) and model scaling for concatenation-based models.
    /// YOLOv7引入了架构改进，包括扩展高效层聚合网络(E-ELAN)和基于连接的模型缩放。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点:
    /// - E-ELAN architecture for better gradient flow
    ///   E-ELAN架构以获得更好的梯度流
    /// - Planned re-parameterization
    ///   计划重参数化
    /// - Coarse-to-fine auxiliary head
    ///   从粗到细的辅助头
    /// </para>
    /// </remarks>
    /// <seealso cref="IYolov7DetModel"/>
    public class Yolov7DetModel : IYolov7DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        public Yolov7DetModel(Yolov7DetConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses image for YOLOv7 inference
        /// 为YOLOv7推理预处理图像
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
        /// Preprocesses batch of images for YOLOv7 inference
        /// 为YOLOv7推理预处理批量图像
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
