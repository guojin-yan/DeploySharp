using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class IImgConfig : IConfig
    {
        public DataProcessorConfig DataProcessor = new DataProcessorConfig();
    }
}
