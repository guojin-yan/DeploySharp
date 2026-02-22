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
    /// YOLOv13 object detection model implementation
    /// YOLOv13目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv13 represents a future evolution in the YOLO family with expected improvements
    /// in detection accuracy, especially for challenging scenarios.
    /// YOLOv13代表YOLO家族的未来演进，预期在检测准确性方面有所改进，特别是在具有挑战性的场景中。
    /// </para>
    /// <para>
    /// YOLOv13 features (expected):
    /// YOLOv13特性（预期）:
    /// - Advanced architectural innovations
    ///   先进的架构创新
    /// - Enhanced multi-scale detection
    ///   增强的多尺度检测
    /// - Improved training stability
    ///   改进的训练稳定性
    /// - Better generalization across domains
    ///   更好的跨域泛化能力
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov13DetConfig("yolov13.onnx");
    /// using var model = new Yolov13DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// foreach (var det in results)
    /// {
    ///     Console.WriteLine($"{det.Category}: {det.Confidence:F2} at ({det.Box.X}, {det.Box.Y})");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov13DetModel"/>
    /// <seealso cref="Yolov13DetConfig"/>
    public class Yolov13DetModel : IYolov13DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv13 detection model configuration / YOLOv13检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv13 model with specified configuration and loads the model weights.
        /// 使用指定配置初始化YOLOv13模型并加载模型权重。
        /// </remarks>
        public Yolov13DetModel(Yolov13DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for YOLOv13 inference
        /// 为YOLOv13推理预处理单张图像
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
        /// Preprocesses a batch of images for YOLOv13 inference
        /// 为YOLOv13推理预处理批量图像
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
