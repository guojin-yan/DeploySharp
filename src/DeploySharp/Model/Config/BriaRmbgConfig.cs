using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Model
{
    public class BriaRmbgConfig : IImgConfig
    {

        public enum BriaRmbgVersion
        {
            V1_4,
            V2_0
        };

        public BriaRmbgConfig() { }


        public BriaRmbgConfig(BriaRmbgVersion briaRmbgVersion, string modelPath)
        {
            this.ModelType = ModelType.PPYOLOETDet;
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;
            this.ConfidenceThreshold = 0.5f;
            this.InferBatch = 1;
            this.DataProcessor.ResizeMode = ImageResizeMode.Stretch;
            if (briaRmbgVersion == BriaRmbgVersion.V1_4) 
            {
                this.DataProcessor.NormalizationType = ImageNormalizationType.Scale_Neg05_05;
            }
            else
            {
                this.DataProcessor.NormalizationType = ImageNormalizationType.ImageNetStandard;
            }
        }
    }

}
