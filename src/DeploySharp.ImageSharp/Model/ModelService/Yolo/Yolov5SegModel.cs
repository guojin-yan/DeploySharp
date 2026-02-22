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
    /// <para>
    /// YOLOv5 segmentation extends detection capabilities with mask prediction,
    /// providing pixel-level instance masks alongside bounding boxes.
    /// YOLOv5分割通过掩膜预测扩展了检测能力，提供像素级实例掩膜以及边界框。
    /// </para>
    /// <para>
    /// YOLOv5-seg characteristics:
    /// YOLOv5-seg特性:
    /// - Prototype mask coefficients for efficient mask generation
    ///   原型掩膜系数用于高效掩膜生成
    /// - Mask branch alongside detection head
    ///   检测头旁边的掩膜分支
    /// - Compatible with all YOLOv5 model sizes
    ///   兼容所有YOLOv5模型大小
    /// </para>
    /// <para>
    /// Output format: Bounding box + binary mask for each instance
    /// 输出格式: 每个实例的边界框 + 二进制掩膜
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov5SegConfig("yolov5s-seg.onnx");
    /// using var model = new Yolov5SegModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("scene.jpg");
    /// var results = model.Predict(image);
    /// foreach (SegResult seg in results)
    /// {
    ///     Console.WriteLine($"{seg.Category} with mask area {seg.MaskArea}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov5SegModel"/>
    /// <seealso cref="Yolov5SegConfig"/>
    /// <seealso cref="SegResult"/>
    public class Yolov5SegModel : IYolov5SegModel
    {
        /// <summary>
        /// Constructor initializes with segmentation model configuration
        /// 构造函数使用分割模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv5 segmentation model configuration / YOLOv5分割模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv5 segmentation model with specified configuration.
        /// 使用指定配置初始化YOLOv5分割模型。
        /// </remarks>
        public Yolov5SegModel(Yolov5SegConfig config) : base(config) { }

        /// <summary>
        /// Preprocesses a single image for YOLOv5 segmentation
        /// 为YOLOv5分割预处理单张图像
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
        /// Preprocesses a batch of images for YOLOv5 segmentation
        /// 为YOLOv5分割预处理批量图像
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
