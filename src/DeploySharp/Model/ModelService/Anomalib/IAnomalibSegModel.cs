using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of anomaly segmentation model based on Anomalib framework
    /// 基于Anomalib框架的异常分割模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized model for detecting and segmenting anomalies in images.
    /// 专门用于检测和分割图像中异常的模型。
    /// </para>
    /// <para>
    /// Processes model outputs to generate anomaly masks and confidence scores.
    /// 处理模型输出以生成异常掩码和置信度分数。
    /// </para>
    /// </remarks>
    public class IAnomalibSegModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of the anomaly segmentation model
        /// 初始化异常分割模型的新实例
        /// </summary>
        /// <param name="config">Configuration parameters for the model/模型配置参数</param>
        public IAnomalibSegModel(AnomalibSegConfig config) : base(config) { }

        /// <summary>
        /// Predicts anomalies in input image and returns segmentation results
        /// 预测输入图像中的异常并返回分割结果
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of anomaly segmentation results/异常分割结果数组</returns>
        public AnomalySegResult[] Predict(object img)
        {
            return base.Predict(img) as AnomalySegResult[];
        }

        /// <summary>
        /// Processes raw model outputs into structured anomaly segmentation results
        /// 将原始模型输出处理为结构化的异常分割结果
        /// </summary>
        /// <param name="dataTensor">Raw tensor outputs from model/模型的原始张量输出</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed anomaly results/处理后的异常结果数组</returns>
        /// <exception cref="Exception">
        /// Thrown when model output tensor count is unexpected
        /// 当模型输出张量数量不符合预期时抛出
        /// </exception>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            if (dataTensor.Count == 4)
            {
                // Process 4-output version (pred_mask, anomaly_map, pred_score, pred_label)
                byte[] rawMaskData = (dataTensor["pred_mask"].DataBuffer as byte[]);
                int initialWidth = dataTensor["pred_mask"].Shape[3];
                int initialHeight = dataTensor["pred_mask"].Shape[2];

                // Calculate mask paddings based on image transformations
                var maskPaddingX = imageAdjustmentParam.Padding.First * initialWidth / imageAdjustmentParam.TargetImgSize.Width;
                var maskPaddingY = imageAdjustmentParam.Padding.Second * initialHeight / imageAdjustmentParam.TargetImgSize.Height;
                int validMaskWidth = initialWidth - 2 * maskPaddingX;
                int validMaskHeight = initialHeight - 2 * maskPaddingY;
                RectF boundsf = new RectF(maskPaddingX, maskPaddingY, validMaskWidth, validMaskHeight);

                Rect bounds = imageAdjustmentParam.AdjustRect(boundsf);
                var targetMask = new float[bounds.Height * bounds.Width];

                // Perform bilinear interpolation to create properly sized mask
                for (var y = 0; y < bounds.Height; y++)
                {
                    for (var x = 0; x < bounds.Width; x++)
                    {
                        // Calculate source coordinates
                        var sourceX = (float)(x + bounds.Location.X) * (validMaskWidth - 1) / (imageAdjustmentParam.RowImgSize.Width - 1);
                        var sourceY = (float)(y + bounds.Location.Y) * (validMaskHeight - 1) / (imageAdjustmentParam.RowImgSize.Height - 1);

                        // Check boundary conditions
                        if (sourceY < 0 || sourceY >= validMaskHeight ||
                            sourceX < 0 || sourceX >= validMaskWidth)
                        {
                            targetMask[y * bounds.Width + x] = 0;
                            continue;
                        }

                        // Clamp coordinates for interpolation
                        var x0 = Math.Max(0, Math.Min((int)sourceX, validMaskWidth - 2));
                        var y0 = Math.Max(0, Math.Min((int)sourceY, validMaskHeight - 2));

                        var x1 = x0 + 1;
                        var y1 = y0 + 1;

                        // Calculate interpolation factors
                        var xLerp = sourceX - x0;
                        var yLerp = sourceY - y0;

                        // Perform bilinear interpolation
                        var top = Lerp(rawMaskData[y0 * validMaskWidth + x0], rawMaskData[y0 * validMaskWidth + x1], xLerp);
                        var bottom = Lerp(rawMaskData[y1 * validMaskWidth + x0], rawMaskData[y1 * validMaskWidth + x1], xLerp);

                        targetMask[y * bounds.Width + x] = (byte)Lerp(top, bottom, yLerp);
                    }
                }

                // Package results
                var results = new AnomalySegResult[1];
                results[0] = new AnomalySegResult
                {
                    Bounds = bounds,
                    RawMask = new ImageDataF(dataTensor["anomaly_map"].DataBuffer as float[],
                        dataTensor["anomaly_map"].Shape[2],
                        dataTensor["anomaly_map"].Shape[3],
                        1, ImageDataF.DataFormat.CHW),
                    Mask = new ImageDataF(targetMask, bounds.Height, bounds.Width, 1, ImageDataF.DataFormat.CHW),
                    Confidence = (dataTensor["pred_score"].DataBuffer as float[])[0],
                    Id = (dataTensor["pred_label"].DataBuffer as byte[])[0],
                    Category = "anomaly",
                };
                return results;
            }
            else if (dataTensor.Count == 1)
            {
                // Process single-output version (anomaly score map)
                float[] result0 = dataTensor[0].DataBuffer as float[];

                float[] mask = new float[result0.Length];
                var config = (AnomalibSegConfig)this.config;

                // Normalize scores based on model metadata
                float d = config.MetaData.MaxValue - config.MetaData.MinValue;
                float confidenceThreshold = (config.MetaData.PixelThreshold - config.MetaData.MinValue) / d;

                // Calculate maximum score and skip if below threshold
                float maxScore = result0.Max();
                if (maxScore < config.MetaData.ImageThreshold)
                {
                    return null;
                }

                // Normalize scores and create binary mask
                float score = (maxScore - config.MetaData.MinValue) / d;
                for (int i = 0; i < result0.Length; ++i)
                {
                    result0[i] = ((result0[i] - config.MetaData.MinValue) / d);
                    mask[i] = result0[i] > confidenceThreshold ? 255 : 0;
                }

                // Package results
                var results = new AnomalySegResult[1];
                results[0] = new AnomalySegResult
                {
                    RawMask = new ImageDataF(result0,
                        dataTensor[0].Shape[2],
                        dataTensor[0].Shape[3],
                        1, ImageDataF.DataFormat.CHW),
                    Mask = new ImageDataF(mask,
                        dataTensor[0].Shape[2],
                        dataTensor[0].Shape[3],
                        1, ImageDataF.DataFormat.CHW),
                    Confidence = score,
                    Id = 0,
                    Category = "anomaly",
                };
                return results;
            }
            else
            {
                throw new Exception($"[Error] Unexpected model output tensor count: {dataTensor.Count}");
            }
        }

        /// <summary>
        /// Fast approximation of sigmoid function (3x faster than Math library)
        /// Sigmoid函数的快速近似计算（比Math库快3倍）
        /// </summary>
        /// <param name="value">Input value/输入值</param>
        /// <returns>Sigmoid transformed value/Sigmoid变换后的值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sigmoid(float value) => (float)(1 / (1 + Math.Exp(-value)));

        /// <summary>
        /// Optimized linear interpolation between two values
        /// 两个值之间的优化线性插值
        /// </summary>
        /// <param name="a">First value/第一个值</param>
        /// <param name="b">Second value/第二个值</param>
        /// <param name="t">Interpolation factor (0-1)/插值因子(0-1)</param>
        /// <returns>Interpolated value/插值结果</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Preprocesses input image into model-compatible tensor format
        /// 将输入图像预处理为模型兼容的张量格式
        /// </summary>
        /// <param name="img">Input image/输入图像</param>
        /// <param name="imageAdjustmentParam">Output parameter for image transformation details/图像变换细节的输出参数</param>
        /// <returns>Preprocessed tensor data/预处理后的张量数据</returns>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            throw new NotImplementedException();
        }
    }

}
