using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlPositionPlanTests
{
    private static readonly Lazy<PaddleOcrVlTokenizer> Tokenizer = new(() =>
    {
        string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "tokenizer.model")))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT to the pinned official assets.");
        return new PaddleOcrVlTokenizer(root!);
    });

    private static JsonDocument Reference(string name)
        => JsonDocument.Parse(File.ReadAllBytes(RepositoryTestFiles.Find(Path.Combine("tests", "assets", "paddleocr-vl", name))));

    [TestMethod]
    [DataRow("ocr-demo-1", PaddleOcrVlTask.TextRecognition)]
    [DataRow("table-official-example", PaddleOcrVlTask.TableRecognition)]
    [DataRow("formula-official-example", PaddleOcrVlTask.FormulaRecognition)]
    [DataRow("chart-official-example", PaddleOcrVlTask.ChartRecognition)]
    [DataRow("spotting-demo-1", PaddleOcrVlTask.TextSpotting)]
    [DataRow("seal-official-example", PaddleOcrVlTask.SealRecognition)]
    public void RealImagePrefillAndDecodeMatchBothPinnedUpstreamImplementations(string id, PaddleOcrVlTask task)
    {
        using JsonDocument images = Reference("six-task-reference.json");
        JsonElement image = images.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
        JsonElement size = image.GetProperty("sourceSize");
        PaddleOcrVlTokenSequence prompt = Tokenizer.Value.EncodePrompt(PaddleOcrVlInputPlanning.PlanOfficialV16(
            new VisualSize(size[0].GetInt32(), size[1].GetInt32()), task));
        PaddleOcrVlPositionPlan positions = PaddleOcrVlPositionPlan.Create(prompt);
        using JsonDocument expected = Reference("position-reference.json");
        Assert.AreEqual("all-positions-exact", expected.RootElement.GetProperty("status").GetString());
        JsonElement vector = expected.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
        Assert.IsTrue(vector.GetProperty("allThreeEqual").GetBoolean());
        long[] buffer = positions.CopyPositionIds();
        long[] original = (long[])buffer.Clone();
        CollectionAssert.AreEqual(vector.GetProperty("inputTokenIds").EnumerateArray().Select(v => v.GetInt64()).ToArray(), prompt.CopyTokenIds());
        CollectionAssert.AreEqual(vector.GetProperty("positionIds").EnumerateArray()
            .SelectMany(axis => axis[0].EnumerateArray()).Select(v => v.GetInt64()).ToArray(), buffer);
        Assert.AreEqual(vector.GetProperty("ropeDelta").GetInt32(), positions.RopeDelta);
        Assert.AreEqual(prompt.Count, positions.PromptLength);
        foreach (JsonElement decode in vector.GetProperty("decode").EnumerateArray())
        {
            var destination = new long[] { -99, -99, -99, -99, -99 };
            positions.WriteDecodePositionIds(decode.GetProperty("pastTokens").GetInt32(), 1, destination, 1);
            CollectionAssert.AreEqual(decode.GetProperty("positionIds").EnumerateArray().Select(v => v.GetInt64()).ToArray(), destination[1..4]);
            Assert.AreEqual(-99L, destination[0]);
            Assert.AreEqual(-99L, destination[4]);
        }
        buffer[0] = -1000;
        CollectionAssert.AreEqual(original, positions.CopyPositionIds());
        Assert.IsTrue(positions.RopeDelta < 0, "Image token counts are compressed to spatial coordinates.");
    }

    [TestMethod]
    public void DecodeValidatesCompletePrefillDestinationAndReservedGenerationBudget()
    {
        PaddleOcrVlTokenSequence prompt = Tokenizer.Value.EncodePrompt(PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336)), maximumNewTokens: 4);
        PaddleOcrVlPositionPlan positions = PaddleOcrVlPositionPlan.Create(prompt);
        var sentinel = Enumerable.Repeat(-99L, 9).ToArray();
        Assert.ThrowsExactly<ArgumentNullException>(() => PaddleOcrVlPositionPlan.Create(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count - 1, 1, sentinel));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count, 0, sentinel));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count, 4, sentinel));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(int.MaxValue, int.MaxValue, sentinel));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count, 1, sentinel, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count, 1, sentinel, int.MaxValue));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count, 3, sentinel, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => positions.WriteDecodePositionIds(prompt.Count, 1, null!));
        Assert.IsTrue(sentinel.All(v => v == -99L), "Failed validation must not partially write the caller's buffer.");
        positions.WriteDecodePositionIds(prompt.Count, 3, sentinel);
        for (int axis = 0; axis < 3; axis++)
            CollectionAssert.AreEqual(new long[] { prompt.Count + positions.RopeDelta, prompt.Count + positions.RopeDelta + 1, prompt.Count + positions.RopeDelta + 2 }, sentinel[(axis * 3)..(axis * 3 + 3)]);
        positions.WriteDecodePositionIds(prompt.Count + 2, 1, sentinel);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => positions.WriteDecodePositionIds(prompt.Count + 3, 1, sentinel));
        PaddleOcrVlPositionPlan oneToken = PaddleOcrVlPositionPlan.Create(Tokenizer.Value.EncodePrompt(prompt.ImagePlan, maximumNewTokens: 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => oneToken.WriteDecodePositionIds(oneToken.PromptLength, 1, sentinel));
    }

    [TestMethod]
    public async Task ConcurrentRequestsAndDecodeCallsRetainTheirOwnPositions()
    {
        PaddleOcrVlTokenSequence horizontal = Tokenizer.Value.EncodePrompt(PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(700, 1000)));
        PaddleOcrVlTokenSequence vertical = Tokenizer.Value.EncodePrompt(PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(1000, 700)));
        PaddleOcrVlPositionPlan a = PaddleOcrVlPositionPlan.Create(horizontal), b = PaddleOcrVlPositionPlan.Create(vertical);
        long[] beforeA = a.CopyPositionIds(), beforeB = b.CopyPositionIds();
        Assert.IsFalse(beforeA.SequenceEqual(beforeB), "Height and width coordinates must retain their orientation.");
        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            PaddleOcrVlPositionPlan plan = i % 2 == 0 ? a : b;
            var destination = new long[3];
            plan.WriteDecodePositionIds(plan.PromptLength + i, 1, destination);
            Assert.IsTrue(destination.All(v => v == plan.PromptLength + plan.RopeDelta + i));
        })));
        CollectionAssert.AreEqual(beforeA, a.CopyPositionIds());
        CollectionAssert.AreEqual(beforeB, b.CopyPositionIds());
    }
}
