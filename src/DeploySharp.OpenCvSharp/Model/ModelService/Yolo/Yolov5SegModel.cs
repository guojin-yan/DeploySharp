using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Buffers;
using Size = DeploySharp.Data.Size;
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv5 instance segmentation model implementation.
    /// YOLOv5实例分割模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YOLOv5 Seg extends the YOLOv5 detection capabilities to pixel-level instance segmentation,
    /// providing precise object boundaries for applications requiring detailed object shapes.
    /// YOLOv5 Seg将YOLOv5检测功能扩展到像素级实例分割，为需要详细目标形状的应用提供精确的目标边界。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Pixel-level instance segmentation masks
    ///   像素级实例分割掩码
    /// - Precise object boundaries
    ///   精确的目标边界
    /// - Real-time segmentation performance
    ///   实时分割性能
    /// - Based on YOLOv5 architecture with mask head
    ///   基于YOLOv5架构的掩码头
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create YOLOv5 segmentor
    /// // 创建YOLOv5分割器
    /// var config = new Yolov5SegConfig("yolov5s-seg.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var segmentor = new Yolov5SegModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("scene.jpg"))
    ///     {
    ///         var results = segmentor.Predict(image);
    ///         
    ///         foreach (var seg in results)
    ///         {
    ///             // Apply mask to image
    ///             // 将掩码应用到图像
    ///             var maskedImage = ApplyMask(image, seg.Mask);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Yolov5SegConfig"/>
    /// <seealso cref="Yolov5DetModel"/>
    public class Yolov5SegModel : IYolov5SegModel
    {
        /// <summary>
        /// Creates a new YOLOv5 segmentation model instance.
        /// 创建新的YOLOv5分割模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config is not Yolov5SegConfig / 当config不是Yolov5SegConfig时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and NMS threshold.
        /// 配置指定模型路径、输入尺寸、置信度阈值和NMS阈值。
        /// </remarks>
        public Yolov5SegModel(Yolov5SegConfig config) : base(config) { }

        /// <summary>
        /// Performs instance segmentation on a single image.
        /// 对单张图像执行实例分割。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of segmentation results with masks and bounding boxes / 包含掩码和边界框的分割结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no objects are detected above the confidence threshold.
        /// Each result includes a pixel-level mask along with detection information.
        /// 如果没有检测到高于置信度阈值的目标，则返回空数组。
        /// 每个结果包括像素级掩码以及检测信息。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("scene.jpg"))
        /// {
        ///     var segmentations = segmentor.Predict(image);
        ///     
        ///     foreach (var seg in segmentations)
        ///     {
        ///         // Use mask for further processing
        ///         ProcessMask(seg.Mask, seg.Bounds);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public SegResult[] Predict(Mat img)
        {
            return base.Predict(img) as SegResult[];
        }

        /// <summary>
        /// Performs batch instance segmentation on multiple images.
        /// 对多张图像执行批量实例分割。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of segmentation results for each image / 每张图像的分割结果列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        /// <remarks>
        /// Batch processing is more efficient than sequential single-image processing.
        /// 批处理比顺序单张图像处理更高效。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt; { image1, image2, image3 };
        /// var allResults = segmentor.PredictBatch(images);
        /// 
        /// for (int i = 0; i &lt; allResults.Count; i++)
        /// {
        ///     Console.WriteLine($"Image {i}: {allResults[i].Length} objects segmented");
        /// }
        /// </code>
        /// </example>
        public List<SegResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<SegResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for YOLOv5 segmentation inference.
        /// 对图像进行预处理以进行YOLOv5分割推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Letterbox resize to 640x640 (maintaining aspect ratio with padding)
        ///    Letterbox调整到640x640(保持宽高比并填充)
        /// 2. Normalize pixel values to 0-1 range
        ///    将像素值归一化到0-1范围
        /// 3. Convert BGR to RGB
        ///    将BGR转换为RGB
        /// 4. Convert to NCHW tensor format
        ///    转换为NCHW张量格式
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

        /// <summary>
        /// Preprocesses batch of images for YOLOv5 segmentation inference.
        /// 对批量图像进行预处理以进行YOLOv5分割推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");

            try
            {
                return CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
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
