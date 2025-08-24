using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    public struct RunSpeed
    {
        public TimeSpan Preprocess { get; }
        public TimeSpan Inference { get; }
        public TimeSpan Postprocess { get; }

        public RunSpeed(TimeSpan preprocess, TimeSpan inference, TimeSpan postprocess)
        {
            Preprocess = preprocess;
            Inference = inference;
            Postprocess = postprocess;
        }

        public override string ToString()
        {
            return $"Input Data Preprocess: {Preprocess.TotalSeconds} ms," +
                   $"\tModel Inference: {Inference.TotalSeconds} ms," +
                   $"\tResult Postprocess: {Postprocess.TotalSeconds} ms";
        }
    }

}
