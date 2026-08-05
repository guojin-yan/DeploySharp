using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    internal sealed class VisualDetectionCandidate
    {
        public VisualDetectionCandidate(int sourceIndex, int classIndex, float score, RectangleF modelBox, RectangleF sourceBox)
        {
            SourceIndex = sourceIndex;
            ClassIndex = classIndex;
            Score = score;
            ModelBox = modelBox;
            SourceBox = sourceBox;
        }

        public int SourceIndex { get; }
        public int ClassIndex { get; }
        public float Score { get; }
        public RectangleF ModelBox { get; }
        public RectangleF SourceBox { get; }
    }

    internal static class DetectionPostprocessing
    {
        public static RectangleF DecodeModelBox(DetectionBoxFormat format, bool normalized, VisualSize modelSize, float first, float second, float third, float fourth)
        {
            if (normalized)
            {
                first *= modelSize.Width;
                third *= modelSize.Width;
                second *= modelSize.Height;
                fourth *= modelSize.Height;
            }

            float left;
            float top;
            float right;
            float bottom;
            if (format == DetectionBoxFormat.Xyxy)
            {
                left = first; top = second; right = third; bottom = fourth;
            }
            else if (format == DetectionBoxFormat.Xywh)
            {
                left = first; top = second; right = first + third; bottom = second + fourth;
            }
            else
            {
                left = first - (third / 2f); top = second - (fourth / 2f); right = first + (third / 2f); bottom = second + (fourth / 2f);
            }

            return new RectangleF(left, top, right - left, bottom - top);
        }

        public static List<VisualDetectionCandidate> Suppress(
            IReadOnlyList<VisualDetectionCandidate> ordered,
            float iouThreshold,
            DetectionNmsMode mode,
            int maximumResults,
            CancellationToken cancellationToken)
        {
            var kept = new List<VisualDetectionCandidate>(Math.Min(ordered.Count, maximumResults));
            for (int candidateIndex = 0; candidateIndex < ordered.Count && kept.Count < maximumResults; candidateIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VisualDetectionCandidate candidate = ordered[candidateIndex];
                bool suppressed = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    if ((keptIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    VisualDetectionCandidate existing = kept[keptIndex];
                    if (mode == DetectionNmsMode.ClassAware && existing.ClassIndex != candidate.ClassIndex) continue;
                    if (DetectionDecoder.IntersectionOverUnion(existing.SourceBox, candidate.SourceBox) > iouThreshold)
                    {
                        suppressed = true;
                        break;
                    }
                }

                if (!suppressed) kept.Add(candidate);
            }

            return kept;
        }
    }
}
