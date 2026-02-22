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
    /// YOLOv11 Oriented Bounding Box (OBB) model implementation
    /// YOLOv11定向边界框(OBB)模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBB models detect rotated objects using oriented bounding boxes instead of axis-aligned boxes.
    /// This is essential for applications like aerial imagery, text detection, and industrial inspection
    /// where objects may appear at arbitrary angles.
    /// OBB模型使用定向边界框而非轴对齐框来检测旋转目标。
    /// 这对于航拍图像、文本检测和工业检查等目标可能以任意角度出现的应用至关重要。
    /// </para>
    /// <para>
    /// OBB output format: (x, y, w, h, angle) where angle represents rotation
    /// OBB输出格式: (x, y, w, h, angle)，其中angle表示旋转角度
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
    /// var config = new Yolov11ObbConfig("yolov11-obb.onnx");
    /// using var model = new Yolov11ObbModel(config);
    /// using var aerialImage = Image.Load&lt;Rgb24&gt;("aerial.jpg");
    /// var detections = model.Predict(aerialImage);
    /// foreach (OBBResult det in detections)
    /// {
    ///     Console.WriteLine($"Object at angle {det.Angle:F1}°");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IYolov11ObbModel"/>
    /// <seealso cref="OBBResult"/>
    public class Yolov11ObbModel : IYolov11ObbModel
    {
        /// <summary>
        /// Constructor initializes with OBB model configuration
        /// 构造函数使用OBB模型配置初始化
        /// </summary>
        /// <param name="config">OBB model configuration / OBB模型配置</param>
        public Yolov11ObbModel(Yolov11ObbConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for OBB inference
        /// 为OBB推理预处理图像
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
        /// Preprocesses batch of images for OBB inference
        /// 为OBB推理预处理批量图像
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
