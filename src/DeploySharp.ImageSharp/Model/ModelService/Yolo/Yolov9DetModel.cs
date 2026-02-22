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
    /// YOLOv9 object detection model implementation
    /// YOLOv9目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv9 introduces programmable gradient information (PGI) and generalized efficient
    /// layer aggregation network (GELAN) for improved learning and inference.
    /// YOLOv9引入了可编程梯度信息(PGI)和广义高效层聚合网络(GELAN)以改进学习和推理。
    /// </para>
    /// <para>
    /// Key innovations:
    /// 关键创新:
    /// - PGI (Programmable Gradient Information) for stable training
    ///   可编程梯度信息用于稳定训练
    /// - GELAN architecture for efficient feature extraction
    ///   GELAN架构用于高效特征提取
    /// - Better information flow and gradient propagation
    ///   更好的信息流和梯度传播
    /// - Improved accuracy without sacrificing speed
    ///   在不牺牲速度的情况下提高准确性
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov9DetConfig("yolov9.onnx");
    /// using var model = new Yolov9DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// foreach (var det in results)
    /// {
    ///     Console.WriteLine($"{det.Category}: {det.Confidence:F2}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov9DetModel"/>
    /// <seealso cref="Yolov9DetConfig"/>
    public class Yolov9DetModel : IYolov9DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv9 detection model configuration / YOLOv9检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv9 model with specified configuration and loads the model weights.
        /// 使用指定配置初始化YOLOv9模型并加载模型权重。
        /// </remarks>
        public Yolov9DetModel(Yolov9DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for YOLOv9 inference
        /// 为YOLOv9推理预处理单张图像
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
        /// Preprocesses a batch of images for YOLOv9 inference
        /// 为YOLOv9推理预处理批量图像
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
