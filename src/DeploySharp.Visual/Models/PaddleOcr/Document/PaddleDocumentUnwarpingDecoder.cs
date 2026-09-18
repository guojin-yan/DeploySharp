using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Decodes the UVDoc corrected-image tensor without binding DeploySharp to an imaging library. / 解码 UVDoc 矫正图张量且不绑定具体图像库。</summary>
    public sealed class PaddleDocumentUnwarpingDecoder : IVisualDecoder
    {
        public PaddleDocumentUnwarpingDecoder(PaddleDocumentModelDescriptor descriptor, string outputName = "fetch_name_0", bool clipToUnitRange = false)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.TextImageUnwarping) throw new ArgumentException("The descriptor must identify text-image unwarping.", nameof(descriptor));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            OutputName = outputName;
            ClipToUnitRange = clipToUnitRange;
        }

        public PaddleDocumentModelDescriptor Descriptor { get; }
        public string OutputName { get; }
        public bool ClipToUnitRange { get; }
        public VisualTaskId Task => VisualTaskId.DocumentUnwarping;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "UVDoc output is missing.", exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId); }
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 4) throw new VisualException(VisualErrorCodes.TensorInvalid, "UVDoc output must be Float32 [batch,channels,height,width].", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            int batch = checked((int)tensor.Shape[0]);
            int channels = checked((int)tensor.Shape[1]);
            int height = checked((int)tensor.Shape[2]);
            int width = checked((int)tensor.Shape[3]);
            if (batch != context.Input.BatchSize || channels <= 0 || height <= 0 || width <= 0) throw new VisualException(VisualErrorCodes.TensorInvalid, "UVDoc output shape does not match the prepared input batch.", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            if (!(tensor.Buffer is float[] source) || source.LongLength != tensor.Length) throw new VisualException(VisualErrorCodes.TensorInvalid, "UVDoc output does not expose a managed Float32 buffer.", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId);
            var results = new List<PaddleDocumentUnwarpingResult>(batch);
            int plane = checked(channels * width * height);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var pixels = new float[plane];
                Array.Copy(source, checked(row * plane), pixels, 0, plane);
                for (int index = 0; index < pixels.Length; index++)
                {
                    float value = pixels[index];
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.DecodeFailed, "UVDoc output contains a non-finite pixel.", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: "index=" + index);
                    if (ClipToUnitRange) pixels[index] = Math.Max(0, Math.Min(1, value));
                }
                var frame = context.Input.BatchFrames[row];
                var metadata = new PaddleDocumentResultMetadata(Descriptor, "backend-neutral-decoder", TimeSpan.Zero, frame.InputId ?? context.Input.InputId ?? "input-not-hashed", row);
                results.Add(new PaddleDocumentUnwarpingResult(metadata, width, height, frame.Transform.Kind.ToString(), pixels: pixels, channels: channels));
            }
            return batch == 1 ? (object)results[0] : new PaddleDocumentUnwarpingBatchResult(results);
        }
    }

    public sealed class PaddleDocumentUnwarpingBatchResult
    {
        private readonly IReadOnlyList<PaddleDocumentUnwarpingResult> _items;
        public PaddleDocumentUnwarpingBatchResult(IEnumerable<PaddleDocumentUnwarpingResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<PaddleDocumentUnwarpingResult>();
            foreach (PaddleDocumentUnwarpingResult item in items) copied.Add(item ?? throw new ArgumentException("Unwarping results cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A batch result requires at least two items; batch one uses PaddleDocumentUnwarpingResult.", nameof(items));
            _items = copied.AsReadOnly();
        }
        public int Count => _items.Count;
        public PaddleDocumentUnwarpingResult this[int index] => _items[index];
        public IReadOnlyList<PaddleDocumentUnwarpingResult> Items => _items;
    }
}

#pragma warning restore CS1591
