using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Decodes a converted Chart2Table integer-token sequence through a caller-provided tokenizer. / 使用调用方提供的 tokenizer 解码已转换 Chart2Table 整数 token 序列。</summary>
    public sealed class PaddleDocumentChartDecoder : IVisualDecoder
    {
        public PaddleDocumentChartDecoder(PaddleDocumentModelDescriptor descriptor, IPaddleDocumentFormulaTokenizer tokenizer, string outputName = "fetch_name_0", int endTokenId = -1, int startTokenId = -1, int padTokenId = -1, int maximumSequenceLength = 8192)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.ChartParsing) throw new ArgumentException("The descriptor must identify chart parsing.", nameof(descriptor));
            Tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            ValidateOptional(endTokenId, tokenizer.Tokens.Count, nameof(endTokenId));
            ValidateOptional(startTokenId, tokenizer.Tokens.Count, nameof(startTokenId));
            ValidateOptional(padTokenId, tokenizer.Tokens.Count, nameof(padTokenId));
            if (maximumSequenceLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSequenceLength));
            OutputName = outputName; EndTokenId = endTokenId; StartTokenId = startTokenId; PadTokenId = padTokenId; MaximumSequenceLength = maximumSequenceLength;
        }

        public PaddleDocumentModelDescriptor Descriptor { get; }
        public IPaddleDocumentFormulaTokenizer Tokenizer { get; }
        public string OutputName { get; }
        public int EndTokenId { get; }
        public int StartTokenId { get; }
        public int PadTokenId { get; }
        public int MaximumSequenceLength { get; }
        public VisualTaskId Task => VisualTaskId.ChartParsing;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Input.BatchSize != 1) throw Failure(context, "Chart2Table decoding currently requires batch size one.");
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw Failure(context, "Chart token output is missing.", exception); }
            if (tensor.ElementType != TensorElementType.Int64 && tensor.ElementType != TensorElementType.Int32 || tensor.Shape.Rank != 2 || tensor.Shape[0] != 1 || tensor.Shape[1] <= 0 || tensor.Shape[1] > MaximumSequenceLength)
                throw Failure(context, "Chart token output must be Int32/Int64 [1,sequence].", technicalDetails: tensor.Shape.ToString());

            int sequence = checked((int)tensor.Shape[1]);
            var ids = new List<int>(sequence);
            var warnings = new List<string>();
            for (int index = 0; index < sequence; index++)
            {
                int id = ReadToken(tensor, index);
                if (id == EndTokenId) break;
                if (id == StartTokenId || id == PadTokenId) continue;
                ids.Add(id);
                if (id < 0 || id >= Tokenizer.Tokens.Count) warnings.Add("unknown-token:" + id.ToString(CultureInfo.InvariantCulture));
            }

            string structuredData;
            try { structuredData = Tokenizer.Decode(ids); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                warnings.Add("tokenizer-fallback:" + exception.GetType().Name);
                var fallback = new StringBuilder();
                foreach (int id in ids) if (id >= 0 && id < Tokenizer.Tokens.Count) fallback.Append(Tokenizer.Tokens[id]);
                structuredData = fallback.ToString();
            }

            string inputId = context.Input.BatchFrames[0].InputId ?? context.Input.InputId ?? "input-not-hashed";
            var metadata = new PaddleDocumentResultMetadata(Descriptor, "backend-neutral-decoder", TimeSpan.Zero, inputId);
            return new PaddleDocumentChartResult(metadata, structuredData, warnings: warnings, tokenIds: ids);
        }

        private static int ReadToken(ITensor tensor, int index)
        {
            if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] longs) return checked((int)longs[index]);
            if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] ints) return ints[index];
            throw new InvalidOperationException("Chart token tensor buffer is not a compatible managed integer array.");
        }

        private VisualException Failure(VisualDecodeContext context, string message, Exception? exception = null, string? technicalDetails = null)
            => new VisualException(VisualErrorCodes.DecodeFailed, message, exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);

        private static void ValidateOptional(int value, int count, string name)
        {
            if (value >= count) throw new ArgumentOutOfRangeException(name);
        }
    }
}

#pragma warning restore CS1591
