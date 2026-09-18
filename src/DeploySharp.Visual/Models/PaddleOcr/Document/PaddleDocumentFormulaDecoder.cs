using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Defines a caller-supplied FormulaNet/UniMERNet token vocabulary. / 定义由调用方提供的 FormulaNet/UniMERNet token 词表。</summary>
    public sealed class PaddleDocumentFormulaSchema
    {
        public PaddleDocumentFormulaSchema(IEnumerable<string> tokens, int endTokenId, int startTokenId = -1, int padTokenId = -1, int unknownTokenId = -1)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            var values = new List<string>(tokens);
            if (values.Count == 0) throw new ArgumentException("Formula vocabulary cannot be empty.", nameof(tokens));
            if (endTokenId < 0 || endTokenId >= values.Count) throw new ArgumentOutOfRangeException(nameof(endTokenId));
            ValidateOptional(startTokenId, values.Count, nameof(startTokenId));
            ValidateOptional(padTokenId, values.Count, nameof(padTokenId));
            ValidateOptional(unknownTokenId, values.Count, nameof(unknownTokenId));
            Tokens = values.AsReadOnly(); EndTokenId = endTokenId; StartTokenId = startTokenId; PadTokenId = padTokenId; UnknownTokenId = unknownTokenId;
        }
        public IReadOnlyList<string> Tokens { get; }
        public int EndTokenId { get; }
        public int StartTokenId { get; }
        public int PadTokenId { get; }
        public int UnknownTokenId { get; }
        private static void ValidateOptional(int value, int count, string name) { if (value >= count) throw new ArgumentOutOfRangeException(name); }
    }

    /// <summary>Decodes exported integer token sequences; byte-level BPE detokenization remains an explicit caller concern. / 解码导出的整数 token 序列；字节级 BPE 合并由调用方显式提供。</summary>
    public sealed class PaddleDocumentFormulaDecoder : IVisualDecoder
    {
        public PaddleDocumentFormulaDecoder(PaddleDocumentModelDescriptor descriptor, PaddleDocumentFormulaSchema schema, string outputName = "fetch_name_0", int maximumSequenceLength = 4096)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.FormulaRecognition) throw new ArgumentException("The descriptor must identify formula recognition.", nameof(descriptor));
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (maximumSequenceLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSequenceLength));
            OutputName = outputName; MaximumSequenceLength = maximumSequenceLength;
        }
        public PaddleDocumentModelDescriptor Descriptor { get; }
        public PaddleDocumentFormulaSchema Schema { get; }
        public string OutputName { get; }
        public int MaximumSequenceLength { get; }
        public VisualTaskId Task => VisualTaskId.FormulaRecognition;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "Formula token output is missing.", exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId); }
            if (tensor.Shape.Rank != 2 || (tensor.ElementType != TensorElementType.Int64 && tensor.ElementType != TensorElementType.Int32)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Formula token output must be Int64/Int32 [batch,sequence].", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            int batch = checked((int)tensor.Shape[0]);
            int sequence = checked((int)tensor.Shape[1]);
            if (batch != context.Input.BatchSize || sequence <= 0 || sequence > MaximumSequenceLength) throw new VisualException(VisualErrorCodes.TensorInvalid, "Formula token output shape is outside the declared contract.", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            var results = new List<PaddleDocumentFormulaResult>(batch);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var ids = new List<int>(sequence);
                var latex = new StringBuilder(sequence * 2);
                var warnings = new List<string>();
                for (int step = 0; step < sequence; step++)
                {
                    int id = ReadToken(tensor, checked(row * sequence + step));
                    if (id == Schema.EndTokenId) break;
                    if (id == Schema.StartTokenId || id == Schema.PadTokenId) continue;
                    ids.Add(id);
                    if (id < 0 || id >= Schema.Tokens.Count) { warnings.Add("unknown-token:" + id.ToString(CultureInfo.InvariantCulture)); continue; }
                    string value = Schema.Tokens[id];
                    if (id == Schema.UnknownTokenId) warnings.Add("unknown-token");
                    latex.Append(value);
                }
                var metadata = new PaddleDocumentResultMetadata(Descriptor, "backend-neutral-decoder", TimeSpan.Zero, context.Input.BatchFrames[row].InputId ?? context.Input.InputId ?? "input-not-hashed", row);
                results.Add(new PaddleDocumentFormulaResult(metadata, latex.ToString(), warnings, ids));
            }
            return batch == 1 ? (object)results[0] : new PaddleDocumentFormulaBatchResult(results);
        }

        private static int ReadToken(ITensor tensor, int index)
        {
            if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] longs) return checked((int)longs[index]);
            if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] ints) return ints[index];
            throw new InvalidOperationException("Formula token tensor buffer is not a compatible managed integer array.");
        }
    }

    public sealed class PaddleDocumentFormulaBatchResult
    {
        private readonly IReadOnlyList<PaddleDocumentFormulaResult> _items;
        public PaddleDocumentFormulaBatchResult(IEnumerable<PaddleDocumentFormulaResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<PaddleDocumentFormulaResult>();
            foreach (PaddleDocumentFormulaResult item in items) copied.Add(item ?? throw new ArgumentException("Formula results cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A batch result requires at least two items; batch one uses PaddleDocumentFormulaResult.", nameof(items));
            _items = copied.AsReadOnly();
        }
        public int Count => _items.Count;
        public PaddleDocumentFormulaResult this[int index] => _items[index];
        public IReadOnlyList<PaddleDocumentFormulaResult> Items => _items;
    }
}

#pragma warning restore CS1591
