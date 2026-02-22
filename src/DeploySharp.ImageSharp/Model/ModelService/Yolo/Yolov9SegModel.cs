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
    /// YOLOv9 instance segmentation model implementation
    /// YOLOv9实例分割模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv9 segmentation variant with PGI and GELAN architecture for mask prediction.
    /// 带PGI和GELAN架构的YOLOv9分割变体，用于掩膜预测。
    /// </para>
    /// <para>
    /// YOLOv9-seg features:
    /// YOLOv9-seg特性:
    /// - PGI for improved mask boundary accuracy
    ///   PGI用于改进掩膜边界精度
    /// - GELAN-based mask head for efficient computation
    ///   基于GELAN的掩膜头用于高效计算
    /// - High-quality instance segmentation
    ///   高质量的实例分割
    /// </para>
    /// <para>
    /// Output format: Bounding box + binary mask for each instance
    /// 输出格式: 每个实例的边界框 + 二进制掩膜
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov9SegConfig("yolov9-seg.onnx");
    /// using var model = new Yolov9SegModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("scene.jpg");
    /// var results = model.Predict(image);
    /// foreach (SegResult seg in results)
    /// {
    ///     Console.WriteLine($"{seg.Category} with {seg.Mask.Length} mask pixels");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov9SegModel"/>
    /// <seealso cref="Yolov9SegConfig"/>
    /// <seealso cref="SegResult"/>
    public class Yolov9SegModel : IYolov9SegModel
    {
        /// <summary>
        /// Constructor initializes with segmentation model configuration
        /// 构造函数使用分割模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv9 segmentation model configuration / YOLOv9分割模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv9 segmentation model with specified configuration.
        /// 使用指定配置初始化YOLOv9分割模型。
        /// </remarks>
        public Yolov9SegModel(Yolov9SegConfig config) : base(config) { }


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
