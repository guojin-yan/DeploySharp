using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static DeploySharp.Data.ImageData<float>;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv8 model for object Segmentation
    /// YOLOv8分割模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv8 Segmentation pipeline including:
    /// 提供标准YOLOv8检测流程，包括：
    /// - Input preprocessing
    ///   输入预处理
    /// - Output decoding
    ///   输出解码
    /// - Confidence filtering
    ///   置信度过滤
    /// - Non-Maximum Suppression
    ///   非极大值抑制
    /// </para>
    /// <para>
    /// Inherits from base IModel interface and implements YOLOv8-specific processing
    /// 继承自基础IModel接口并实现YOLOv8特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov8SegModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv8 detector
        /// 初始化YOLOv8检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov8SegModel(Yolov8SegConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        public SegResult[] Predict(object img)
        {
            return base.Predict(img) as SegResult[];
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

            float[] result0 = dataTensor[0].DataBuffer as float[];
            float[] result1 = dataTensor[1].DataBuffer as float[];

            var config = (Yolov8SegConfig)this.config;
            int rowResultNum = config.OutputSizes[0][2];
            int oneResultLen = config.OutputSizes[0][1];

            int maskLen = config.OutputSizes[1][1];


            var candidateBoxes = new ConcurrentBag<BoundingBox>();
            int initialWidth = config.OutputSizes[1][3];
            int initialHeight = config.OutputSizes[1][2];

            // 4. 并行处理候选框检测
            Parallel.For(0, rowResultNum, i =>
            {
                for (int j = 4; j < oneResultLen - maskLen; j++)  // Iterate through each class
                {
                    float conf = result0[rowResultNum * j + i];
                    int label = j - 4;
                    if (conf > config.ConfidenceThreshold)  // Confidence threshold filtering
                    {
                        // Parse center coordinates, width and height
                        float cx = result0[rowResultNum * 0 + i];
                        float cy = result0[rowResultNum * 1 + i];
                        float ow = result0[rowResultNum * 2 + i];
                        float oh = result0[rowResultNum * 3 + i];

                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = label,
                            Confidence = conf,
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


            var maskPaddingX = imageAdjustmentParam.Padding.First * initialWidth / imageAdjustmentParam.TargetImgSize.Width;
            var maskPaddingY = imageAdjustmentParam.Padding.Second * initialHeight / imageAdjustmentParam.TargetImgSize.Height;
            int validMaskWidth = initialWidth - 2 * maskPaddingX;
            int validMaskHeight = initialHeight - 2 * maskPaddingY;

            // 7. 并行处理每个检测框的掩膜
            var segResults = new SegResult[boxes.Count()];
            //Parallel.For(0, boxes.Count(), index =>
            //{
            for(int index = 0; index < boxes.Count(); ++index)
            { 
                float[] rawMaskData = new float[validMaskWidth * validMaskHeight];
                var box = boxes[index];
                var bounds = imageAdjustmentParam.AdjustRect(box.Box);

                // 8. 快速掩膜数据处理
                float[] maskData = new float[maskLen];
                for (int i = oneResultLen - maskLen; i < oneResultLen; ++i)
                {
                    maskData[i - oneResultLen + maskLen] = result0[rowResultNum * i + box.Index];
                }


                // 9. 向量化掩膜计算
                //Array.Clear(rawMaskData);
                for (int y = 0; y < validMaskHeight; y++)
                {
                    int baseOffset = (y + maskPaddingY) * initialWidth;
                    for (int x = 0; x < validMaskWidth; x++)
                    {
                        float sum = 0;
                        for (int i = 0; i < maskLen; i++)
                        {
                            sum += result1[i * initialWidth * initialHeight + baseOffset + x + maskPaddingX] * maskData[i];
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
                    Mask =  new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
                    Id = classID,
                    Bounds = imageAdjustmentParam.AdjustRect(box.Box),
                    Confidence = box.Confidence,
                    Category = categoryFlag ? category : classID.ToString(),
                };

            }
            //});



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