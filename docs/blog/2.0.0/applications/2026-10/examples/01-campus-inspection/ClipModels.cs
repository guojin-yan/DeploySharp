using System.Net;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.ML.Tokenizers;

public sealed record Prompt(string Key, string Text);
public sealed record PromptGroup(string Name, int[] ClassIds, Prompt[] Candidates);
internal sealed record RoiBatch(OpenCvDecodedRoiImage Image, IReadOnlyList<IVisualRoiGeometry> Regions);
internal sealed record ImageEmbeddings(float[][] Values);
public sealed record SemanticMatch(string Key, string Text, double Cosine, double CandidateScore);

internal static class ClipModels
{
    public static VisualModelDefinition<RoiBatch, ImageEmbeddings> CreateImageEncoder()
    {
        var clip = VisionLanguageProfiles.CreateClipVitB32();
        var processing = new VisualPreprocessingOptions(
            new VisualSize(224, 224), VisualResizeMode.ShortestEdgeCenterCrop, VisualColorOrder.Rgb,
            VisualNormalizationOptions.MeanStandardDeviation(clip.ImageMean, clip.ImageStandardDeviation),
            VisualTensorLayout.Nchw, interpolation: VisualInterpolationMode.Cubic);
        return new VisualModelDefinitionBuilder<RoiBatch, ImageEmbeddings>(
                "blog.clip.roi-batch", new ModelId("blog/clip/image"), new VisualTaskId("blog-roi-embedding"), "1", "onnx")
            .WithInput("pixel_values", TensorElementType.Float32, new TensorShape(-1, 3, 224, 224),
                VisualTensorLayout.Nchw, minimumBatch: 1, maximumBatch: 16)
            .WithPreprocessing(processing)
            .AddOutput("image_embedding", TensorElementType.Float32, new TensorShape(-1, 512))
            .WithInputPreprocessor((input, profile, ct) => input.Image.PrepareBatch(
                input.Regions, "pixel_values", profile.Preprocessing!.ToOpenCvOptions(), ct))
            .WithDecoder(context => new ImageEmbeddings(NormalizeRows(
                (float[])context.GetOutput<float>("image_embedding").Buffer, context.Input.BatchSize)))
            .Build();
    }

    public static async Task<float[][]> EncodePromptsAsync(
        BackendRegistry backends, BackendRequest request, string models, Prompt[] prompts, CancellationToken ct)
    {
        if (prompts.Length is < 1 or > 64) throw new InvalidDataException("候选描述总数需要在 1—64 之间。");
        var tokenizer = new EnglishClipTokenizer(models);
        var (ids, mask) = tokenizer.Encode(prompts.Select(p => p.Text).ToArray());
        using var text = backends.CreateSession(new ModelArtifact(
            new ModelId("blog/clip/text"), "onnx", Path.Combine(models, "clip-vit-b-32.text-encoder.onnx")), request);
        var outputs = await text.RunAsync(new InferenceInputs([
            new NamedTensor("input_ids", new Tensor<long>(new TensorShape(prompts.Length, 77), ids, TensorBufferOwnership.Transfer)),
            new NamedTensor("attention_mask", new Tensor<long>(new TensorShape(prompts.Length, 77), mask, TensorBufferOwnership.Transfer))]), ct);
        // 文本候选只编码一次；后续每批 ROI 直接复用这些特征。
        return NormalizeRows((float[])outputs.GetRequired("text_embedding").Buffer, prompts.Length);
    }

    public static float[][] NormalizeRows(float[] values, int rows)
    {
        if (values.Length != rows * 512 || values.Any(x => !float.IsFinite(x)))
            throw new InvalidDataException("CLIP 需要每行 512 维有限特征。");
        var result = new float[rows][];
        for (int row = 0; row < rows; row++)
        {
            double norm = 0;
            for (int d = 0; d < 512; d++) norm += (double)values[row * 512 + d] * values[row * 512 + d];
            if (norm < 1e-20) throw new InvalidDataException("CLIP 返回零向量。");
            double scale = 1 / Math.Sqrt(norm);
            result[row] = new float[512];
            for (int d = 0; d < 512; d++) result[row][d] = (float)(values[row * 512 + d] * scale);
        }
        return result;
    }

    public static SemanticMatch[] Match(float[] image, PromptGroup group,
        IReadOnlyDictionary<string, float[]> texts)
    {
        var pairs = group.Candidates.Select(prompt =>
        {
            double cosine = 0;
            float[] text = texts[prompt.Key];
            for (int d = 0; d < image.Length; d++) cosine += (double)image[d] * text[d];
            return (prompt, cosine, logit: cosine * 100.0000076);
        }).ToArray();
        double max = pairs.Max(x => x.logit), sum = pairs.Sum(x => Math.Exp(x.logit - max));
        return pairs.Select(x => new SemanticMatch(x.prompt.Key, x.prompt.Text, x.cosine,
                Math.Exp(x.logit - max) / sum))
            .OrderByDescending(x => x.CandidateScore).ToArray();
    }
}

// 本教程限定英文 ASCII 候选，避免把简化清洗冒充任意语言的官方 ftfy 行为。
internal sealed class EnglishClipTokenizer
{
    private readonly BpeTokenizer _tokenizer;
    public EnglishClipTokenizer(string directory)
    {
        _tokenizer = BpeTokenizer.Create(new BpeOptions(
            Path.Combine(directory, "clip-vit-b-32.vocab.json"),
            Path.Combine(directory, "clip-vit-b-32.merges.txt"))
        {
            ByteLevel = true, EndOfWordSuffix = "</w>",
            PreTokenizer = new RegexPreTokenizer(new Regex(
                "'s|'t|'re|'ve|'m|'ll|'d|[a-z]+|[0-9]|[^\\s\\p{L}\\p{N}]+", RegexOptions.CultureInvariant), null),
            UnknownToken = "<|endoftext|>"
        });
    }

    public (long[] Ids, long[] Mask) Encode(string[] texts)
    {
        var ids = Enumerable.Repeat(49407L, texts.Length * 77).ToArray();
        var mask = new long[ids.Length];
        for (int row = 0; row < texts.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(texts[row]) || texts[row].Any(c => c < 32 || c > 126 || c is '<' or '>'))
                throw new InvalidDataException("本例候选描述需为非空可打印 ASCII 英文，不接受特殊 token。");
            string normalized = Regex.Replace(WebUtility.HtmlDecode(WebUtility.HtmlDecode(texts[row])), "\\s+", " ")
                .Trim().ToLowerInvariant();
            if (normalized.Any(c => c < 32 || c > 126 || c is '<' or '>'))
                throw new InvalidDataException("HTML 清洗后的候选超出本文 tokenizer 的英文范围。");
            var tokens = _tokenizer.EncodeToIds(normalized);
            if (tokens.Count > 75) throw new InvalidDataException("候选描述超出 CLIP 的 77 token 上限，不静默截断。");
            int start = row * 77;
            ids[start] = 49406; mask[start] = 1;
            for (int i = 0; i < tokens.Count; i++) { ids[start + 1 + i] = tokens[i]; mask[start + 1 + i] = 1; }
            mask[start + tokens.Count + 1] = 1;
        }
        return (ids, mask);
    }
}
