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

        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            // 单图处理直接调用 Batch 逻辑的简化版，保持核心算法一致
            var batchResult = PostprocessBatchInternal(dataTensor, new[] { imageAdjustmentParam });
            return batchResult[0];
        }
        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            // 直接返回内部处理结果
            return PostprocessBatchInternal(dataTensor, imageAdjustmentParams);
        }
        /// <summary>
        /// 统一的后处理核心逻辑 (支持多线程 Batch 处理)
        /// </summary>
        private List<Result[]> PostprocessBatchInternal(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];
            float[] result1 = dataTensor[1].DataBuffer as float[];
            var config = (Yolov8SegConfig)this.config;

            // --- 1. 维度参数准备 ---
            int rowResultNum = config.OutputSizes[0][2]; // 8400
            int oneResultLen = config.OutputSizes[0][1]; // 116 (4 bbox + 80 class + 32 mask)
            int batchSize = config.InferBatch;
            int resultSizePerBatch = rowResultNum * oneResultLen;
            int maskLen = config.OutputSizes[1][1]; // 32
            int initialWidth = config.OutputSizes[1][3]; // 160
            int initialHeight = config.OutputSizes[1][2]; // 160
            int protoArea = initialWidth * initialHeight;
            float confThreshold = config.ConfidenceThreshold;
            Result[][] results = new Result[batchSize][];
            // --- 2. Batch 并行处理 (利用多核 CPU) ---
            Parallel.For(0, batchSize, b =>
            {
                int batchOffset = b * resultSizePerBatch;
                int protoBatchOffset = b * maskLen * protoArea;
                // 2.1 候选框提取 (使用 List 代替 ConcurrentBag 提升性能)
                var candidateBoxes = new List<BoundingBox>();

                //// 并行处理 Grid
                //Parallel.For(0, rowResultNum, i =>
                //{
                //    // 预计算该 Grid 点在 result0 中的基准偏移
                //    int baseIdx = batchOffset + i;

                //    // 提取坐标 (所有类别共享)
                //    float cx = result0[baseIdx];
                //    float cy = result0[baseIdx + rowResultNum];
                //    float ow = result0[baseIdx + rowResultNum * 2];
                //    float oh = result0[baseIdx + rowResultNum * 3];
                //    // 遍历类别
                //    for (int j = 4; j < oneResultLen - maskLen; j++)
                //    {
                //        float conf = result0[baseIdx + rowResultNum * j];
                //        if (conf > confThreshold)
                //        {
                //            int label = j - 4;
                //            // 坐标转换: cx, cy, w, h -> x, y, w, h
                //            candidateBoxes.Add(new BoundingBox
                //            {
                //                Index = i,
                //                NameIndex = label,
                //                Confidence = conf,
                //                Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                //                Angle = 0.0f
                //            });
                //        }
                //    }
                //});


                // 1. 准备并行数据源（索引 0 到 rowResultNum）
                var parallelQuery = ParallelEnumerable.Range(0, rowResultNum);
                // 2. 并行处理并收集结果到线程安全集合
                // ConcurrentBag 是线程安全的无序集合
                var concurrentBoxes = new ConcurrentBag<BoundingBox>();
                parallelQuery.ForAll(i =>
                {
                    int baseIdx = batchOffset + i;
                    float cx = result0[baseIdx];
                    float cy = result0[baseIdx + rowResultNum];
                    float ow = result0[baseIdx + rowResultNum * 2];
                    float oh = result0[baseIdx + rowResultNum * 3];
                    for (int j = 4; j < oneResultLen - maskLen; j++)
                    {
                        float conf = result0[baseIdx + rowResultNum * j];
                        if (conf > confThreshold)
                        {
                            int label = j - 4;
                            // 使用 Add 替代
                            concurrentBoxes.Add(new BoundingBox
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
                // 3. (可选) 如果后续需要 List，再转换回来
                candidateBoxes = concurrentBoxes.ToList();
                // 2.2 NMS
                var boxes = config.NonMaxSuppression.Run(candidateBoxes, config.NmsThreshold);
                int boxCount = boxes.Count();
                if (boxCount == 0)
                {
                    results[b] = Array.Empty<SegResult>();
                    return;
                }
                // 2.3 掩膜预处理
                var param = imageAdjustmentParams[b];
                int maskPaddingX = (int)(param.Padding.First * initialWidth / (float)param.TargetImgSize.Width);
                int maskPaddingY = (int)(param.Padding.Second * initialHeight / (float)param.TargetImgSize.Height);
                int validMaskWidth = initialWidth - 2 * maskPaddingX;
                int validMaskHeight = initialHeight - 2 * maskPaddingY;
                // 2.4 并行处理每个 Box 的掩膜生成
                var segResults = new SegResult[boxCount];

                // 如果 Box 数量较少，使用串行；如果多则并行 (此处直接并行，通常性能更好)
                Parallel.For(0, boxCount, index =>
                {
                    var box = boxes[index];
                    var bounds = param.AdjustRect(box.Box);
                    // --- A. 提取 Mask Coefficients (使用 ArrayPool) ---
                    float[] maskCoeffs = ArrayPool<float>.Shared.Rent(maskLen);
                    int coeffStartIdx = batchOffset + box.Index + rowResultNum * (oneResultLen - maskLen);

                    // 快速复制 Coeffs
                    for (int k = 0; k < maskLen; k++)
                    {
                        maskCoeffs[k] = result0[coeffStartIdx + k * rowResultNum];
                    }
                    // --- B. 矩阵乘法生成掩膜 (MatMul) ---
                    float[] rawMaskData = ArrayPool<float>.Shared.Rent(validMaskWidth * validMaskHeight);

                    // 核心计算循环：循环展开
                    for (int y = 0; y < validMaskHeight; y++)
                    {
                        int yProto = y + maskPaddingY;
                        int baseOffsetProto = yProto * initialWidth;
                        int rowOffsetMask = y * validMaskWidth;
                        for (int x = 0; x < validMaskWidth; x++)
                        {
                            int xProto = x + maskPaddingX;
                            int pixelIdxProto = baseOffsetProto + xProto;
                            int pixelIdxMask = rowOffsetMask + x;
                            float sum = 0;
                            // 4次循环展开，提升指令级并行
                            int k;
                            for (k = 0; k <= maskLen - 4; k += 4)
                            {
                                int pBase = protoBatchOffset + pixelIdxProto;
                                sum += maskCoeffs[k] * result1[pBase + k * protoArea];
                                sum += maskCoeffs[k + 1] * result1[pBase + (k + 1) * protoArea];
                                sum += maskCoeffs[k + 2] * result1[pBase + (k + 2) * protoArea];
                                sum += maskCoeffs[k + 3] * result1[pBase + (k + 3) * protoArea];
                            }
                            // 处理剩余部分
                            for (; k < maskLen; k++)
                            {
                                sum += maskCoeffs[k] * result1[protoBatchOffset + k * protoArea + pixelIdxProto];
                            }
                            rawMaskData[pixelIdxMask] = FastSigmoid(sum);
                        }
                    }
                    // --- C. 双线性插值 ---
                    float[] targetMask = new float[bounds.Width * bounds.Height];

                    // 预计算缩放比例
                    float scaleX = (validMaskWidth - 1f) / (param.RowImgSize.Width - 1f);
                    float scaleY = (validMaskHeight - 1f) / (param.RowImgSize.Height - 1f);
                    int w = bounds.Width;
                    int h = bounds.Height;
                    int bx = bounds.Location.X;
                    int by = bounds.Location.Y;
                    for (int y = 0; y < h; y++)
                    {
                        float srcY = (y + by) * scaleY;
                        int y0 = (int)srcY;

                        // 边界检查
                        if (y0 >= 0 && y0 < validMaskHeight - 1)
                        {
                            int y1 = y0 + 1;
                            float yLerp = srcY - y0;
                            int row0Offset = y0 * validMaskWidth;
                            int row1Offset = y1 * validMaskWidth;
                            int targetRowOffset = y * w;
                            for (int x = 0; x < w; x++)
                            {
                                float srcX = (x + bx) * scaleX;
                                if (srcX >= 0 && srcX < validMaskWidth - 1)
                                {
                                    int x0 = (int)srcX;
                                    int x1 = x0 + 1;
                                    float xLerp = srcX - x0;
                                    // 计算插值
                                    float top = rawMaskData[row0Offset + x0] + (rawMaskData[row0Offset + x1] - rawMaskData[row0Offset + x0]) * xLerp;
                                    float bottom = rawMaskData[row1Offset + x0] + (rawMaskData[row1Offset + x1] - rawMaskData[row1Offset + x0]) * xLerp;

                                    targetMask[targetRowOffset + x] = top + (bottom - top) * yLerp;
                                }
                            }
                        }
                    }
                    // --- D. 结果封装 ---
                    bool categoryFlag = config.CategoryDict.TryGetValue(box.NameIndex, out string category);
                    segResults[index] = new SegResult
                    {
                        Mask = new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
                        Id = box.NameIndex,
                        Bounds = bounds,
                        Confidence = box.Confidence,
                        Category = categoryFlag ? category : box.NameIndex.ToString(),
                    };
                    // 归还 ArrayPool
                    ArrayPool<float>.Shared.Return(maskCoeffs);
                    ArrayPool<float>.Shared.Return(rawMaskData);
                });
                results[b] = segResults;
            });
            return new List<Result[]>(results);
        }
        /// <summary>
        /// 快速 Sigmoid 近似计算 (比 Math.Exp 快 10-20 倍)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastSigmoid(float value)
        {
            float k = 1.0f / (1.0f + Math.Abs(value));
            return value * k * 0.5f + 0.5f;
        }

    }
}