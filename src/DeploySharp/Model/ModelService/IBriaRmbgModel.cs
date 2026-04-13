using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Model
{
    public abstract class IBriaRmbgModel : IModel
    {
        public IBriaRmbgModel(IConfig config) : base(config)
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
            var config = (BriaRmbgConfig)this.config;

            // 1. 获取原始数据
            var rawBuffer = dataTensor[0].DataBuffer as float[];
            var result = ResizeBilinear(rawBuffer, dataTensor[0].Shape[2], dataTensor[0].Shape[3], 
                imageAdjustmentParam.RowImgSize.Width, imageAdjustmentParam.RowImgSize.Height);
            ImageDataF image = new ImageDataF(
                result,
                imageAdjustmentParam.RowImgSize.Width, imageAdjustmentParam.RowImgSize.Height,
                1);

            ImageDataB imageDataB = new ImageDataB(
                Binarize(result, config.ConfidenceThreshold),
                imageAdjustmentParam.RowImgSize.Width, imageAdjustmentParam.RowImgSize.Height,
                1);

            SegResult segResult = new SegResult
            {
                Id = 0,
                Bounds = new Rect { X = 0, Y = 0, Width = image.Width, Height = image.Height },
                Confidence = 1.0f,
                Category = "Foreground",
                Mask = image,
                ByteMask = imageDataB
            };
            return new SegResult[] { segResult };
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            var config = (DEIMv2DetConfig)this.config;
            float[] resultScores = dataTensor[2].DataBuffer as float[];
            long[] resultLables = dataTensor[0].DataBuffer as long[];
            float[] resultBoxes = dataTensor[1].DataBuffer as float[];
            int batchSize = config.InferBatch;

            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                // 获取有效检测（分数高于阈值）
                List<DetResult> detResults = new List<DetResult>();
                for (int i = 0; i < resultScores.Length / batchSize; i++)
                {
                    if (resultScores[i + 300 * b] < config.ConfidenceThreshold)
                    {
                        continue;
                    }
                    int s = 4 * i;
                    // Parse bounding box coordinates (x1,y1,x2,y2 format)
                    // 解析边界框坐标(x1,y1,x2,y2格式)
                    float cx = resultBoxes[s + 0 + 4 * 300 * b];
                    float cy = resultBoxes[s + 1 + 4 * 300 * b];
                    float dx = resultBoxes[s + 2 + 4 * 300 * b];
                    float dy = resultBoxes[s + 3 + 4 * 300 * b];

                    // Convert to width/height format
                    // 转换为宽/高格式
                    int width = (int)((dx - cx));
                    int height = (int)((dy - cy));

                    Rect box = new Rect
                    {
                        X = (int)cx,
                        Y = (int)cy,
                        Width = width,
                        Height = height
                    };
                    int classID = (int)resultLables[i + 300 * b];
                    bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                    // Create detection result with adjusted coordinates
                    // 创建带有调整坐标的检测结果
                    detResults.Add(new DetResult
                    {
                        Id = classID,                               // Class ID/类别ID
                        Bounds = (box), // Adjusted rectangle/调整后的矩形
                        Confidence = resultScores[i + 300 * b],                // Detection confidence/检测置信度
                        Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                    });
                }

                results[b] = detResults.ToArray();

            }
            return new List<Result[]>(results);
        }

        private static float[] ResizeBilinear(float[] src, int srcW, int srcH, int dstW, int dstH)
        {
            float[] dst = new float[dstW * dstH];
            float xRatio = (float)srcW / dstW;
            float yRatio = (float)srcH / dstH;
            unsafe
            {
                fixed (float* pSrc = src, pDst = dst)
                {
                    for (int y = 0; y < dstH; y++)
                    {
                        for (int x = 0; x < dstW; x++)
                        {
                            // 计算源图坐标
                            float srcX = x * xRatio;
                            float srcY = y * yRatio;
                            int x0 = (int)srcX;
                            int y0 = (int)srcY;
                            int x1 = Math.Min(x0 + 1, srcW - 1);
                            int y1 = Math.Min(y0 + 1, srcH - 1);
                            // 计算插值权重
                            float xDiff = srcX - x0;
                            float yDiff = srcY - y0;
                            // 获取四个邻近点
                            float v00 = pSrc[y0 * srcW + x0];
                            float v10 = pSrc[y0 * srcW + x1];
                            float v01 = pSrc[y1 * srcW + x0];
                            float v11 = pSrc[y1 * srcW + x1];
                            // 双线性插值公式
                            float val = v00 * (1 - xDiff) * (1 - yDiff) +
                                        v10 * xDiff * (1 - yDiff) +
                                        v01 * (1 - xDiff) * yDiff +
                                        v11 * xDiff * yDiff;
                            pDst[y * dstW + x] = val;
                        }
                    }
                }
            }
            return dst;
        }

        private static byte[] Binarize(float[] data, float threshold)
        {
            byte[] result = new byte[data.Length];
            unsafe
            {
                fixed (float* pSrc = data)
                fixed (byte* pDst = result)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        // 大于阈值为 255 (白/前景)，否则为 0 (黑/背景)
                        *(pDst + i) = (*(pSrc + i) > threshold) ? (byte)255 : (byte)0;
                    }
                }
            }
            return result;
        }

    }
}
