using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

/// <summary>Replays pinned real CTC windows to isolate seam quality/cost from model/runtime noise. / 回放固定真实 CTC 窗口，独立验证接缝质量与成本。</summary>
[TestClass]
[DoNotParallelize]
public sealed class OcrRecognitionWindowTraceReplayTests
{
    private const string TraceSha = "12742dac110f98831fefbda2ade8751e3cd37cb1b4ba6fcc4a98c0a3bea882b4";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void RealNaturalWindowTracesMeasureSeamQualityAndMergeOnlyCost()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_RUN") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_RUN=1 for pinned natural-wide CTC replay.");
        string repo = FindRepo();
        string tracePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_TRACE")
            ?? Path.Combine(repo, "artifacts/paddleocr-natural-wide-gt-20261023-final/paddleocr-hiertext-natural-wide-gt-20261023072131.local-traces.json");
        string candidatePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_CANDIDATES")
            ?? Path.Combine(repo, "tests/assets/paddleocr/natural-wide-selection.json");
        string baselinePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_BASELINE")
            ?? Path.Combine(repo, "tests/assets/paddleocr/natural-wide-baseline.json");
        string traceSha = HashFile(tracePath);
        Assert.AreEqual(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_TRACE_SHA") ?? TraceSha, traceSha);
        Assert.AreEqual(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_CANDIDATES_SHA")
            ?? "80e9fb310b775644b0e9022e19b99608afce26acf97c160aa441175bb131f50d", HashFile(candidatePath));
        string phase = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_PHASE") ?? "candidate";
        if (phase is not ("baseline" or "candidate")) throw new ArgumentException("Replay phase must be baseline or candidate.");

        var profile = new TextCropProfile("tests/natural-window-replay", 48, OcrRecognitionWidthMode.Dynamic, 320, 3200, minimumWidth: 48)
            .WithRecognitionWindows(new OcrRecognitionWindowOptions(.2, 32));
        using JsonDocument baselineJson = JsonDocument.Parse(File.ReadAllText(baselinePath));
        Dictionary<string, string> strata = baselineJson.RootElement.GetProperty("dataset").GetProperty("lines").EnumerateArray()
            .ToDictionary(row => row.GetProperty("Key").GetString()!, row => row.GetProperty("qualityStratum").GetString()!, StringComparer.Ordinal);
        using JsonDocument candidateJson = JsonDocument.Parse(File.ReadAllText(candidatePath));
        JsonElement[] candidates = candidateJson.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        using JsonDocument traceJson = JsonDocument.Parse(File.ReadAllText(tracePath));
        var cases = new List<ReplayCase>();
        foreach (JsonElement row in traceJson.RootElement.EnumerateArray())
        {
            if (row.GetProperty("mode").GetString() != "SlidingWindow") continue;
            string key = row.GetProperty("Key").GetString()!;
            int candidateIndex = Array.FindIndex(candidates, candidate => candidate.GetProperty("imageId").GetString() + "/"
                + candidate.GetProperty("paragraphIndex").GetInt32() + "/" + candidate.GetProperty("lineIndex").GetInt32() == key);
            Assert.IsTrue(candidateIndex >= 0);
            JsonElement candidate = candidates[candidateIndex];
            float[] points = candidate.GetProperty("vertices").EnumerateArray().Select(value => value.GetSingle()).ToArray();
            var quad = new TextQuadrilateral(new PointF(points[0], points[1]), new PointF(points[2], points[3]),
                new PointF(points[4], points[5]), new PointF(points[6], points[7]), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(candidateIndex, 1, quad.Polygon, quad);
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            var raw = new List<OcrRecognitionWindowResult>();
            foreach (JsonElement window in row.GetProperty("windows").EnumerateArray())
            {
                int index = window.GetProperty("Index").GetInt32();
                Assert.AreEqual(plans[index].Start, window.GetProperty("Start").GetDouble());
                Assert.AreEqual(plans[index].End, window.GetProperty("End").GetDouble());
                var tokens = window.GetProperty("Tokens").EnumerateArray().Select(token => new OcrToken(
                    token.GetProperty("Timestep").GetInt32(), token.GetProperty("ClassIndex").GetInt32(), token.GetProperty("Confidence").GetSingle(),
                    token.GetProperty("Text").GetString(), token.GetProperty("IsBlank").GetBoolean(), token.GetProperty("IsCollapsedRepeat").GetBoolean(),
                    token.GetProperty("IsUnknown").GetBoolean(), token.GetProperty("Emitted").GetBoolean())).ToArray();
                OcrToken[] emitted = tokens.Where(token => token.Emitted).ToArray();
                float confidence = emitted.Length == 0 ? 0 : (float)(emitted.Sum(token => (double)token.Confidence) / emitted.Length);
                var recognition = new RecognizedText(candidateIndex, window.GetProperty("Text").GetString()!, confidence, tokens,
                    "natural-wide-replay", "v6-small-medium", "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6");
                Assert.AreEqual(plans[index].Crop.WidthInfo.TargetWidth, window.GetProperty("Width").GetProperty("TensorWidth").GetInt32());
                raw.Add(new OcrRecognitionWindowResult(plans[index], recognition, plans[index].Crop.WidthInfo));
            }
            Assert.AreEqual(plans.Count, raw.Count);
            string stratum = strata[key];
            cases.Add(new ReplayCase(row.GetProperty("backend").GetString()!, row.GetProperty("model").GetString()!, key, stratum,
                row.GetProperty("expected").GetString()!, row.GetProperty("recognized").GetString()!, region, raw.ToArray()));
        }
        Assert.AreEqual(int.TryParse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_CASES"), out int expectedCases)
            ? expectedCases : 52, cases.Count);

        OcrRegionResult[] results = Sweep(cases, profile);
        var qualityRows = new List<object>();
        var metrics = new List<(ReplayCase Case, OcrTextAccuracyMetrics Before, OcrTextAccuracyMetrics After)>();
        for (int index = 0; index < cases.Count; index++)
        {
            ReplayCase item = cases[index]; OcrRegionResult merged = results[index];
            if (phase == "baseline") Assert.AreEqual(item.Baseline, merged.Recognition.Text, item.Key);
            Assert.AreEqual(item.Region.SourceIndex, merged.Recognition.SourceRegionIndex);
            var text = new StringBuilder();
            int traceIndex = 0;
            foreach (OcrRecognitionWindowResult window in merged.RecognitionWindows)
            {
                int ordinal = 0;
                foreach (OcrToken original in window.Recognition.Tokens)
                {
                    bool emitted = original.Emitted && ordinal++ >= window.RemovedPrefixTokens;
                    OcrToken token = merged.Recognition.Tokens[traceIndex];
                    Assert.AreEqual(traceIndex++, token.Timestep);
                    Assert.AreEqual(original.ClassIndex, token.ClassIndex); Assert.AreEqual(original.Confidence, token.Confidence);
                    Assert.AreEqual(original.Text, token.Text); Assert.AreEqual(emitted, token.Emitted);
                    Assert.AreEqual(original.IsBlank, token.IsBlank); Assert.AreEqual(original.IsCollapsedRepeat, token.IsCollapsedRepeat);
                    Assert.AreEqual(original.IsUnknown, token.IsUnknown);
                    if (emitted) text.Append(token.Text);
                }
            }
            Assert.AreEqual(merged.Recognition.Text, text.ToString());
            OcrTextAccuracyMetrics before = OcrTextAccuracy.Compare(item.Expected, item.Baseline);
            OcrTextAccuracyMetrics after = OcrTextAccuracy.Compare(item.Expected, merged.Recognition.Text);
            metrics.Add((item, before, after));
            qualityRows.Add(new { item.Backend, item.Model, item.Key, item.Stratum, before, after,
                beforeTextSha256 = Hash(item.Baseline), afterTextSha256 = Hash(merged.Recognition.Text),
                windows = merged.RecognitionWindows.Select(window => new { window.Index, window.RemovedPrefixTokens, window.SeamUncertain, window.OverlapEditDistance }).ToArray() });
        }
        string fingerprint = Fingerprint(results);
        for (int index = 0; index < 5; index++) Assert.AreEqual(fingerprint, Fingerprint(Sweep(cases, profile)));
        var samples = new List<object>(); var times = new List<double>(); var allocations = new List<long>();
        for (int index = 0; index < 50; index++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            OcrRegionResult[] measured = Sweep(cases, profile);
            double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.AreEqual(fingerprint, Fingerprint(measured));
            times.Add(milliseconds); allocations.Add(allocated);
            samples.Add(new { index, milliseconds, allocatedBytes = allocated });
        }
        var aggregate = metrics.GroupBy(row => row.Case.Backend + "/" + row.Case.Model + "/" + row.Case.Stratum).Select(group => new
        {
            key = group.Key, lines = group.Count(),
            referenceCharacters = group.Sum(row => row.Before.ReferenceCharacters), referenceWords = group.Sum(row => row.Before.ReferenceWords),
            beforeCharacterEdits = group.Sum(row => row.Before.CharacterEditDistance), afterCharacterEdits = group.Sum(row => row.After.CharacterEditDistance),
            beforeWordEdits = group.Sum(row => row.Before.WordEditDistance), afterWordEdits = group.Sum(row => row.After.WordEditDistance),
            cerBetter = group.Count(row => row.After.CharacterEditDistance < row.Before.CharacterEditDistance),
            cerWorse = group.Count(row => row.After.CharacterEditDistance > row.Before.CharacterEditDistance),
            cerEqual = group.Count(row => row.After.CharacterEditDistance == row.Before.CharacterEditDistance)
        }).ToArray();
        string output = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(output);
        string reportPath = Path.GetFullPath(Path.Combine(output, "paddleocr-window-seam-replay-" + phase + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json"));
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, phase,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION") ?? "unknown",
            visualAssemblySha256 = HashFile(typeof(OcrPipeline).Assembly.Location), testAssemblySha256 = HashFile(GetType().Assembly.Location),
            environment = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription, logicalProcessors = Environment.ProcessorCount },
            inputs = new { traceSha256 = traceSha, candidateSha256 = HashFile(candidatePath), baselineReportSha256 = HashFile(baselinePath), cases = cases.Count, mainQualityCases = cases.Count(item => item.Stratum == "legible-height-at-least-2px") },
            protocol = new { path = "OcrRecognitionWindowMerger.Merge only; preloaded real raw CTC windows", maximumWidth = 3200, targetHeight = 48,
                overlap = .2, maximumOverlapTokens = 64, maximumOverlapEditDistance = 2, warmupSweeps = 5, measuredSweeps = 50, linesPerSweep = cases.Count,
                metrics = "OcrTextAccuracy.Compare: ordinal Unicode-scalar CER and whitespace-token WER, no implicit normalization",
                allocations = "GC.GetAllocatedBytesForCurrentThread during Sweep; includes result array and merge results; excludes fingerprint, metrics and JSON",
                timing = "Sequential merge-only sweeps; no inference/preprocessing/CTC decode/native GPU work. CPU frequency not controlled; phase order is baseline then candidate, not randomized paired trials." },
            cost = new { p50Ms = Percentile(times, .5), p95Ms = Percentile(times, .95), meanMs = times.Average(), minAllocatedBytes = allocations.Min(), maxAllocatedBytes = allocations.Max() },
            fingerprint, aggregate, rows = qualityRows, samples,
            boundary = "Same pinned raw tokens isolate a seam-code change. This replay is not an additional dataset or native backend rerun, a full OCR latency benchmark, or proof of general accuracy. Real pipeline/other-backend reruns and repeated-number/single-character safety remain separate gates."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(reportPath);
        TestContext.WriteLine("SEAM_REPLAY_REPORT=" + reportPath);
    }

    private static OcrRegionResult[] Sweep(IReadOnlyList<ReplayCase> cases, TextCropProfile profile)
    {
        var result = new OcrRegionResult[cases.Count];
        for (int index = 0; index < cases.Count; index++) result[index] = OcrRecognitionWindowMerger.Merge(cases[index].Region, profile, cases[index].Windows);
        return result;
    }
    private static string Fingerprint(IEnumerable<OcrRegionResult> results) => Hash(JsonSerializer.Serialize(results.Select(row => new { row.Recognition.Text,
        windows = row.RecognitionWindows.Select(window => new { window.RemovedPrefixTokens, window.SeamUncertain, window.OverlapEditDistance }).ToArray() }).ToArray()));
    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        double[] ordered = values.OrderBy(value => value).ToArray(); double position = (ordered.Length - 1) * fraction;
        int lower = (int)position, upper = Math.Min(lower + 1, ordered.Length - 1); return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string HashFile(string path) { using FileStream stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string FindRepo()
        => RepositoryTestFiles.Root;
    private sealed record ReplayCase(string Backend, string Model, string Key, string Stratum, string Expected, string Baseline, TextRegion Region, OcrRecognitionWindowResult[] Windows);
}
