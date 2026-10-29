using JYPPX.DeploySharp.Geometry;

// Coordinates use continuous pixel edges [0,width] x [0,height]. Source is the
// EXIF-normalized source.png. Rotation is the counterclockwise correction applied.
internal sealed record PageCoordinateMap(int Width, int Height, int Rotation)
{
    public PointF ToSource(PointF point) => Rotation switch
    {
        90 => new PointF(Width - point.Y, point.X),
        180 => new PointF(Width - point.X, Height - point.Y),
        270 => new PointF(point.Y, Height - point.X),
        _ => point
    };
    public PointF[] Map(RectangleF b) => new[] { new PointF(b.X, b.Y), new PointF(b.Right, b.Y),
        new PointF(b.Right, b.Bottom), new PointF(b.X, b.Bottom) }.Select(ToSource).ToArray();
}
