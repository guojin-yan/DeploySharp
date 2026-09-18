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
            Labels = values.AsReadOnly(); OutputName = outputName; CountOutputName = countOutputName; NormalizedCoordinates = normalizedCoordinates; ScoreThreshold = scoreThreshold;
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
            if (output.Shape.Rank == 2) { batch = context.Input.BatchSize; int totalRows = checked((int)output.Shape[0]); if (totalRows % batch != 0) throw Failure(context, "A flattened Paddle NMS output row count must be divisible by the input batch.", OutputName); rows = totalRows / batch; if (batch != 1 && CountOutputName == null) throw Failure(context, "A flattened Paddle NMS output requires bbox_num for batched input.", OutputName); }
            else if (output.Shape.Rank == 3) { batch = checked((int)output.Shape[0]); rows = checked((int)output.Shape[1]); }
            else throw Failure(context, "Paddle NMS output must be [rows,6] or [batch,rows,6].", OutputName, output.Shape.ToString());
            if (output.Shape[output.Shape.Rank - 1] != 6 || batch != context.Input.BatchSize) throw Failure(context, "Paddle NMS output shape does not match the prepared batch.", OutputName, output.Shape.ToString());
            int[] counts = ResolveCounts(context, batch, rows);
            float[] values = (float[])output.Buffer;
            var decoded = new List<DetectionResult>(batch);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(rows, counts[row]);
                var detections = new List<Detection>(count);
                for (int index = 0; index < count; index++)
                {
                    int offset = (row * rows + index) * 6;
                    int classIndex = checked((int)values[offset]);
                    float score = values[offset + 1];
                    if (classIndex < 0 || classIndex >= Labels.Count || score < ScoreThreshold) continue;
                    if (float.IsNaN(score) || float.IsInfinity(score)) throw Failure(context, "Paddle NMS score is not finite.", OutputName, "index=" + index);
                    float x1 = values[offset + 2], y1 = values[offset + 3], x2 = values[offset + 4], y2 = values[offset + 5];
                    if (NormalizedCoordinates) { x1 *= context.Input.BatchFrames[row].ModelSize.Width; x2 *= context.Input.BatchFrames[row].ModelSize.Width; y1 *= context.Input.BatchFrames[row].ModelSize.Height; y2 *= context.Input.BatchFrames[row].ModelSize.Height; }
                    RectangleF box = context.Input.BatchFrames[row].Transform.ToSource(new RectangleF(x1, y1, x2 - x1, y2 - y1));
                    box = context.Input.BatchFrames[row].Transform.ClipToSource(box);
                    detections.Add(new Detection(box, new LabelScore(classIndex, Labels[classIndex], score)));
                }
                decoded.Add(new DetectionResult(detections));
            }
            return batch == 1 ? (object)decoded[0] : new DetectionBatchResult(decoded);
        }

        private int[] ResolveCounts(VisualDecodeContext context, int batch, int rows)
        {
            var counts = new int[batch];
            if (CountOutputName == null) { counts[0] = rows; return counts; }
            ITensor countTensor = Required(context, CountOutputName);
            if ((countTensor.ElementType != TensorElementType.Int32 && countTensor.ElementType != TensorElementType.Int64) || countTensor.Length < batch) throw Failure(context, "Paddle bbox_num must be an integer tensor with one value per batch row.", CountOutputName);
            for (int index = 0; index < batch; index++) counts[index] = Math.Max(0, Math.Min(rows, countTensor.ElementType == TensorElementType.Int32 ? ((int[])countTensor.Buffer)[index] : checked((int)((long[])countTensor.Buffer)[index])));
            return counts;
        }
        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "A required Paddle NMS output is missing.", exception, context.Profile.ProfileId, name, modelId: context.Profile.ModelId); } }
        private static VisualException Failure(VisualDecodeContext context, string message, string tensorName, string? details = null) => new VisualException(VisualErrorCodes.DecodeFailed, message, profileId: context.Profile.ProfileId, tensorName: tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
    }
}

#pragma warning restore CS1591
