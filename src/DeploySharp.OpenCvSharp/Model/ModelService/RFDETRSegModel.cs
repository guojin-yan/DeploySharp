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
    /// RFDETR (Real-time Feature Detection with Transformers) instance segmentation model implementation.
    /// RFDETR(实时特征检测与变换器)实例分割模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFDETR Seg extends the transformer-based detection architecture to instance segmentation,
    /// combining global context understanding with precise pixel-level segmentation.
    /// RFDETR Seg将基于变换器的检测架构扩展到实例分割，将全局上下文理解与精确的像素级分割相结合。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Transformer-based segmentation architecture
    ///   基于变换器的分割架构
    /// - Global context for segmentation
    ///   分割的全局上下文
    /// - Precise instance boundaries
    ///   精确的实例边界
    /// - Real-time segmentation performance
    ///   实时分割性能
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create RFDETR segmentor
    /// // 创建RFDETR分割器
    /// var config = new RFDETRSegConfig("rfdetr-seg.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var segmentor = new RFDETRSegModel(config))
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
    /// <seealso cref="RFDETRSegConfig"/>
    /// <seealso cref="RFDETRDetModel"/>
    public class RFDETRSegModel : IRFDETRSegModel
    {
        /// <summary>
        /// Creates a new RFDETR segmentation model instance.
        /// 创建新的RFDETR分割模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and other parameters.
        /// 配置指定模型路径、输入尺寸、置信度阈值和其他参数。
        /// </remarks>
        public RFDETRSegModel(IConfig config) : base(config)
        {
        }

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
        /// Preprocesses image for RFDETR segmentation inference.
        /// 对图像进行预处理以进行RFDETR分割推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Resize to model input size
        ///    调整到模型输入尺寸
        /// 2. Normalize pixel values
        ///    归一化像素值
        /// 3. Convert to tensor format
        ///    转换为张量格式
        /// </para>
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageProcessToDataTensor(
                    (Mat)img,
                    config,
                    out imageAdjustmentParam);

                //long[] data = new long[config.InputSizes[1][1]];
                //data[0] = (long)imageAdjustmentParam.RowImgSize.Width;
                //data[1] = (long)imageAdjustmentParam.RowImgSize.Height;
                //dataTensors.AddNode(
                //    config.InputNames[1],
                //    0,
                //    TensorType.Input,
                //    data,
                //    config.InputSizes[1],
                //    typeof(long));
                return dataTensors;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images for RFDETR segmentation inference.
        /// 对批量图像进行预处理以进行RFDETR分割推理。
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
                DataTensor dataTensors = CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
                    config,
                    out imageAdjustmentParam);

                long[] data = new long[config.InputSizes[1][0] * config.InputSizes[1][1]];
                for (int b = 0; b < 2; ++b)
                {

                    data[b * config.InputSizes[1][1]] = (long)imageAdjustmentParam[b].RowImgSize.Width;
                    data[b * config.InputSizes[1][1] + 1] = (long)imageAdjustmentParam[b].RowImgSize.Height;
                }

                dataTensors.AddNode(
                    config.InputNames[1],
                    0,
                    TensorType.Input,
                    data,
                    config.InputSizes[1],
                    typeof(long));

                return dataTensors;

            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }
    }
}

