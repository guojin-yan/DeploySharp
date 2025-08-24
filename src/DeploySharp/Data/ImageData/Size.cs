using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct Size(int Width, int Height)
    {

        public int Width = Width;
        public int Height = Height;

        public Size(double width, double height)
            : this((int)width, (int)height)
        {
        }
        public static explicit operator Size(SizeD size)
            => new(size.Width, size.Height);

        public static explicit operator Size(SizeF size)
            => new(size.Width, size.Height);

        public override string ToString()
        {
            return $"(Width: {Width}, Height: {Height})";
        }

    }
}
