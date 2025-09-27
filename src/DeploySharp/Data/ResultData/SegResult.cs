using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class SegResult : DetResult
    {
        public ImageDataF Mask { get; set; }

        public SegResult()
        {
            Type = ResultType.Segmentation;
        }
    }
}
