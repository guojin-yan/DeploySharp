using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{

    public class AnomalySegResult : Result
    {
        public ImageDataF RawMask { get; set; }
        public ImageDataF Mask { get; set; }
    }
}
