using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IYolov6DetModel : IYolov5DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov6DetModel(Yolov6DetConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }
    }
}
