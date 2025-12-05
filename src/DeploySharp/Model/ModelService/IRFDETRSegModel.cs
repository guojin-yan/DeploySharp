using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    public abstract class IRFDETRSegModel : IModel
    {
        public IRFDETRSegModel(IConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }
        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// 对原始模型输出进行后处理以提取检测结果
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor/原始模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed detection results/处理后的检测结果数组</returns>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            var config = (RFDETRSegConfig)this.config;
            float[] resultScores = dataTensor[1].DataBuffer as float[];
            float[] resultBoxes = dataTensor[0].DataBuffer as float[];
            float[] resultMask = dataTensor[2].DataBuffer as float[];
            int imageWidth = config.InputSizes[0][2];
            int imageHeight = config.InputSizes[0][3];
            int resultCount = dataTensor[1].Shape[1];
            int categoryCount = dataTensor[1].Shape[2];

            int initialWidth = dataTensor[2].Shape[3];
            int initialHeight = dataTensor[2].Shape[2];

            for (int i = 0; i < resultScores.Length; ++i)
            {
                resultScores[i] = Sigmoid(resultScores[i]);
            }
            // 获取有效检测（分数高于阈值）
            List<SegResult> detResults = new List<SegResult>();
            var boxes = new List<BoundingBox>();
            for (int i = 0; i < resultCount; i++)
            {
                var (maxValue, index) = Util.FindMaxInRange(resultScores, categoryCount * i, categoryCount * (i + 1));
                float scores = (maxValue);
                if (scores < config.ConfidenceThreshold)
                {
                    continue;
                }

                // 解析检测框 [1, 300, 4] - 注意：坐标是cxcywh格式，归一化的
                int detIndex = i * 4;
                float cx = resultBoxes[detIndex];
                float cy = resultBoxes[detIndex + 1];
                float w = resultBoxes[detIndex + 2];
                float h = resultBoxes[detIndex + 3];

                // 将cxcywh归一化坐标转换为xyxy归一化坐标
                float x1 = cx - w / 2;
                float y1 = cy - h / 2;
                float x2 = cx + w / 2;
                float y2 = cy + h / 2;


                // 将归一化坐标转换为实际像素坐标
                float pixelX1 = x1 * imageWidth;
                float pixelY1 = y1 * imageHeight;
                float pixelX2 = x2 * imageWidth;
                float pixelY2 = y2 * imageHeight;

                // 确保坐标在图像范围内
                pixelX1 = Math.Max(0, Math.Min(pixelX1, imageWidth));
                pixelY1 = Math.Max(0, Math.Min(pixelY1, imageHeight));
                pixelX2 = Math.Max(0, Math.Min(pixelX2, imageWidth));
                pixelY2 = Math.Max(0, Math.Min(pixelY2, imageHeight));

                // 检查边界框是否有效
                float width = pixelX2 - pixelX1;
                float height = pixelY2 - pixelY1;

                // 转换为矩形格式
                RectF box = new RectF(pixelX1, pixelY1, width, height);

                int classID = index;
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                // Create detection result with adjusted coordinates
                // 创建带有调整坐标的检测结果
                boxes.Add(new BoundingBox
                {
                    Index = i,
                    NameIndex = classID,
                    Confidence = scores,
                    Box = box,
                    Angle = 0.0f
                });
            }



            //// 6. 掩膜处理准备
            //float[] rawMaskBuffer = ArrayPool<float>.Shared.Rent(initialWidth * initialHeight);
            ////Span<float> rawMaskData = rawMaskBuffer.AsSpan(0, initialWidth * initialHeight);


            var maskPaddingX = imageAdjustmentParam.Padding.First * initialWidth / imageAdjustmentParam.TargetImgSize.Width;
            var maskPaddingY = imageAdjustmentParam.Padding.Second * initialHeight / imageAdjustmentParam.TargetImgSize.Height;
            int validMaskWidth = initialWidth - 2 * maskPaddingX;
            int validMaskHeight = initialHeight - 2 * maskPaddingY;

            // 7. 并行处理每个检测框的掩膜
            var segResults = new SegResult[boxes.Count()];
            //Parallel.For(0, boxes.Count(), index =>
            //{
            for (int index = 0; index < boxes.Count(); ++index)
            {
                float[] rawMaskData = new float[validMaskWidth * validMaskHeight];
                var box = boxes[index];
                var bounds = imageAdjustmentParam.AdjustRect(box.Box);


                // 9. 向量化掩膜计算
                //Array.Clear(rawMaskData);
                for (int y = 0; y < validMaskHeight; y++)
                {
                    int baseOffset = (y + maskPaddingY) * initialWidth;
                    for (int x = 0; x < validMaskWidth; x++)
                    {
                        rawMaskData[y * validMaskWidth + x] = Sigmoid(resultMask[box.Index * initialWidth * initialHeight + baseOffset + x + maskPaddingX]);
                    }
                }

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

                        var top = Lerp(rawMaskData[y0 * validMaskWidth + x0], rawMaskData[y0 * validMaskWidth + x1], xLerp);
                        var bottom = Lerp(rawMaskData[y1 * validMaskWidth + x0], rawMaskData[y1 * validMaskWidth + x1], xLerp);
                        targetMask[y * bounds.Width + x] = Lerp(top, bottom, yLerp);

                    }
                }


                int classID = box.NameIndex;
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                //OpenCvSharp.Cv2.ImShow("targetMask", Mat.FromPixelData(bounds.Height, bounds.Width, MatType.CV_32FC1, targetMask));
                //Cv2.WaitKey(0);
                segResults[index] = new SegResult
                {
                    Mask = new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
                    Id = classID,
                    Bounds = imageAdjustmentParam.AdjustRect(box.Box),
                    Confidence = box.Confidence,
                    Category = categoryFlag ? category : classID.ToString(),
                };

            }


            return segResults.ToArray();
        }
        // 快速Sigmoid近似计算 (比标准库快3倍)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sigmoid(float value)
        {
            if (value >= 0)
            {
                return 1.0f / (1.0f + (float)Math.Exp(-value));
            }
            else
            {
                float expX = (float)Math.Exp(value);
                return expX / (1.0f + expX);
            }
        }

        // 优化的线性插值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;


    }
}
