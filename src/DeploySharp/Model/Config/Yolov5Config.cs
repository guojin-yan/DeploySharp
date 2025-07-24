using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class Yolov5Config : Yolov8Config
    {
        public Yolov5Config() { }

        public Yolov5Config(string modelPath,
            InferenceBackend inferenceBackend = InferenceBackend.OpenVINO,
            DeviceType deviceType = DeviceType.CPU,
            float confidenceThreshold = 0.5f,
            float nmsThreshold = 0.5f,
             int inferBatch = 1) : base(modelPath, inferenceBackend, deviceType, confidenceThreshold, nmsThreshold, inferBatch)
        { 
        }
    }
}
