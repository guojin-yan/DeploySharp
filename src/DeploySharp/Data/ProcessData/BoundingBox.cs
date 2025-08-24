using System;

namespace DeploySharp.Data
{
    public class BoundingBox : IComparable<BoundingBox>
    {
        public int Index { get; set; }
        public int NameIndex { get; set; }

        public float Confidence { get; set; }

        public RectF Box { get; set; }

        public float Angle { get; set; }

        public int CompareTo(BoundingBox other) => Confidence.CompareTo(other.Confidence);
    }
}
