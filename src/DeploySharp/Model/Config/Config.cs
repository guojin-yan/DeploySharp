using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public interface IConfig
    {
        public void SetModelPath(string path);
        public void SetTargetInferenceBackend(InferenceBackend inferenceBackend);
        public void SetTargetDeviceType(DeviceType targetDeviceType);
        public string ToString();
    }
}
