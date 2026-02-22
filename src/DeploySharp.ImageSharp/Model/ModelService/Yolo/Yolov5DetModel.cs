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
    /// YOLOv5 object detection model implementation
    /// YOLOv5目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv5 is a widely-used object detection model known for its balance of speed and accuracy.
    /// It introduced significant improvements over YOLOv4 with PyTorch-based implementation
    /// and better training strategies.
    /// YOLOv5是一种广泛使用的目标检测模型，以其速度和准确性的平衡而闻名。
    /// 它通过基于PyTorch的实现和更好的训练策略，相比YOLOv4引入了显著改进。
    /// </para>
    /// <para>
    /// YOLOv5 characteristics:
    /// YOLOv5特性:
    /// - Anchor-based detection with auto-anchor calculation
    ///   基于锚点的检测，带有自动锚点计算
    /// - Mosaic augmentation for training
    ///   用于训练的Mosaic增强
    /// - Multiple model sizes (n, s, m, l, x)
    ///   多种模型大小(n, s, m, l, x)
    /// - Strong community support and ecosystem
    ///   强大的社区支持和生态系统
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov5DetConfig("yolov5s.onnx");
    /// using var model = new Yolov5DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// </code>
    /// </example>
    /// <seealso cref="IYolov5DetModel"/>
    public class Yolov5DetModel : IYolov5DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv5 detection model configuration / YOLOv5检测模型配置</param>
        public Yolov5DetModel(Yolov5DetConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses image for YOLOv5 inference
        /// 为YOLOv5推理预处理图像
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
        /// Preprocesses batch of images for YOLOv5 inference
        /// 为YOLOv5推理预处理批量图像
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
