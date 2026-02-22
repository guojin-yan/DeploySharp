using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Anomalib segmentation model implementation for anomaly detection tasks.
    /// 用于异常检测任务的Anomalib分割模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anomalib models are designed for unsupervised anomaly detection in images.
    /// They can identify defective or anomalous regions without requiring
    /// labeled anomaly samples during training.
    /// Anomalib模型专为图像中的无监督异常检测而设计。
    /// 它们可以在训练期间不需要标记的异常样本的情况下识别缺陷或异常区域。
    /// </para>
    /// <para>
    /// Common use cases include:
    /// 常见用例包括：
    /// - Industrial defect detection (industry inspection)
    ///   工业缺陷检测（工业检测）
    /// - Quality control in manufacturing
    ///   制造业质量控制
    /// - Medical image anomaly detection
    ///   医学图像异常检测
    /// - Surface defect inspection
    ///   表面缺陷检测
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create Anomalib model for defect detection
    /// // 创建用于缺陷检测的Anomalib模型
    /// var config = new AnomalibSegConfig(
    ///     modelPath: "anomalib_model.onnx",
    ///     inferenceBackend: InferenceBackend.OpenVINO,
    ///     deviceType: DeviceType.CPU);
    /// 
    /// using (var model = new AnomalibSegModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("product.jpg"))
    ///     {
    ///         // Run anomaly detection
    ///         // 运行异常检测
    ///         var results = model.Predict(image);
    ///         
    ///         // results[0] contains anomaly map and score
    ///         // results[0]包含异常图和分数
    ///         Console.WriteLine($"Anomaly Score: {results[0].Confidence}");
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="AnomalibSegConfig"/>
    public class AnomalibSegModel : IAnomalibSegModel
    {
        /// <summary>
        /// Creates a new Anomalib segmentation model instance.
        /// 创建新的Anomalib分割模型实例。
        /// </summary>
        /// <param name="config">Anomalib model configuration / Anomalib模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidOperationException">Thrown when model initialization fails / 当模型初始化失败时抛出</exception>
        /// <remarks>
        /// The configuration should specify the path to an ONNX model exported from Anomalib.
        /// 配置应指定从Anomalib导出的ONNX模型的路径。
        /// </remarks>
        public AnomalibSegModel(AnomalibSegConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses the input image for anomaly detection inference.
        /// 对输入图像进行预处理以进行异常检测推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output parameter for image adjustment info / 图像调整信息的输出参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image format is invalid / 当图像格式无效时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps typically include:
        /// 预处理步骤通常包括：
        /// 1. Resize to model input dimensions
        ///    调整尺寸到模型输入维度
        /// 2. Normalize pixel values (usually to [0, 1] or [-1, 1])
        ///    归一化像素值（通常到[0, 1]或[-1, 1]）
        /// 3. Convert to tensor format (NCHW)
        ///    转换为张量格式(NCHW)
        /// </para>
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");

            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Mat)img,
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
