using DeploySharp.Data;
using DeploySharp.Log;
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
    public abstract class IYolov10DetModel : IModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov10DetModel(Yolov10DetConfig config) : base(config) 
        { 
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

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

            var config = (Yolov10DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][0];
            int oneResultLen = config.OutputSizes[0][1];


            List<DetResult> detResults = new List<DetResult>();
            for (var i = 0; i < rowResultNum; i++)
            {
                int s = 7 * i;
                if (result0[s + 5] > config.ConfidenceThreshold)
                {
                    continue;
                }
                float cx = result0[s + 0];
                float cy = result0[s + 1];
                float dx = result0[s + 2];
                float dy = result0[s + 3];
                int width = (int)((dx - cx));
                int height = (int)((dy - cy));
                RectF box = new RectF();
                box.X = cx;
                box.Y = cy;
                box.Width = width;
                box.Height = height;

                int classID = (int)result0[s + 4];
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                detResults.Add(new DetResult
                {
                    Id = classID,
                    Bounds = imageAdjustmentParam.AdjustRect(box),
                    Confidence = result0[s + 5],
                    Category = categoryFlag ? category : classID.ToString(),
                });
            }

            return detResults.ToArray();
        }


    }
}
