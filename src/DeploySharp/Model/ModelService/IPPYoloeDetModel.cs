using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IPPYoloeDetModel : IModel
    {
        public IPPYoloeDetModel(IConfig config) : base(config)
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
            var config = (PPYoloeDetConfig)this.config;
            float[] resultScores = dataTensor[1].DataBuffer as float[];
            float[] resultBoxes = dataTensor[0].DataBuffer as float[];
            int resultCount = dataTensor[1].Shape[2];
            int categoryCount = dataTensor[1].Shape[1];
            // 获取有效检测（分数高于阈值）
            var candidateBoxes = new ConcurrentBag<BoundingBox>();
            // 4. 并行处理候选框检测
            Parallel.For(0, resultCount, i =>
            {
                for (int j = 0; j < categoryCount; j++)  // Iterate through each class
                {
                    float conf = resultScores[resultCount * j + i];
                    int label = j;
                    if (conf > config.ConfidenceThreshold)  // Confidence threshold filtering
                    {
                        float cx = resultBoxes[0 + i * 4];
                        float cy = resultBoxes[1 + i * 4];
                        float dx = resultBoxes[2 + i * 4];
                        float dy = resultBoxes[3 + i * 4];

                        // Convert to width/height format
                        // 转换为宽/高格式
                        int width = (int)((dx - cx));
                        int height = (int)((dy - cy));
                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = label,
                            Confidence = conf,
                            Box = new RectF(cx, cy, width, height),
                            Angle = 0.0f
                        });
                    }
                }
            });

            // 5. NMS处理
            var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

            var detResult = new DetResult[boxes.Length];

            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                int classID = box.NameIndex;
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                detResult[i] = new DetResult
                {
                    Id = classID,
                    Bounds = new Rect((int)box.Box.X, (int)box.Box.Y, (int)box.Box.Width, (int)box.Box.Height),
                    Confidence = box.Confidence,
                    Category = categoryFlag ? category : classID.ToString(),
                };
            }

            return detResult;
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            var config = (PPYoloeDetConfig)this.config;
            float[] resultScores = dataTensor[1].DataBuffer as float[];
            float[] resultBoxes = dataTensor[0].DataBuffer as float[];
            int resultCount = dataTensor[1].Shape[2];
            int categoryCount = dataTensor[1].Shape[1];
            int batchSize = config.InferBatch;

            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                // 获取有效检测（分数高于阈值）
                var candidateBoxes = new ConcurrentBag<BoundingBox>();
                // 4. 并行处理候选框检测
                Parallel.For(0, resultCount, i =>
                {
                    for (int j = 0; j < categoryCount; j++)  // Iterate through each class
                    {
                        float conf = resultScores[resultCount * j + i + b * categoryCount];
                        int label = j;
                        if (conf > config.ConfidenceThreshold)  // Confidence threshold filtering
                        {
                            float cx = resultBoxes[0 + i * 4 + b * resultCount * 4];
                            float cy = resultBoxes[1 + i * 4 + b * resultCount * 4];
                            float dx = resultBoxes[2 + i * 4 + b * resultCount * 4];
                            float dy = resultBoxes[3 + i * 4 + b * resultCount * 4];

                            // Convert to width/height format
                            // 转换为宽/高格式
                            int width = (int)((dx - cx));
                            int height = (int)((dy - cy));
                            candidateBoxes.Add(new BoundingBox
                            {
                                Index = i,
                                NameIndex = label,
                                Confidence = conf,
                                Box = new RectF(cx, cy, width, height),
                                Angle = 0.0f
                            });
                        }
                    }
                });

                // 5. NMS处理
                var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

                var detResult = new DetResult[boxes.Length];

                for (var i = 0; i < boxes.Length; i++)
                {
                    var box = boxes[i];
                    int classID = box.NameIndex;
                    bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                    detResult[i] = new DetResult
                    {
                        Id = classID,
                        Bounds = new Rect((int)box.Box.X, (int)box.Box.Y, (int)box.Box.Width, (int)box.Box.Height),
                        Confidence = box.Confidence,
                        Category = categoryFlag ? category : classID.ToString(),
                    };
                }

                results[b] = detResult;

            }
            return new List<Result[]>(results);
        }

    }
}
