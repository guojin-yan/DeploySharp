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
    /// YOLOv26 Oriented Bounding Box (OBB) model implementation
    /// YOLOv26定向边界框(OBB)模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv26 OBB variant for rotated object detection in aerial and industrial imagery.
    /// YOLOv26 OBB变体，用于航拍和工业图像中的旋转目标检测。
    /// </para>
    /// <para>
    /// OBB output format: (x, y, w, h, angle) where angle represents rotation in degrees
    /// OBB输出格式: (x, y, w, h, angle)，其中angle表示旋转角度（度）
    /// </para>
    /// <para>
    /// Use cases:
    /// 使用场景:
    /// - Aerial/satellite image analysis
    ///   航拍/卫星图像分析
    /// - Document text detection
    ///   文档文本检测
    /// - Industrial part inspection
    ///   工业零件检查
    /// - Ship detection in maritime images
    ///   海事图像中的船只检测
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Yolov26ObbConfig("yolov26-obb.onnx");
    /// using var model = new Yolov26ObbModel(config);
    /// using var aerialImage = Image.Load&lt;Rgb24&gt;("aerial.jpg");
    /// var detections = model.Predict(aerialImage);
    /// foreach (OBBResult det in detections)
    /// {
    ///     Console.WriteLine($"Object at angle {det.Angle:F1}°");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov26ObbModel"/>
    /// <seealso cref="Yolov26ObbConfig"/>
    /// <seealso cref="OBBResult"/>
    public class Yolov26ObbModel : IYolov26ObbModel
    {
        /// <summary>
        /// Constructor initializes with OBB model configuration
        /// 构造函数使用OBB模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv26 OBB model configuration / YOLOv26 OBB模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// Initializes the YOLOv26 OBB model with specified configuration.
        /// 使用指定配置初始化YOLOv26 OBB模型。
        /// </remarks>
        public Yolov26ObbModel(Yolov26ObbConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses a single image for OBB inference
        /// 为OBB推理预处理单张图像
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
        /// Preprocesses a batch of images for OBB inference
        /// 为OBB推理预处理批量图像
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
