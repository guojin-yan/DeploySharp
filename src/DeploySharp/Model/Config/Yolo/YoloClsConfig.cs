using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Model
{
    public class YoloClsConfig : IImgConfig
    {        /// <summary>
             /// Initializes a new instance with default values
             /// 使用默认值初始化新实例
             /// </summary>
             /// <remarks>
             /// The model path must be set separately before use.
             /// 使用前需要单独设置模型路径。
             /// </remarks>
        public YoloClsConfig() { }

        public YoloClsConfig(string modelPath)
        {
            this.ModelType = ModelType.YOLOCls;
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;
            this.ConfidenceThreshold = 0.5f;
            this.InferBatch = 1;
            this.DataProcessor.ResizeMode = ImageResizeMode.Stretch;
            this.DataProcessor.NormalizationType = ImageNormalizationType.Scale_0_1;

        }


        public YoloClsConfig(
            string modelPath,
            InferenceBackend inferenceBackend = InferenceBackend.OpenVINO,
            DeviceType deviceType = DeviceType.CPU,
            float confidenceThreshold = 0.5f,
            int inferBatch = 1,
            ImageResizeMode resizeMode = ImageResizeMode.Stretch,
            ImageNormalizationType normalizationType = ImageNormalizationType.Scale_0_1)
        {
            this.ModelType = ModelType.YOLOv5Det;
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = inferenceBackend;
            this.TargetDeviceType = deviceType;
            this.ConfidenceThreshold = confidenceThreshold;
            this.InferBatch = inferBatch;
            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
        }
    }
}
