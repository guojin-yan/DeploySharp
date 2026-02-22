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
    /// YOLOv12 object detection model implementation
    /// YOLOv12目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv12 continues the evolution of the YOLO series with further architectural improvements
    /// and optimizations for both accuracy and inference speed.
    /// YOLOv12继续发展YOLO系列，通过进一步的架构改进和优化来提高准确性和推理速度。
    /// </para>
    /// <para>
    /// YOLOv12 improvements:
    /// YOLOv12改进:
    /// - R-ELAN (Residual ELAN) architecture for better gradient flow
    ///   残差ELAN架构以获得更好的梯度流
    /// - Attention mechanisms for enhanced feature extraction
    ///   注意力机制用于增强特征提取
    /// - Optimized for real-time applications
    ///   针对实时应用进行优化
    /// - Improved small object detection
    ///   改进的小目标检测
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov12DetConfig("yolov12.onnx");
    /// using var model = new Yolov12DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// foreach (var det in results)
    /// {
    ///     Console.WriteLine($"{det.Category}: {det.Confidence:F2}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov12DetModel"/>
    /// <seealso cref="Yolov12DetConfig"/>
    public class Yolov12DetModel : IYolov12DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv12 detection model configuration / YOLOv12检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv12 model with specified configuration and loads the model weights.
        /// 使用指定配置初始化YOLOv12模型并加载模型权重。
        /// </remarks>
        public Yolov12DetModel(Yolov12DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for YOLOv12 inference
        /// 为YOLOv12推理预处理单张图像
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
        /// Preprocesses a batch of images for YOLOv12 inference
        /// 为YOLOv12推理预处理批量图像
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
