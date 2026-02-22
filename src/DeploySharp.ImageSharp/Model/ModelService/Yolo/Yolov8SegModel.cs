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
    /// YOLOv8 instance segmentation model implementation
    /// YOLOv8实例分割模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv8 segmentation variant providing pixel-level masks with detection capabilities.
    /// YOLOv8分割变体，提供像素级掩膜和检测能力。
    /// </para>
    /// <para>
    /// YOLOv8-seg characteristics:
    /// YOLOv8-seg特性:
    /// - Instance segmentation with high-quality masks
    ///   高质量的实例分割掩膜
    /// - Efficient mask head design
    ///   高效的掩膜头设计
    /// - Compatible with all YOLOv8 model scales
    ///   兼容所有YOLOv8模型尺度
    /// </para>
    /// <para>
    /// Output format: Bounding box + binary mask for each instance
    /// 输出格式: 每个实例的边界框 + 二进制掩膜
    /// </para>
    /// <para>
    /// Applications:
    /// 应用场景:
    /// - Medical image analysis
    ///   医学图像分析
    /// - Autonomous driving
    ///   自动驾驶
    /// - Agricultural analysis
    ///   农业分析
    /// - Industrial inspection
    ///   工业检查
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov8SegConfig("yolov8s-seg.onnx");
    /// using var model = new Yolov8SegModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("scene.jpg");
    /// var results = model.Predict(image);
    /// foreach (SegResult seg in results)
    /// {
    ///     Console.WriteLine($"{seg.Category} with mask area {seg.MaskArea}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov8SegModel"/>
    /// <seealso cref="Yolov8SegConfig"/>
    /// <seealso cref="SegResult"/>
    public class Yolov8SegModel : IYolov8SegModel
    {
        /// <summary>
        /// Constructor initializes with segmentation model configuration
        /// 构造函数使用分割模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv8 segmentation model configuration / YOLOv8分割模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv8 segmentation model with specified configuration.
        /// 使用指定配置初始化YOLOv8分割模型。
        /// </remarks>
        public Yolov8SegModel(Yolov8SegConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for instance segmentation
        /// 为实例分割预处理单张图像
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
        /// Preprocesses a batch of images for instance segmentation
        /// 为实例分割预处理批量图像
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
