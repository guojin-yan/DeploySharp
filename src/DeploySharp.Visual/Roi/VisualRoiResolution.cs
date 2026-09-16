using System;

namespace JYPPX.DeploySharp.Visual
{
    internal static class VisualRoiResolution
    {
        internal static IVisualRoiGeometry Resolve(VisualRoiSnapshot snapshot, VisualRoi roi, VisualRoiCoordinateContext? coordinateContext)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (coordinateContext != null && coordinateContext.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(coordinateContext));
            if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World)
            {
                if (coordinateContext == null) throw new NotSupportedException("TileLocal and World ROI filtering requires an explicit projection context.");
                return snapshot.Resolve(roi, coordinateContext);
            }
            if (roi.CoordinateSpace == RoiCoordinateSpace.ModelInput) throw new NotSupportedException("ModelInput ROI filtering requires a concrete VisualInputFrame.");
            return snapshot.Resolve(roi);
        }
    }
}
