using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlTokenizerTests
{
    private static readonly Lazy<PaddleOcrVlTokenizer> Tokenizer = new(() => new PaddleOcrVlTokenizer(ModelRoot()));

    private static string ModelRoot()
    {
        string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "tokenizer.model")))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT to the pinned official PaddleOCR-VL-1.6 asset directory.");
        return root!;
    }

    private static JsonDocument Reference()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        const string name = "eng/models/paddle-ocr/verification/paddleocr-vl-tokenizer-reference-20261023.json";
        while (current != null)
        {
            string path = Path.Combine(current.FullName, name);
            if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllBytes(path));
            current = current.Parent;
        }
        throw new FileNotFoundException("The independent official tokenizer reference is missing.", name);
    }

    [TestMethod]
    public void PlainWhitespaceUnicodeAndStructuralVectorsMatchOfficialAssets()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using JsonDocument report = Reference();
        int count = 0;
        foreach (JsonElement vector in report.RootElement.GetProperty("textVectors").EnumerateArray())
        {
            string text = vector.GetProperty("text").GetString()!;
            int[] expected = vector.GetProperty("tokenIds").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            CollectionAssert.AreEqual(expected, tokenizer.EncodeText(text).ToArray(), text);
            Assert.AreEqual(vector.GetProperty("decoded").GetString(), tokenizer.DecodeCompletion(expected), text);
            Assert.AreEqual(vector.GetProperty("decodedWithSpecialTokens").GetString(), tokenizer.DecodeCompletion(expected, false), text);
            count++;
        }
        Assert.AreEqual(18, count);
    }

    [TestMethod]
    [DataRow(PaddleOcrVlTask.TextRecognition)]
    [DataRow(PaddleOcrVlTask.TableRecognition)]
    [DataRow(PaddleOcrVlTask.FormulaRecognition)]
    [DataRow(PaddleOcrVlTask.ChartRecognition)]
    [DataRow(PaddleOcrVlTask.TextSpotting)]
    [DataRow(PaddleOcrVlTask.SealRecognition)]
    public void ExpandedTaskPromptsMatchOfficialTokens(PaddleOcrVlTask task)
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using JsonDocument report = Reference();
        JsonElement vector = report.RootElement.GetProperty("taskVectors").EnumerateArray().Single(v => v.GetProperty("task").GetString() == task.ToString());
        int side = task == PaddleOcrVlTask.TextSpotting ? 168 : 336;
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(side, side), task);
        PaddleOcrVlTokenSequence sequence = tokenizer.EncodePrompt(plan);
        Assert.AreEqual(vector.GetProperty("promptTemplate").GetString(), sequence.PromptTemplate);
        Assert.AreEqual(vector.GetProperty("expandedTokenCount").GetInt32(), sequence.Count);
        Assert.AreEqual(vector.GetProperty("imageOffset").GetInt32(), sequence.ImageTokenOffset);
        Assert.AreEqual(100273L, sequence.TokenIds[0]); // CLS, never BOS=1.
        Assert.AreEqual(144, sequence.TokenIds.Count(id => id == tokenizer.ImagePatchTokenId));
        string csv = string.Join(",", sequence.TokenIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        Assert.AreEqual(vector.GetProperty("tokenCsvSha256").GetString(), Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(csv))).ToLowerInvariant());
        Assert.AreEqual(tokenizer.Sha256, sequence.TokenizerSha256);
    }

    [TestMethod]
    public void All1041AddedTokensRetainOfficialIdsAndSpecialFlags()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using JsonDocument config = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ModelRoot(), "tokenizer_config.json")));
        int count = 0;
        foreach (JsonProperty token in config.RootElement.GetProperty("added_tokens_decoder").EnumerateObject())
        {
            int id = int.Parse(token.Name, CultureInfo.InvariantCulture);
            string text = token.Value.GetProperty("content").GetString()!;
            CollectionAssert.AreEqual(new[] { id }, tokenizer.EncodeText(text).ToArray(), text);
            Assert.AreEqual(text, tokenizer.DecodeCompletion(new[] { id }, false), text);
            Assert.AreEqual(token.Value.GetProperty("special").GetBoolean() ? "" : text, tokenizer.DecodeCompletion(new[] { id }), text);
            count++;
        }
        Assert.AreEqual(1041, count);
    }

    [TestMethod]
    public void DecodingMatchesOfficialIncompleteByteFallbackAndSpecialRemoval()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using JsonDocument report = Reference();
        foreach (JsonElement vector in report.RootElement.GetProperty("decodeVectors").EnumerateArray())
        {
            int[] ids = vector.GetProperty("tokenIds").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            Assert.AreEqual(vector.GetProperty("decoded").GetString(), tokenizer.DecodeCompletion(ids));
            Assert.AreEqual(vector.GetProperty("decodedWithSpecialTokens").GetString(), tokenizer.DecodeCompletion(ids, false));
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void EveryVocabularySlotDecodesToTheOfficialValue(bool skipSpecialTokens)
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using JsonDocument report = Reference();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] size = new byte[4];
        for (int id = 0; id < tokenizer.VocabularySize; id++)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(tokenizer.DecodeCompletion(new[] { id }, skipSpecialTokens));
            BinaryPrimitives.WriteInt32LittleEndian(size, bytes.Length);
            hash.AppendData(size);
            hash.AppendData(bytes);
        }
        string role = skipSpecialTokens ? "skipSpecialTokens" : "includeSpecialTokens";
        Assert.AreEqual(report.RootElement.GetProperty("allVocabularyDecode").GetProperty("sha256").GetProperty(role).GetString(), Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    [TestMethod]
    public void PromptBudgetsAreCheckedIncludingImageTokensAndReservedOutput()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336));
        Assert.AreEqual(157, tokenizer.EncodePrompt(plan, maximumNewTokens: 1, maximumContextTokens: 158).Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tokenizer.EncodePrompt(plan, maximumNewTokens: 1, maximumContextTokens: 157));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tokenizer.EncodePrompt(plan, maximumNewTokens: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tokenizer.EncodePrompt(plan, maximumNewTokens: 100, maximumContextTokens: 100));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tokenizer.EncodePrompt(plan, maximumContextTokens: 131073));
        Assert.ThrowsExactly<ArgumentNullException>(() => tokenizer.EncodePrompt(null!));
    }

    [TestMethod]
    public async Task SharedTokenizerHasIndependentConcurrentRequestState()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336));
        PaddleOcrVlTokenSequence[] sequences = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() =>
        {
            CollectionAssert.AreEqual(new[] { 18830, 269, 10834 }, tokenizer.EncodeText("hello  world").ToArray());
            Assert.AreEqual("🧪", tokenizer.DecodeCompletion(new[] { 253, 172, 180, 183 }));
            return tokenizer.EncodePrompt(plan);
        })));
        long[] copy = sequences[0].CopyTokenIds();
        copy[0] = 0;
        foreach (PaddleOcrVlTokenSequence sequence in sequences)
        {
            Assert.AreEqual(100273L, sequence.TokenIds[0]);
            Assert.AreEqual(157, sequence.Count);
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<long>)sequence.TokenIds)[0] = 0);
        }
        Assert.AreNotSame(sequences[0].TokenIds, sequences[1].TokenIds);
    }

    [TestMethod]
    public void RejectsInvalidTokenIdsAndNullText()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        Assert.ThrowsExactly<VisualException>(() => tokenizer.DecodeCompletion(new[] { -1 }));
        Assert.ThrowsExactly<VisualException>(() => tokenizer.DecodeCompletion(new[] { 103424 }));
        Assert.ThrowsExactly<ArgumentNullException>(() => tokenizer.DecodeCompletion(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => tokenizer.EncodeText(null!));
        Assert.AreEqual("0 1 2 3 4 5 6 7 8 9", tokenizer.DecodeCompletion(tokenizer.EncodeText("0 1 2 3 4 5 6 7 8 9")));
    }

    [TestMethod]
    public void RejectsMissingAndMismatchedOfficialAssets()
    {
        string source = ModelRoot();
        string directory = Path.Combine(Path.GetTempPath(), "deploysharp-vl-tokenizer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            VisualException missing = Assert.ThrowsExactly<VisualException>(() => new PaddleOcrVlTokenizer(directory));
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, missing.ErrorCode);
            foreach (string name in new[] { "tokenizer.model", "tokenizer.json", "tokenizer_config.json", "chat_template.jinja" })
                File.Copy(Path.Combine(source, name), Path.Combine(directory, name));
            File.AppendAllText(Path.Combine(directory, "chat_template.jinja"), " ");
            VisualException mismatch = Assert.ThrowsExactly<VisualException>(() => new PaddleOcrVlTokenizer(directory));
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, mismatch.ErrorCode);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void RejectsEmptyAssetDirectoryArgument()
        => Assert.ThrowsExactly<ArgumentException>(() => new PaddleOcrVlTokenizer(" "));
}
