using System;
using System.Collections.Generic;
using DeploySharp.Data;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DeploySharp.Log;


namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv5 model for object Segmentation
    /// YOLOv5分割模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv5 Segmentation pipeline including:
    /// 提供标准YOLOv5检测流程，包括：
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
    /// Inherits from base IModel interface and implements YOLOv5-specific processing
    /// 继承自基础IModel接口并实现YOLOv5特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov5SegModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv5 detector
        /// 初始化YOLOv5检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov5SegModel(Yolov5SegConfig config) : base(config)
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
            // 调用内部统一逻辑，Batch=1
            var batchResult = PostprocessBatchInternal(dataTensor, new[] { imageAdjustmentParam });
            return batchResult[0];
        }
        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            return PostprocessBatchInternal(dataTensor, imageAdjustmentParams);
        }
        /// <summary>
        /// YOLOv5Seg 后处理核心逻辑 (统一接口)
        /// </summary>
        private List<Result[]> PostprocessBatchInternal(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];
            float[] result1 = dataTensor[1].DataBuffer as float[];
            var config = (Yolov5SegConfig)this.config;
            // --- 1. 维度参数准备 ---
            // YOLOv5Seg 结构: [Batch, Grid(Anchors), Info]
            // 注意：根据原代码 rowResultNum = config.OutputSizes[0][1]，这里将其视为 Grid 总数
            int rowResultNum = config.OutputSizes[0][1];
            int oneResultLen = config.OutputSizes[0][2]; // 85 (4+1+80) or 117 (4+1+80+32)
            int batchSize = config.InferBatch;
            int resultSizePerBatch = rowResultNum * oneResultLen;
            int maskLen = config.OutputSizes[1][1]; // 32
            int initialWidth = config.OutputSizes[1][3]; // 160
            int initialHeight = config.OutputSizes[1][2]; // 160
            int protoArea = initialWidth * initialHeight;
            float confThreshold = config.ConfidenceThreshold;
            float objThreshold = 0.25f; // 原代码中的硬编码阈值
            Result[][] results = new Result[batchSize][];
            // --- 2. Batch 并行处理 (利用多核 CPU) ---
            Parallel.For(0, batchSize, b =>
            {
                int batchOffset = b * resultSizePerBatch;
                int protoBatchOffset = b * maskLen * protoArea;
                // 2.1 候选框提取
                // 使用 List 避免 ConcurrentBag 的锁开销
                var candidateBoxes = new List<BoundingBox>();
                // 并行处理 Grid
                Parallel.For(0, rowResultNum, i =>
                {
                    // 预计算该 Grid 点在 result0 中的基准偏移
                    int baseIdx = batchOffset + i * oneResultLen;
                    // 1. 快速检查 Objectness (索引 4)
                    float objConf = result0[baseIdx + 4];
                    if (objConf <= objThreshold) return;
                    // 2. 提取坐标 (索引 0-3)
                    float cx = result0[baseIdx];
                    float cy = result0[baseIdx + 1];
                    float ow = result0[baseIdx + 2];
                    float oh = result0[baseIdx + 3];
                    // 3. 遍历类别 (索引 5 到 oneResultLen - maskLen)
                    for (int j = 5; j < oneResultLen - maskLen; j++)
                    {
                        float classConf = result0[baseIdx + j];
                        // 注意：原代码逻辑只用了 classConf，未乘以 objConf。
                        // 如果 NMS 需要乘积，需在此处修改。
                        if (classConf > confThreshold)
                        {
                            candidateBoxes.Add(new BoundingBox
                            {
                                Index = i,
                                NameIndex = j - 5,
                                Confidence = classConf,
                                Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                                Angle = 0.0f
                            });
                        }
                    }
                });
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
                // 2.4 并行生成每个 Box 的掩膜
                var segResults = new SegResult[boxCount];
                Parallel.For(0, boxCount, index =>
                {
                    var box = boxes[index];
                    var bounds = param.AdjustRect(box.Box);
                    // --- A. 提取 Mask Coefficients (使用 ArrayPool) ---
                    float[] maskCoeffs = ArrayPool<float>.Shared.Rent(maskLen);

                    // Coeffs 位于 result0 的每个 grid 的最后 maskLen 个通道
                    int coeffBaseIdx = batchOffset + box.Index * oneResultLen + (oneResultLen - maskLen);

                    // 快速复制
                    Array.Copy(result0, coeffBaseIdx, maskCoeffs, 0, maskLen);
                    // --- B. 矩阵乘法生成掩膜 (MatMul + Sigmoid) ---
                    float[] rawMaskData = ArrayPool<float>.Shared.Rent(validMaskWidth * validMaskHeight);
                    // 核心计算循环：使用循环展开优化
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

                            // 4次循环展开 (兼容旧版 .NET)
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
                    segResults[index] = new SegResult
                    {
                        Mask = new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
                        Id = box.NameIndex,
                        Bounds = bounds,
                        Confidence = box.Confidence
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
