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
    /// YOLOv11 instance segmentation model implementation
    /// YOLOv11实例分割模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instance segmentation combines object detection with pixel-level segmentation,
    /// providing both bounding boxes and precise object masks. YOLOv11-seg extends
    /// detection capabilities with mask prediction for each detected instance.
    /// 实例分割结合了目标检测和像素级分割，同时提供边界框和精确的目标掩膜。
    /// YOLOv11-seg通过为每个检测到的实例预测掩膜来扩展检测能力。
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
    /// - Autonomous driving (lane segmentation, object boundaries)
    ///   自动驾驶（车道分割、目标边界）
    /// - Agricultural crop analysis
    ///   农业作物分析
    /// - Industrial quality inspection
    ///   工业质量检查
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov11SegConfig("yolov11-seg.onnx");
    /// using var model = new Yolov11SegModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("scene.jpg");
    /// var results = model.Predict(image);
    /// foreach (SegResult seg in results)
    /// {
    ///     Console.WriteLine($"{seg.Category} with {seg.Mask.Length} mask pixels");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov11SegModel"/>
    /// <seealso cref="SegResult"/>
    public class Yolov11SegModel : IYolov11SegModel
    {
        /// <summary>
        /// Constructor initializes with segmentation model configuration
        /// 构造函数使用分割模型配置初始化
        /// </summary>
        /// <param name="config">Segmentation model configuration / 分割模型配置</param>
        public Yolov11SegModel(Yolov11SegConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for instance segmentation
        /// 为实例分割预处理图像
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
        /// Preprocesses batch of images for instance segmentation
        /// 为实例分割预处理批量图像
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

