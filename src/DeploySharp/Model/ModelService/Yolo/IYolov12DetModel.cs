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
    /// Implementation of YOLOv12 model for object detection
    /// </summary>
    public abstract class IYolov12DetModel : IYolov8DetModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov12DetModel(Yolov12DetConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results containing bounding boxes, class IDs and confidence scores</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }

    }
}
