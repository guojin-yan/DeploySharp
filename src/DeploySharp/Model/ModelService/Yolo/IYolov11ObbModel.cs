using DeploySharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IYolov11ObbModel : IYolov8ObbModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov11ObbModel(Yolov8ObbConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results containing bounding boxes, class IDs and confidence scores</returns>
        public ObbResult[] Predict(object img)
        {
            return base.Predict(img) as ObbResult[];
        }

    }
}
