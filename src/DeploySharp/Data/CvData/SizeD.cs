using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct SizeD(double Width, double Height)
    {

        public double Width = Width;

        public double Height = Height;

        public static implicit operator SizeD(Size size)
            => new(size.Width, size.Height);

        public static implicit operator SizeD(SizeF size)
            => new(size.Width, size.Height);

        public readonly Size ToSize() => new(Width, Height);


        public readonly SizeF ToSizeF() => new(Width, Height);
    }
}
