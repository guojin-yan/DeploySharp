using DeploySharp.Data;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    public abstract class IYolov11SegModel : IYolov8SegModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public IYolov11SegModel(Yolov11SegConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public SegResult[] Predict(object img)
        {
            return base.Predict(img) as SegResult[];
        }
    }
}