using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class ImageDataF : ImageData<float>
    {
        public ImageDataF(int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(width, height, channels, format) { }

        public ImageDataF(float[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(data, width, height, channels, format) { }
    }
}
