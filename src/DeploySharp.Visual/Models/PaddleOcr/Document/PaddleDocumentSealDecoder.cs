using System;
using System.Collections.Generic;
using System.Globalization;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Decodes PP-OCRv4 seal probability maps into source-space connected regions. / 将 PP-OCRv4 印章概率图解码为源图连通区域。</summary>
    public sealed class PaddleDocumentSealDecoder : IVisualDecoder
    {
        public PaddleDocumentSealDecoder(PaddleDocumentModelDescriptor descriptor, string outputName = "fetch_name_0", float threshold = .3f, int minimumArea = 16, int maximumRegions = 256)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.SealTextDetection) throw new ArgumentException("The descriptor must identify seal detection.", nameof(descriptor));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (minimumArea <= 0 || maximumRegions <= 0) throw new ArgumentOutOfRangeException(nameof(minimumArea));
            OutputName = outputName; Threshold = threshold; MinimumArea = minimumArea; MaximumRegions = maximumRegions;
        }
        public PaddleDocumentModelDescriptor Descriptor { get; }
        public string OutputName { get; }
        public float Threshold { get; }
        public int MinimumArea { get; }
        public int MaximumRegions { get; }
        public VisualTaskId Task => VisualTaskId.SealTextDetection;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "Seal probability output is missing.", exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId); }
            if (tensor.ElementType != TensorElementType.Float32 || !(tensor.Buffer is float[] values) || tensor.Shape.Rank != 4 || tensor.Shape[1] != 1) throw new VisualException(VisualErrorCodes.TensorInvalid, "Seal output must be Float32 [batch,1,height,width].", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            int batch = checked((int)tensor.Shape[0]);
            int height = checked((int)tensor.Shape[2]);
            int width = checked((int)tensor.Shape[3]);
            if (batch != context.Input.BatchSize) throw new VisualException(VisualErrorCodes.TensorInvalid, "Seal output batch does not match the prepared input batch.", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId);
            var results = new List<PaddleDocumentSealResult>(batch);
            int plane = checked(width * height);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                results.Add(DecodeRow(context, row, width, height, values, checked(row * plane)));
            }
            return batch == 1 ? (object)results[0] : new PaddleDocumentSealBatchResult(results);
        }

        private PaddleDocumentSealResult DecodeRow(VisualDecodeContext context, int row, int width, int height, float[] values, int offset)
        {
            int plane = checked(width * height);
            bool[] visited = VisualArrayPool<bool>.Rent(plane);
            Array.Clear(visited, 0, plane);
            var regions = new List<PaddleDocumentRegion>();
            int[] queue = VisualArrayPool<int>.Rent(Math.Max(width, height) * 2);
            try
            {
                for (int y = 0; y < height && regions.Count < MaximumRegions; y++) for (int x = 0; x < width && regions.Count < MaximumRegions; x++)
                {
                    int seed = y * width + x;
                    float seedScore = values[offset + seed];
                    if (float.IsNaN(seedScore) || float.IsInfinity(seedScore)) throw Failure(context, "Seal probability output contains a non-finite value.", offset + seed);
                    if (visited[seed] || seedScore < Threshold) continue;
                    int head = 0, tail = 0, area = 0;
                    int minX = x, minY = y, maxX = x, maxY = y;
                    float maxScore = seedScore, sum = 0;
                    EnsureQueueCapacity(ref queue, tail + 1); queue[tail++] = seed; visited[seed] = true;
                    while (head < tail)
                    {
                        int current = queue[head++]; int cx = current % width, cy = current / width; float score = values[offset + current];
                        area++; sum += score; maxScore = Math.Max(maxScore, score); minX = Math.Min(minX, cx); minY = Math.Min(minY, cy); maxX = Math.Max(maxX, cx); maxY = Math.Max(maxY, cy);
                        TryVisit(cx - 1, cy, width, height, values, offset, visited, ref queue, ref tail, context);
                        TryVisit(cx + 1, cy, width, height, values, offset, visited, ref queue, ref tail, context);
                        TryVisit(cx, cy - 1, width, height, values, offset, visited, ref queue, ref tail, context);
                        TryVisit(cx, cy + 1, width, height, values, offset, visited, ref queue, ref tail, context);
                    }
                    if (area < MinimumArea) continue;
                    var frame = context.Input.BatchFrames[row];
                    RectangleF modelBox = new RectangleF(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    RectangleF sourceBox = frame.Transform.ClipToSource(frame.Transform.ToSource(modelBox));
                    var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["mask-area"] = area.ToString(CultureInfo.InvariantCulture), ["mask-score"] = (sum / area).ToString("R", CultureInfo.InvariantCulture) };
                    regions.Add(new PaddleDocumentRegion("seal", maxScore, sourceBox, metadata));
                }
            }
            finally
            {
                VisualArrayPool<int>.Return(queue);
                VisualArrayPool<bool>.Return(visited);
            }
            var warnings = regions.Count >= MaximumRegions ? new[] { "maximum-regions-reached" } : Array.Empty<string>();
            var resultMetadata = new PaddleDocumentResultMetadata(Descriptor, "backend-neutral-decoder", TimeSpan.Zero, context.Input.BatchFrames[row].InputId ?? context.Input.InputId ?? "input-not-hashed", row);
            return new PaddleDocumentSealResult(resultMetadata, width, height, regions, warnings);
        }

        private void TryVisit(int x, int y, int width, int height, float[] values, int offset, bool[] visited, ref int[] queue, ref int tail, VisualDecodeContext context)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = y * width + x;
            float score = values[offset + index];
            if (float.IsNaN(score) || float.IsInfinity(score)) throw Failure(context, "Seal probability output contains a non-finite value.", offset + index);
            if (visited[index] || score < Threshold) return;
            EnsureQueueCapacity(ref queue, tail + 1); queue[tail++] = index; visited[index] = true;
        }
        private static void EnsureQueueCapacity(ref int[] queue, int required)
        {
            if (required <= queue.Length) return;
            int[] previous = queue;
            queue = new int[Math.Max(required, queue.Length * 2)];
            Array.Copy(previous, queue, previous.Length);
            VisualArrayPool<int>.Return(previous);
        }
        private VisualException Failure(VisualDecodeContext context, string message, int index) => new VisualException(VisualErrorCodes.DecodeFailed, message, profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: "index=" + index.ToString(CultureInfo.InvariantCulture));
    }

    public sealed class PaddleDocumentSealBatchResult
    {
        private readonly IReadOnlyList<PaddleDocumentSealResult> _items;
        public PaddleDocumentSealBatchResult(IEnumerable<PaddleDocumentSealResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<PaddleDocumentSealResult>(); foreach (PaddleDocumentSealResult item in items) copied.Add(item ?? throw new ArgumentException("Seal results cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A batch result requires at least two items; batch one uses PaddleDocumentSealResult.", nameof(items));
            _items = copied.AsReadOnly();
        }
        public int Count => _items.Count;
        public PaddleDocumentSealResult this[int index] => _items[index];
        public IReadOnlyList<PaddleDocumentSealResult> Items => _items;
    }
}

#pragma warning restore CS1591
