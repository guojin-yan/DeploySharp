using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Decodes Paddle multiclass NMS output [class,score,x1,y1,x2,y2]. / 解码 Paddle 多类别 NMS 输出 [类别,分数,x1,y1,x2,y2]。</summary>
    public sealed class PaddleDocumentNmsDecoder : IVisualDecoder
    {
        public PaddleDocumentNmsDecoder(PaddleDocumentModelDescriptor descriptor, IEnumerable<string> labels, string outputName = "fetch_name_0", string? countOutputName = "fetch_name_1", bool normalizedCoordinates = false, float scoreThreshold = 0)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.LayoutDetection && descriptor.Module != PaddleDocumentModule.TableCellDetection && descriptor.Module != PaddleDocumentModule.SealTextDetection) throw new ArgumentException("The descriptor must identify a region-detection module.", nameof(descriptor));
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            var values = new List<string>(labels);
            if (values.Count == 0) throw new ArgumentException("At least one label is required.", nameof(labels));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0 || scoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            Labels = values.AsReadOnly(); OutputName = outputName; CountOutputName = string.IsNullOrWhiteSpace(countOutputName) ? null : countOutputName; NormalizedCoordinates = normalizedCoordinates; ScoreThreshold = scoreThreshold;
        }
        public PaddleDocumentModelDescriptor Descriptor { get; }
        public IReadOnlyList<string> Labels { get; }
        public string OutputName { get; }
        public string? CountOutputName { get; }
        public bool NormalizedCoordinates { get; }
        public float ScoreThreshold { get; }
        public VisualTaskId Task => Descriptor.Module == PaddleDocumentModule.LayoutDetection ? VisualTaskId.LayoutDetection : Descriptor.Module == PaddleDocumentModule.TableCellDetection ? VisualTaskId.TableCellDetection : VisualTaskId.SealTextDetection;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ITensor output = Required(context, OutputName);
            if (output.ElementType != TensorElementType.Float32 || !(output.Buffer is float[])) throw Failure(context, "Paddle NMS output must be a managed Float32 tensor.", OutputName);
            int batch;
            int rows;
            bool flattened = output.Shape.Rank == 2;
            if (flattened) { batch = context.Input.BatchSize; rows = checked((int)output.Shape[0]); if (batch != 1 && CountOutputName == null) throw Failure(context, "A flattened Paddle NMS output requires bbox_num for batched input.", OutputName); }
            else if (output.Shape.Rank == 3) { batch = checked((int)output.Shape[0]); rows = checked((int)output.Shape[1]); }
            else throw Failure(context, "Paddle NMS output must be [rows,6] or [batch,rows,6].", OutputName, output.Shape.ToString());
            if (output.Shape[output.Shape.Rank - 1] != 6 || batch != context.Input.BatchSize) throw Failure(context, "Paddle NMS output shape does not match the prepared batch.", OutputName, output.Shape.ToString());
            int[] counts = ResolveCounts(context, batch, rows);
            if (flattened && batch > 1)
            {
                long totalCount = 0;
                for (int index = 0; index < counts.Length; index++) totalCount += counts[index];
                if (totalCount != rows) throw Failure(context, "Flattened Paddle NMS bbox_num must sum to the output row count.", CountOutputName!, output.Shape.ToString());
            }
            float[] values = (float[])output.Buffer;
            var decoded = new List<DetectionResult>(batch);
            int rowOffset = 0;
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int count = counts[row];
                var detections = new List<Detection>(count);
                for (int index = 0; index < count; index++)
                {
                    int offset = (rowOffset + index) * 6;
                    float classValue = values[offset];
                    if (float.IsNaN(classValue) || float.IsInfinity(classValue)) throw Failure(context, "Paddle NMS class index is not finite.", OutputName, "index=" + index);
                    if (classValue < 0 || classValue >= Labels.Count) continue;
                    if (classValue != Math.Truncate(classValue)) throw Failure(context, "Paddle NMS class index must be integral.", OutputName, "index=" + index);
                    int classIndex = (int)classValue;
                    float score = values[offset + 1];
                    if (float.IsNaN(score) || float.IsInfinity(score)) throw Failure(context, "Paddle NMS score is not finite.", OutputName, "index=" + index);
                    if (score < ScoreThreshold) continue;
                    float x1 = values[offset + 2], y1 = values[offset + 3], x2 = values[offset + 4], y2 = values[offset + 5];
                    if (float.IsNaN(x1) || float.IsInfinity(x1) || float.IsNaN(y1) || float.IsInfinity(y1) || float.IsNaN(x2) || float.IsInfinity(x2) || float.IsNaN(y2) || float.IsInfinity(y2))
                        throw Failure(context, "Paddle NMS box coordinates are not finite.", OutputName, "index=" + index);
                    if (NormalizedCoordinates) { x1 *= context.Input.BatchFrames[row].ModelSize.Width; x2 *= context.Input.BatchFrames[row].ModelSize.Width; y1 *= context.Input.BatchFrames[row].ModelSize.Height; y2 *= context.Input.BatchFrames[row].ModelSize.Height; }
                    RectangleF box = context.Input.BatchFrames[row].Transform.ToSource(new RectangleF(x1, y1, x2 - x1, y2 - y1));
                    box = context.Input.BatchFrames[row].Transform.ClipToSource(box);
                    detections.Add(new Detection(box, new LabelScore(classIndex, Labels[classIndex], score)));
                }
                decoded.Add(new DetectionResult(detections));
                rowOffset += flattened ? count : rows;
            }
            return batch == 1 ? (object)decoded[0] : new DetectionBatchResult(decoded);
        }

        private int[] ResolveCounts(VisualDecodeContext context, int batch, int rows)
        {
            var counts = new int[batch];
            if (CountOutputName == null)
            {
                for (int index = 0; index < batch; index++) counts[index] = rows;
                return counts;
            }
            ITensor countTensor = Required(context, CountOutputName);
            if ((countTensor.ElementType != TensorElementType.Int32 && countTensor.ElementType != TensorElementType.Int64) || countTensor.Length != batch) throw Failure(context, "Paddle bbox_num must be an integer tensor with one value per batch row.", CountOutputName);
            for (int index = 0; index < batch; index++)
            {
                long raw = countTensor.ElementType == TensorElementType.Int32 ? ((int[])countTensor.Buffer)[index] : ((long[])countTensor.Buffer)[index];
                if (raw < 0 || raw > rows) throw Failure(context, "Paddle bbox_num is outside the output row bounds.", CountOutputName, "index=" + index);
                counts[index] = (int)raw;
            }
            return counts;
        }
        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "A required Paddle NMS output is missing.", exception, context.Profile.ProfileId, name, modelId: context.Profile.ModelId); } }
        private static VisualException Failure(VisualDecodeContext context, string message, string tensorName, string? details = null) => new VisualException(VisualErrorCodes.DecodeFailed, message, profileId: context.Profile.ProfileId, tensorName: tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
    }
}

#pragma warning restore CS1591
