using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class PPOcrConfig
    {
        public PPOcrDetConfig DetConfig { get; set; }
        public PPOcrClsConfig ClsConfig { get; set; }
        public PPOcrRecConfig RecConfig { get; set; }
    }
}
