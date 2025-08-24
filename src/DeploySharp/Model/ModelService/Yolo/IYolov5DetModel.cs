using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Collections.Concurrent;
using System.Numerics;
using System.Diagnostics;
using System.Configuration;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv5 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public abstract class IYolov5DetModel : IModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov5DetModel(Yolov5DetConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }


        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor</param>
        /// <returns>Processed detection results</returns>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];
            //float[] result1 = dataTensor[1].GetMemory<float>().ToArray();

            var config = (Yolov5DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][1];
            int oneResultLen = config.OutputSizes[0][2];
            //int maskLen = config.OutputSizes[1][1];


            var candidateBoxes = new ConcurrentBag<BoundingBox>();
            //int initialWidth = config.OutputSizes[1][3];
            //int initialHeight = config.OutputSizes[1][2];

            // 4. 并行处理候选框检测
            Parallel.For(0, rowResultNum, i =>
            {
                float conf = result0[oneResultLen * i + 4];
                if (conf <= 0.25f) return;

                for (int j = 5; j < oneResultLen; j++)
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

            var detResult = new DetResult[boxes.Length];

            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                detResult[i] = new DetResult
                {
                    Id = box.NameIndex,
                    Bounds = imageAdjustmentParam.AdjustRect(box.Box),
                    Confidence = box.Confidence
                };
            }

            return detResult;
        }

    }

}
