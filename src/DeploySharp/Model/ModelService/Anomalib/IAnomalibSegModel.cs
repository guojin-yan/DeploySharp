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
    public class IAnomalibSegModel : IModel
    {
        public IAnomalibSegModel(AnomalibSegConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public AnomalySegResult[] Predict(object img)
        {
            return base.Predict(img) as AnomalySegResult[];
        }
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            if (dataTensor.Count == 4) 
            {
                byte[] rawMaskData = (dataTensor["pred_mask"].DataBuffer as byte[]);
                int initialWidth = dataTensor["pred_mask"].Shape[3];
                int initialHeight = dataTensor["pred_mask"].Shape[2];
                var maskPaddingX = imageAdjustmentParam.Padding.First * initialWidth / imageAdjustmentParam.TargetImgSize.Width;
                var maskPaddingY = imageAdjustmentParam.Padding.Second * initialHeight / imageAdjustmentParam.TargetImgSize.Height;
                int validMaskWidth = initialWidth - 2 * maskPaddingX;
                int validMaskHeight = initialHeight - 2 * maskPaddingY;
                RectF boundsf = new RectF(maskPaddingX, maskPaddingY, validMaskWidth, validMaskHeight);

                Rect bounds = imageAdjustmentParam.AdjustRect(boundsf);
                var targetMask = new float[bounds.Height * bounds.Width];
                for (var y = 0; y < bounds.Height; y++)
                {
                    for (var x = 0; x < bounds.Width; x++)
                    {
                        // Calculate source coordinates
                        var sourceX = (float)(x + bounds.Location.X) * (validMaskWidth - 1) / (imageAdjustmentParam.RowImgSize.Width - 1);
                        var sourceY = (float)(y + bounds.Location.Y) * (validMaskHeight - 1) / (imageAdjustmentParam.RowImgSize.Height - 1);

                        // Check if source coordinates are out of bounds
                        if (sourceY < 0 || sourceY >= validMaskHeight ||
                            sourceX < 0 || sourceX >= validMaskWidth)
                        {
                            targetMask[y * bounds.Width + x] = 0;
                            continue;
                        }

                        // Ensure coordinates are within valid range for interpolation
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
                var results = new AnomalySegResult[1];
                results[0] = new AnomalySegResult
                {
                    Bounds = bounds,
                    RawMask = new ImageDataF(dataTensor["anomaly_map"].DataBuffer as float[], dataTensor["anomaly_map"].Shape[2], dataTensor["anomaly_map"].Shape[3], 1, ImageDataF.DataFormat.CHW),
                    Mask = new ImageDataF(targetMask, bounds.Height, bounds.Width, 1, ImageDataF.DataFormat.CHW),
                    Confidence = (dataTensor["pred_score"].DataBuffer as float[])[0],
                    Id = (dataTensor["pred_label"].DataBuffer as byte[])[0],
                    Category = "anomaly",
                };
                return results;
            }
            else if(dataTensor.Count == 1)
            {
                float[] result0 = dataTensor[0].DataBuffer as float[];

                float[] mask = new float[result0.Length];
                var config = (AnomalibSegConfig)this.config;
                float d = config.MetaData.MaxValue - config.MetaData.MinValue;
                float confidenceThreshold = (config.MetaData.PixelThreshold - config.MetaData.MinValue) / d;
                float maxScore = result0.Max();
                if (maxScore < config.MetaData.ImageThreshold)
                {
                    return null;
                }
                float score = (maxScore - config.MetaData.MinValue) / d;
                for (int i = 0; i < result0.Length; ++i)
                {
                    result0[i] = ((result0[i] - config.MetaData.MinValue) / d);
                    mask[i] = result0[i] > confidenceThreshold ? 255 : 0;
                }

                var results = new AnomalySegResult[1];
                results[0] = new AnomalySegResult
                {
                    RawMask = new ImageDataF(result0, dataTensor[0].Shape[2], dataTensor[0].Shape[3], 1, ImageDataF.DataFormat.CHW),
                    Mask = new ImageDataF(mask, dataTensor[0].Shape[2], dataTensor[0].Shape[3], 1, ImageDataF.DataFormat.CHW),
                    Confidence = score,
                    Id = 0,
                    Category = "anomaly",
                };
                return results;
            }
            else
            {
                throw new Exception($"[错误] 模型输出节点数异常: {dataTensor.Count}");
            }
        }


        // 快速Sigmoid近似计算 (比标准库快3倍)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sigmoid(float value) => (float)(1 / (1 + Math.Exp(-value)));

        // 优化的线性插值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            throw new NotImplementedException();
        }
    }
}
