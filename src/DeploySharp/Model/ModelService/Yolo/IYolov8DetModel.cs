using DeploySharp.Data;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv8 model for object detection
    /// </summary>
    public abstract class IYolov8DetModel : IModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov8DetModel(Yolov8DetConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results containing bounding boxes, class IDs and confidence scores</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// </summary>
        /// <param name="dataTensor">Raw output tensor from model</param>
        /// <returns>Processed detection results</returns>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (Yolov5DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][2];
            int oneResultLen = config.OutputSizes[0][1];

            var candidateBoxes = new ConcurrentBag<BoundingBox>();

            // 4. 并行处理候选框检测
            Parallel.For(0, rowResultNum, i =>
            {
                for (int j = 4; j < oneResultLen; j++)  // Iterate through each class
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
