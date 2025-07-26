using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{
    public class InferEngineFactory
    {
        public static IModelInferEngine Create(InferenceBackend backend)
        {
            return backend switch
            {
                InferenceBackend.OpenVINO => new OpenVinoInferEngine(),
                InferenceBackend.OnnxRuntime => new OnnxRuntimeInferEngine(),
                _ => throw new NotSupportedException()
            };
        }
    }
}
