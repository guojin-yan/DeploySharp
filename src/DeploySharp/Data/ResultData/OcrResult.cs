using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Data
{
    public class OcrResult
    {
        public ObbResult[] TextAreas { get; set; }
        public Result[] TextOrientations { get; set; }
        public TextRecResult[] TextContents { get; set; }
    }
}
