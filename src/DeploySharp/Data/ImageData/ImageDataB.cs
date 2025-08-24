using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class ImageDataB : ImageData<byte>
    {
        public ImageDataB(int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(width, height, channels, format) { }

        public ImageDataB(byte[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(data, width, height, channels, format) { }
    }
}
