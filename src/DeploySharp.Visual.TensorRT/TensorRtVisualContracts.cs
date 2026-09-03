using System;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.OpenCV;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    // Kept separate from native construction so contract failures can be tested on CPU-only CI.
    internal static class TensorRtVisualContracts
    {
        public static void ValidatePreprocessing(VisualModelProfile profile, OpenCvPreprocessOptions preprocessing)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (preprocessing == null) throw new ArgumentNullException(nameof(preprocessing));
            if (preprocessing.BatchSize != 1 || preprocessing.Layout != VisualTensorLayout.Nchw || preprocessing.OutputType != OpenCvOutputType.Float32)
                throw new NotSupportedException("CUDA visual preprocessing currently requires batch-one Float32 NCHW output.");
            if (preprocessing.ColorOrder != VisualColorOrder.Rgb && preprocessing.ColorOrder != VisualColorOrder.Bgr)
                throw new NotSupportedException("CUDA visual preprocessing currently supports RGB or BGR output.");
            if (preprocessing.Interpolation != OpenCvInterpolation.Linear)
                throw new NotSupportedException("CUDA visual preprocessing currently supports bilinear interpolation.");
            if (preprocessing.ResizeMode != OpenCvResizeMode.Resize && preprocessing.ResizeMode != OpenCvResizeMode.Letterbox && preprocessing.ResizeMode != OpenCvResizeMode.LongestSidePadBottomRight)
                throw new NotSupportedException("CUDA visual preprocessing currently supports Resize or longest-side padding.");
            if (profile.Input.ElementType != TensorElementType.Float32 || profile.Input.Layout != VisualTensorLayout.Nchw || profile.Input.MinimumBatch != 1 || profile.Input.MaximumBatch != 1)
                throw new NotSupportedException("The visual profile must expose one Float32 NCHW image.");
            TensorShape shape = profile.Input.ShapePattern;
            if (shape.IsDynamic || shape.Rank != 4 || shape[0] != 1 || shape[1] != 3 || shape[2] != preprocessing.ModelSize.Height || shape[3] != preprocessing.ModelSize.Width)
                throw new NotSupportedException("The visual profile input shape must exactly match [1,3,H,W].");
        }

        public static void ValidateMetadata(ModelMetadata metadata, VisualModelProfile profile)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (metadata.Inputs.Count != 1) throw new NotSupportedException("GPU visual preprocessing requires exactly one engine input.");
            TensorDescriptor input = metadata.Inputs[0];
            if (!string.Equals(input.Name, profile.Input.Name, StringComparison.Ordinal) || input.ElementType != profile.Input.ElementType || input.Shape.IsDynamic || !ShapesEqual(input.Shape, profile.Input.ShapePattern))
                throw new InvalidOperationException("TensorRT input metadata does not match the visual profile.");
            if (metadata.Outputs.Count != profile.Outputs.Count) throw new InvalidOperationException("TensorRT output count does not match the visual profile.");
            foreach (VisualOutputBinding expected in profile.Outputs)
            {
                TensorDescriptor? actual = metadata.Outputs.FirstOrDefault(value => string.Equals(value.Name, expected.Name, StringComparison.Ordinal));
                if (actual == null || actual.ElementType != expected.ElementType || actual.Shape.IsDynamic || !ShapePatternMatches(expected.ShapePattern, actual.Shape))
                    throw new InvalidOperationException("TensorRT output metadata does not match visual output '" + expected.Name + "'.");
            }
        }

        private static bool ShapesEqual(TensorShape first, TensorShape second) => first.ToArray().SequenceEqual(second.ToArray());

        private static bool ShapePatternMatches(TensorShape pattern, TensorShape actual)
        {
            if (pattern.Rank != actual.Rank) return false;
            for (int index = 0; index < pattern.Rank; index++) if (pattern[index] > 0 && pattern[index] != actual[index]) return false;
            return true;
        }
    }
}
