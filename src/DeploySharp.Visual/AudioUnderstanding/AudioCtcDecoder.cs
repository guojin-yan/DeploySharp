using System;
using System.Collections.Generic;
using System.Text;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Performs deterministic lowest-index greedy CTC decoding with frame-aligned token spans. / 执行确定性最低索引 Greedy CTC 解码并生成帧对齐 Token 区间。</summary>
    public sealed class AudioCtcDecoder
    {
        /// <summary>Initializes a decoder bound to one exact vocabulary and timestamp contract. / 初始化绑定精确词表与时间戳合同的 Decoder。</summary>
        public AudioCtcDecoder(Wav2Vec2CtcVocabulary vocabulary, AudioTimestampContract timestamps)
        {
            Vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary)); Timestamps = timestamps ?? throw new ArgumentNullException(nameof(timestamps));
            if (timestamps.Ownership != AudioTimestampOwnership.CtcFrameStride || timestamps.SecondsPerFrame <= 0) throw new VisualException(VisualErrorCodes.AudioContractInvalid, "CTC decoding requires a positive frame-stride timestamp contract.");
        }

        /// <summary>Gets bound vocabulary. / 获取绑定词表。</summary>
        public Wav2Vec2CtcVocabulary Vocabulary { get; }
        /// <summary>Gets frame-to-time contract. / 获取帧到时间合同。</summary>
        public AudioTimestampContract Timestamps { get; }

        /// <summary>Decodes `[1, frames, vocabulary]` logits; ties select the lowest token ID. / 解码 `[1, frames, vocabulary]` Logits；平局选择最低 Token ID。</summary>
        public AudioCtcDecodedResult Decode(ITensor logits, bool includeTokenTimestamps = true)
        {
            if (logits == null) throw new ArgumentNullException(nameof(logits));
            AudioTokenizerContract contract = Vocabulary.Contract;
            if (logits.ElementType != TensorElementType.Float32 || logits.Shape.Rank != 3 || logits.Shape[0] != 1 || logits.Shape[1] <= 0 || logits.Shape[2] != contract.VocabularySize) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "CTC logits type or shape differs from the vocabulary contract.", tensorName: "logits");
            if (logits.Shape[1] > int.MaxValue) throw AudioFailure.Limit("CTC frame count exceeds managed capacity.", tensorName: "logits");
            float[] values = (float[])logits.Buffer; int frames = checked((int)logits.Shape[1]); int classes = contract.VocabularySize;
            var raw = new List<int>(frames); var collapsed = new List<int>(Math.Min(frames, 256)); var segments = new List<MutableSegment>(includeTokenTimestamps ? Math.Min(frames, 256) : 0); int previous = -1;
            for (int frame = 0; frame < frames; frame++)
            {
                int offset = checked(frame * classes); int selected = 0; float maximum = values[offset];
                if (float.IsNaN(maximum) || float.IsInfinity(maximum)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "CTC logits contain NaN or Infinity.", tensorName: "logits");
                for (int token = 1; token < classes; token++)
                {
                    float current = values[offset + token]; if (float.IsNaN(current) || float.IsInfinity(current)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "CTC logits contain NaN or Infinity.", tensorName: "logits");
                    if (current > maximum) { maximum = current; selected = token; }
                }
                raw.Add(selected);
                if (selected != contract.BlankTokenId && selected != previous)
                {
                    collapsed.Add(selected);
                    if (includeTokenTimestamps) segments.Add(new MutableSegment(selected, frame, Probability(values, offset, classes, selected, maximum)));
                }
                else if (includeTokenTimestamps && selected != contract.BlankTokenId && selected == previous)
                {
                    MutableSegment segment = segments[segments.Count - 1]; segment.EndFrameExclusive = frame + 1; segment.ProbabilitySum += Probability(values, offset, classes, selected, maximum); segment.FrameCount++;
                }
                previous = selected;
            }
            var text = new StringBuilder(); var outputSegments = new List<AudioCtcTokenSegment>(segments.Count);
            if (includeTokenTimestamps)
            {
                foreach (MutableSegment segment in segments)
                {
                    string token = Vocabulary.GetToken(segment.TokenId);
                    AppendTokenText(text, token, segment.TokenId, contract);
                    outputSegments.Add(new AudioCtcTokenSegment(segment.TokenId, token, segment.StartFrame, segment.EndFrameExclusive, Timestamps.SecondsPerFrame, (float)(segment.ProbabilitySum / segment.FrameCount)));
                }
            }
            else foreach (int tokenId in collapsed) AppendTokenText(text, Vocabulary.GetToken(tokenId), tokenId, contract);
            return new AudioCtcDecodedResult(text.ToString().Trim(), raw, collapsed, outputSegments);
        }

        private static void AppendTokenText(StringBuilder text, string token, int tokenId, AudioTokenizerContract contract)
        {
            if (tokenId == contract.WordDelimiterTokenId) text.Append(' ');
            else if (tokenId == contract.UnknownTokenId) text.Append(token);
            else if (!(token.StartsWith("<", StringComparison.Ordinal) && token.EndsWith(">", StringComparison.Ordinal))) text.Append(token);
        }

        private static double Probability(float[] values, int offset, int classes, int selected, float maximum)
        {
            double sum = 0; for (int token = 0; token < classes; token++) sum += Math.Exp(values[offset + token] - maximum);
            return 1.0 / sum;
        }

        private sealed class MutableSegment
        {
            internal MutableSegment(int tokenId, int frame, double probability) { TokenId = tokenId; StartFrame = frame; EndFrameExclusive = frame + 1; ProbabilitySum = probability; FrameCount = 1; }
            internal int TokenId { get; } internal int StartFrame { get; } internal int EndFrameExclusive { get; set; } internal double ProbabilitySum { get; set; } internal int FrameCount { get; set; }
        }
    }
}
