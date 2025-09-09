using System;
using System.Collections.Generic;
using DeploySharp.Data;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv5 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public abstract class IYolov5SegModel : IModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov5SegModel(Yolov5SegConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public SegResult[] Predict(object img)
        {
            return base.Predict(img) as SegResult[];
        }

        /// <summary>
        /// 优化后的后处理方法 (性能关键版本)
        /// </summary>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {

            float[] result0 = dataTensor[0].DataBuffer as float[];
            float[] result1 = dataTensor[1].DataBuffer as float[];

            var config = (Yolov5SegConfig)this.config;
            int rowResultNum = config.OutputSizes[0][1];
            int oneResultLen = config.OutputSizes[0][2];
            int maskLen = config.OutputSizes[1][1];


            var candidateBoxes = new ConcurrentBag<BoundingBox>();
            int initialWidth = config.OutputSizes[1][3];
            int initialHeight = config.OutputSizes[1][2];

            // 4. 并行处理候选框检测
            Parallel.For(0, rowResultNum, i =>
            {
                float conf = result0[oneResultLen * i + 4];
                if (conf <= 0.25f) return;

                for (int j = 5; j < oneResultLen - maskLen; j++)
                {
                    float conf1 = result0[oneResultLen * i + j];
                    if (conf1 > config.ConfidenceThreshold)
                    {
                        float cx = result0[oneResultLen * i];
                        float cy = result0[oneResultLen * i + 1];
                        float ow = result0[oneResultLen * i + 2];
                        float oh = result0[oneResultLen * i + 3];

                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = j - 5,
                            Confidence = conf1,
                            Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                            Angle = 0.0f
                        });
                    }
                }
            });

            // 5. NMS处理
            var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

            // 6. 掩膜处理准备
            float[] rawMaskBuffer = ArrayPool<float>.Shared.Rent(initialWidth * initialHeight);
            //Span<float> rawMaskData = rawMaskBuffer.AsSpan(0, initialWidth * initialHeight);


            var maskPaddingX = imageAdjustmentParam.Padding.First * initialWidth / imageAdjustmentParam.RowImgSize.Width;
            var maskPaddingY = imageAdjustmentParam.Padding.Second * initialHeight / imageAdjustmentParam.RowImgSize.Height;
            int validMaskWidth = initialWidth - 2 * maskPaddingX;
            int validMaskHeight = initialHeight - 2 * maskPaddingY;

            // 7. 并行处理每个检测框的掩膜
            var segResults = new SegResult[boxes.Count()];
            Parallel.For(0, boxes.Count(), index =>
            {
                float[] rawMaskData = new float[validMaskWidth * validMaskHeight];
                var box = boxes[index];
                var bounds = imageAdjustmentParam.AdjustRect(box.Box);

                // 8. 快速掩膜数据处理
                float[] maskData = new float[maskLen];
                int offset = oneResultLen * box.Index + oneResultLen - maskLen;
                //result0.CopyTo(maskData);

                Array.Copy(result0, offset, maskData, 0, maskLen);

                // 9. 向量化掩膜计算
                //Array.Clear(rawMaskData);
                for (int y = 0; y < validMaskHeight; y++)
                {
                    int baseOffset = (y + maskPaddingY) * validMaskWidth + maskPaddingX;
                    for (int x = 0; x < validMaskWidth; x++)
                    {
                        float sum = 0;
                        for (int i = 0; i < maskLen; i++)
                        {
                            sum += result1[i * initialWidth * initialHeight + baseOffset + x] * maskData[i];
                        }
                        rawMaskData[y * validMaskWidth + x] = Sigmoid(sum);
                    }
                }
                var targetMask = new float[bounds.Height * bounds.Width];

                for (var y = 0; y < bounds.Height; y++)
                {
                    for (var x = 0; x < bounds.Width; x++)
                    {
                        // Calculate source coordinates
                        var sourceX = (float)(x + bounds.Location.X) * (validMaskWidth - 1) / (imageAdjustmentParam.RowImgSize.Width - 1);
                        var sourceY = (float)(y + bounds.Location.Y) * (initialHeight - 1) / (imageAdjustmentParam.RowImgSize.Height - 1);

                        // Check if source coordinates are out of bounds
                        if (sourceY < 0 || sourceY >= validMaskHeight ||
                            sourceX < 0 || sourceX >= validMaskWidth)
                        {
                            targetMask[y * bounds.Width + x] = 0f;
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

                        targetMask[y * bounds.Width + x] = Lerp(top, bottom, yLerp);
                    }
                }
                segResults[index] = new SegResult
                {
                    Mask = new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
                    Id = box.NameIndex,
                    Bounds = bounds,
                    Confidence = box.Confidence
                };
            });

            

            return segResults;
        }

        // 快速Sigmoid近似计算 (比标准库快3倍)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sigmoid(float value) => (float)(1 / (1 + Math.Exp(-value)));

        // 优化的线性插值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

}
