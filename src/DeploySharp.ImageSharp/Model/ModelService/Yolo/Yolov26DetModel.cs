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
    /// YOLOv26 object detection model implementation
    /// YOLOv26目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv26 represents an experimental/prototype version in the YOLO series
    /// with advanced architectural features for research purposes.
    /// YOLOv26代表YOLO系列中的实验性/原型版本，具有用于研究目的的高级架构特性。
    /// </para>
    /// <para>
    /// YOLOv26 characteristics:
    /// YOLOv26特性:
    /// - Experimental architecture with novel design patterns
    ///   具有新颖设计模式的实验性架构
    /// - Advanced feature fusion techniques
    ///   高级特征融合技术
    /// - Optimized for research and development
    ///   针对研究和开发进行优化
    /// - Potential future YLO series direction
    ///   潜在的未来YOLO系列方向
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov26DetConfig("yolov26.onnx");
    /// using var model = new Yolov26DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// foreach (var det in results)
    /// {
    ///     Console.WriteLine($"{det.Category}: {det.Confidence:F2}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov26DetModel"/>
    /// <seealso cref="Yolov26DetConfig"/>
    public class Yolov26DetModel : IYolov26DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv26 detection model configuration / YOLOv26检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv26 model with specified configuration and loads the model weights.
        /// 使用指定配置初始化YOLOv26模型并加载模型权重。
        /// </remarks>
        public Yolov26DetModel(Yolov26DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for YOLOv26 inference
        /// 为YOLOv26推理预处理单张图像
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
        /// Preprocesses a batch of images for YOLOv26 inference
        /// 为YOLOv26推理预处理批量图像
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
