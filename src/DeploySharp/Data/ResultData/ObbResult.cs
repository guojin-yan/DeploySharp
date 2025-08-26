using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class ObbResult : Result
    {
       public RotatedRect Bounds { get; set; }

        public override string ToString()
        {
            return base.ToString() + ": " + Bounds.ToString();
        }
    }
}
