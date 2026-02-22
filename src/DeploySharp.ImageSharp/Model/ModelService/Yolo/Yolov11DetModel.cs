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
    /// YOLOv11 object detection model implementation
    /// YOLOv11目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv11 represents the latest evolution in the YOLO series with state-of-the-art
    /// object detection capabilities. It incorporates modern architectural patterns
    /// and training techniques for superior performance.
    /// YOLOv11代表YOLO系列的最新演进，具有最先进的物体检测能力。
    /// 它结合了现代架构模式和训练技术以实现卓越性能。
    /// </para>
    /// <para>
    /// YOLOv11 features:
    /// YOLOv11特性:
    /// - Advanced anchor-free detection head
    ///   先进的无锚点检测头
    /// - Improved backbone with better feature representation
    ///   改进的骨干网络，具有更好的特征表示
    /// - Efficient architecture for real-time applications
    ///   适用于实时应用的高效架构
    /// - Strong performance on COCO and custom datasets
    ///   在COCO和自定义数据集上表现强劲
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Initialize model with OpenVINO backend
    /// var config = new Yolov11DetConfig(
    ///     modelPath: "yolov11.onnx",
    ///     inferenceBackend: InferenceBackend.OpenVINO,
    ///     deviceType: DeviceType.GPU
    /// );
    /// using var model = new Yolov11DetModel(config);
    /// 
    /// // Load and process image
    /// using var image = Image.Load&lt;Rgb24&gt;("scene.jpg");
    /// var detections = model.Predict(image);
    /// 
    /// // Filter high-confidence detections
    /// var confident = detections.Where(d =&gt; d.Confidence &gt; 0.7);
    /// </code>
    /// </example>
    /// <seealso cref="IYolov11DetModel"/>
    /// <seealso cref="Yolov11DetConfig"/>
    public class Yolov11DetModel : IYolov11DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// 构造函数使用模型配置初始化
        /// </summary>
        /// <param name="config">YOLOv11 detection model configuration / YOLOv11检测模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
        /// <exception cref="ArgumentException">Thrown when config has invalid parameters</exception>
        /// <remarks>
        /// Validates the configuration and initializes the inference backend.
        /// 验证配置并初始化推理后端。
        /// </remarks>
        public Yolov11DetModel(Yolov11DetConfig config) : base(config) { }


        /// <summary>
        /// Preprocesses image for YOLOv11 inference
        /// 为YOLOv11推理预处理图像
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed DataTensor / 预处理后的DataTensor</returns>
        /// <remarks>
        /// Standard preprocessing pipeline: resize → normalize → tensor conversion.
        /// 标准预处理流程：调整大小 → 归一化 → 张量转换。
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
        /// Preprocesses batch of images for YOLOv11 inference
        /// 为YOLOv11推理预处理批量图像
        /// </summary>
        /// <param name="img">List of images / 图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Batched DataTensor / 批量DataTensor</returns>
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
