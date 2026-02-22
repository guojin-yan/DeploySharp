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
    /// - Strong balance of accuracy and speed
    ///   准确性和速度的良好平衡
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov6DetConfig("yolov6s.onnx");
    /// using var model = new Yolov6DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// foreach (var det in results)
    /// {
    ///     Console.WriteLine($"{det.Category}: {det.Confidence:F2}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov6DetModel"/>
    /// <seealso cref="Yolov6DetConfig"/>
    public class Yolov6DetModel : IYolov6DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv6 detection model configuration / YOLOv6检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv6 model with specified configuration and loads the model weights.
        /// 使用指定配置初始化YOLOv6模型并加载模型权重。
        /// </remarks>
        public Yolov6DetModel(Yolov6DetConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses a single image for YOLOv6 inference
        /// 为YOLOv6推理预处理单张图像
        /// </summary>
        /// <param name="img">Input image (expected Image&lt;Rgb24&gt;) / 输入图像（预期为Image&lt;Rgb24&gt;）</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed DataTensor / 预处理后的DataTensor</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Image&lt;Rgb24&gt; / 当img不是Image&lt;Rgb24&gt;时抛出</exception>
        /// <remarks>
        /// Applies standard YOLO preprocessing: resize, normalize, and convert to tensor format.
        /// 应用标准YOLO预处理：调整大小、归一化并转换为张量格式。
        /// </remarks>
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
        /// Preprocesses a batch of images for YOLOv6 inference
        /// 为YOLOv6推理预处理批量图像
        /// </summary>
        /// <param name="img">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters array / 输出调整参数数组</param>
        /// <returns>Preprocessed DataTensor for batch / 批量预处理后的DataTensor</returns>
        /// <remarks>
        /// Processes multiple images as a batch for improved throughput.
        /// 将多张图像作为批次处理以提高吞吐量。
        /// </remarks>
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
