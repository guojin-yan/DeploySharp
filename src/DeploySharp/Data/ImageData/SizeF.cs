using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct SizeF(float Width, float Height)
    {

        public float Width = Width;

        public float Height = Height;


        public SizeF(double width, double height)
            : this((float)width, (float)height)
        {
        }

        public static implicit operator SizeF(Size size)
            => new(size.Width, size.Height);

        public static explicit operator SizeF(SizeD size)
            => new(size.Width, size.Height);



        public readonly Size ToSize() => new(Width, Height);


        public readonly SizeD ToSizeD() => new(Width, Height);
    }
}
