using DeploySharp.Data;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// Anomalib segmentation model implementation for anomaly detection using ImageSharp
    /// 使用ImageSharp的Anomalib分割模型实现，用于异常检测
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anomalib is a deep learning library for anomaly detection. This model implementation
    /// provides integration with DeploySharp's inference pipeline for anomaly segmentation tasks.
    /// Anomalib是用于异常检测的深度学习库。此模型实现提供与DeploySharp推理流水线的集成，
    /// 用于异常分割任务。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点:
    /// - Supports various anomaly detection algorithms (PatchCore, CFA, STFPM, etc.)
    ///   支持多种异常检测算法(PatchCore、CFA、STFPM等)
    /// - Outputs pixel-level anomaly heatmaps and segmentation masks
    ///   输出像素级异常热图和分割掩膜
    /// - Provides anomaly scores for entire images and individual regions
    ///   为整个图像和单独区域提供异常分数
    /// </para>
    /// <para>
    /// Preprocessing steps:
    /// 预处理步骤:
    /// 1. Load image with ImageSharp
    ///    使用ImageSharp加载图像
    /// 2. Resize to model input dimensions using CvDataProcessor
    ///    使用CvDataProcessor调整大小到模型输入尺寸
    /// 3. Normalize pixel values (typically ImageNet standardization)
    ///    归一化像素值(通常是ImageNet标准化)
    /// 4. Convert to DataTensor format for inference
    ///    转换为DataTensor格式进行推理
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Initialize model
    /// var config = new AnomalibSegConfig("patchcore_model.onnx");
    /// using var model = new AnomalibSegModel(config);
    /// 
    /// // Load image
    /// using var image = Image.Load&lt;Rgb24&gt;("product_image.jpg");
    /// 
    /// // Run inference
    /// var results = model.Predict(image);
    /// 
    /// // Visualize anomaly map
    /// var options = new VisualizeOptions(1.0f);
    /// using var visualized = Visualize.DrawSegResult(results, image, options);
    /// visualized.Save("anomaly_result.jpg");
    /// </code>
    /// </example>
    /// <seealso cref="IAnomalibSegModel"/>
    /// <seealso cref="AnomalibSegConfig"/>
    /// <seealso cref="CvDataProcessor.ImageProcessToDataTensor"/>
    public class AnomalibSegModel : IAnomalibSegModel
    {
        /// <summary>
        /// Initializes a new instance of the AnomalibSegModel with the specified configuration
        /// 使用指定配置初始化AnomalibSegModel的新实例
        /// </summary>
        /// <param name="config">
        /// Model configuration containing path, input size, and preprocessing parameters / 
        /// 包含路径、输入大小和预处理参数的模型配置
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null
        /// 当config为null时抛出
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when model file is not found
        /// 当模型文件未找到时抛出
        /// </exception>
        /// <remarks>
        /// The constructor validates the configuration and initializes the underlying
        /// inference engine (OpenVINO, ONNX Runtime, or TensorRT based on config).
        /// 构造函数验证配置并初始化底层推理引擎（根据配置使用OpenVINO、ONNX Runtime或TensorRT）。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var config = new AnomalibSegConfig(
        ///     modelPath: "models/patchcore.onnx",
        ///     inferenceBackend: InferenceBackend.OpenVINO,
        ///     deviceType: DeviceType.CPU
        /// );
        /// var model = new AnomalibSegModel(config);
        /// </code>
        /// </example>
        public AnomalibSegModel(AnomalibSegConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses an image for model inference
        /// 为模型推理预处理图像
        /// </summary>
        /// <param name="img">Input image as object (expected to be Image&lt;Rgb24&gt;) / 输入图像作为对象（预期为Image&lt;Rgb24&gt;）</param>
        /// <param name="imageAdjustmentParam">
        /// Output parameter containing image adjustment information for post-processing / 
        /// 包含后处理图像调整信息的输出参数
        /// </param>
        /// <returns>Preprocessed DataTensor ready for inference / 准备进行推理的预处理DataTensor</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when img is null
        /// 当img为null时抛出
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown when img is not Image&lt;Rgb24&gt;
        /// 当img不是Image&lt;Rgb24&gt;时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when preprocessing fails
        /// 当预处理失败时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Preprocessing pipeline:
        /// 预处理流水线:
        /// 1. Logs input image dimensions for debugging
        ///    记录输入图像尺寸用于调试
        /// 2. Calls CvDataProcessor.ImageProcessToDataTensor for standard CV preprocessing
        ///    调用CvDataProcessor.ImageProcessToDataTensor进行标准CV预处理
        /// 3. Handles exceptions and logs detailed error information
        ///    处理异常并记录详细错误信息
        /// </para>
        /// <para>
        /// The imageAdjustmentParam is used to map model outputs back to original image coordinates.
        /// imageAdjustmentParam用于将模型输出映射回原始图像坐标。
        /// </para>
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
    }
}
