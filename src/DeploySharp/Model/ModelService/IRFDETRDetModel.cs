using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IRFDETRDetModel : IModel
    {
        public IRFDETRDetModel(IConfig config) : base(config)
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
            var config = (RFDETRDetConfig)this.config;
            float[] resultScores = dataTensor[1].DataBuffer as float[];
            float[] resultBoxes = dataTensor[0].DataBuffer as float[];
            int resultCount = dataTensor[1].Shape[1];
            int categoryCount = dataTensor[1].Shape[2];
            for (int i = 0; i < resultScores.Length; ++i) 
            {
                resultScores[i] = Sigmoid(resultScores[i]);
            }
            // 获取有效检测（分数高于阈值）
            List<DetResult> detResults = new List<DetResult>();
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
                float pixelX1 = x1 * imageAdjustmentParam.RowImgSize.Width;
                float pixelY1 = y1 * imageAdjustmentParam.RowImgSize.Height;
                float pixelX2 = x2 * imageAdjustmentParam.RowImgSize.Width;
                float pixelY2 = y2 * imageAdjustmentParam.RowImgSize.Height;

                // 确保坐标在图像范围内
                pixelX1 = Math.Max(0, Math.Min(pixelX1, imageAdjustmentParam.RowImgSize.Width));
                pixelY1 = Math.Max(0, Math.Min(pixelY1, imageAdjustmentParam.RowImgSize.Height));
                pixelX2 = Math.Max(0, Math.Min(pixelX2, imageAdjustmentParam.RowImgSize.Width));
                pixelY2 = Math.Max(0, Math.Min(pixelY2, imageAdjustmentParam.RowImgSize.Height));

                // 检查边界框是否有效
                float width = pixelX2 - pixelX1;
                float height = pixelY2 - pixelY1;

                // 转换为矩形格式
                Rect box = new Rect((int)pixelX1, (int)pixelY1, (int)width, (int)height);

                int classID = index;
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                // Create detection result with adjusted coordinates
                // 创建带有调整坐标的检测结果
                detResults.Add(new DetResult
                {
                    Id = classID,                               // Class ID/类别ID
                    Bounds = box,//imageAdjustmentParam.AdjustRect(box), // Adjusted rectangle/调整后的矩形
                    Confidence = scores,                // Detection confidence/检测置信度
                    Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                });
            }
            return detResults.ToArray();
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            var config = (RFDETRDetConfig)this.config;
            float[] resultScores = dataTensor[1].DataBuffer as float[];
            float[] resultBoxes = dataTensor[0].DataBuffer as float[];
            int boxResultCount = dataTensor[0].Shape[1];
            int lableResultCount = dataTensor[1].Shape[1];
            int categoryCount = dataTensor[1].Shape[2];
            for (int i = 0; i < resultScores.Length; ++i)
            {
                resultScores[i] = Sigmoid(resultScores[i]);
            }
            int batchSize = config.InferBatch;

            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                // 获取有效检测（分数高于阈值）
                List<DetResult> detResults = new List<DetResult>();
                for (int i = 0; i < lableResultCount; i++)
                {
                    var (maxValue, index) = Util.FindMaxInRange(resultScores, categoryCount * i + b * lableResultCount * categoryCount, categoryCount * (i + 1) + b * lableResultCount * categoryCount);
                    float scores = (maxValue);
                    if (scores < config.ConfidenceThreshold)
                    {
                        continue;
                    }


                    // 解析检测框 [1, 300, 4] - 注意：坐标是cxcywh格式，归一化的
                    int detIndex = i * 4;
                    float cx = resultBoxes[detIndex + 4 * b * boxResultCount];
                    float cy = resultBoxes[detIndex + 1 + 4 * b * boxResultCount];
                    float w = resultBoxes[detIndex + 2 + 4 * b * boxResultCount];
                    float h = resultBoxes[detIndex + 3 + 4 * b * boxResultCount];

                    // 将cxcywh归一化坐标转换为xyxy归一化坐标
                    float x1 = cx - w / 2;
                    float y1 = cy - h / 2;
                    float x2 = cx + w / 2;
                    float y2 = cy + h / 2;


                    // 将归一化坐标转换为实际像素坐标
                    float pixelX1 = x1 * imageAdjustmentParams[b].RowImgSize.Width;
                    float pixelY1 = y1 * imageAdjustmentParams[b].RowImgSize.Height;
                    float pixelX2 = x2 * imageAdjustmentParams[b].RowImgSize.Width;
                    float pixelY2 = y2 * imageAdjustmentParams[b].RowImgSize.Height;

                    // 确保坐标在图像范围内
                    pixelX1 = Math.Max(0, Math.Min(pixelX1, imageAdjustmentParams[b].RowImgSize.Width));
                    pixelY1 = Math.Max(0, Math.Min(pixelY1, imageAdjustmentParams[b].RowImgSize.Height));
                    pixelX2 = Math.Max(0, Math.Min(pixelX2, imageAdjustmentParams[b].RowImgSize.Width));
                    pixelY2 = Math.Max(0, Math.Min(pixelY2, imageAdjustmentParams[b].RowImgSize.Height));

                    // 检查边界框是否有效
                    float width = pixelX2 - pixelX1;
                    float height = pixelY2 - pixelY1;

                    // 转换为矩形格式
                    Rect box = new Rect((int)pixelX1, (int)pixelY1, (int)width, (int)height);

                    int classID = index;
                    bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                    // Create detection result with adjusted coordinates
                    // 创建带有调整坐标的检测结果
                    detResults.Add(new DetResult
                    {
                        Id = classID,                               // Class ID/类别ID
                        Bounds = box,//imageAdjustmentParam.AdjustRect(box), // Adjusted rectangle/调整后的矩形
                        Confidence = scores,                // Detection confidence/检测置信度
                        Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                    });
                }

                results[b] = detResults.ToArray();

            }
            return new List<Result[]>(results);
        }


        // Sigmoid函数
        private float Sigmoid(float x)
        {
            return 1.0f / (1.0f + (float)Math.Exp(-x));
        }

    }
}
