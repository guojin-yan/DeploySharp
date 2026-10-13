using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// A deliberately small lifecycle probe for the FormulaNet-L CUDA provider.
/// It is separate from the bounded parity test because it keeps one CPU Session
/// and one CUDA Session alive for all samples and inspects the raw token tensor
/// before attempting to decode it. This makes provider-level contamination
/// distinguishable from decoder failures.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFormulaCudaLifecycleIntegrationTests
{
    private const string DefaultModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string DefaultSourceRoot = @"E:\Model\PaddleDocument\source";
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void FormulaNetLRecordsSingleSessionCudaLifecycle()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE=1 and restore with DeploySharpFormulaOrtCuda=true to run the FormulaNet-L lifecycle probe.");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("The opt-in FormulaNet-L CUDA lifecycle probe currently uses the Windows CUDA package.");

        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_MODEL_ROOT") ?? DefaultModelRoot);
        string sourceRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_SOURCE_ROOT") ?? DefaultSourceRoot);
        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ROOT") ?? DefaultDatasetRoot);
        string modelPath = Path.Combine(modelRoot, "pp-formulanet-l.onnx");
        string yamlPath = Path.Combine(sourceRoot, "pp-formulanet-l", "PP-FormulaNet-L_infer", "inference.yml");
        string manifestPath = Path.Combine(datasetRoot, "realFormula-manifest.jsonl");
        if (!File.Exists(modelPath) || !File.Exists(yamlPath) || !File.Exists(manifestPath))
            Assert.Inconclusive("FormulaNet-L lifecycle assets are incomplete. Expected model, tokenizer and manifest under the configured roots.");

        int limit = ReadPositiveInt("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE_LIMIT", 3);
        int offset = ReadNonNegativeInt("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE_OFFSET", 0);
        List<RealFormulaRow> samples = File.ReadLines(manifestPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RealFormulaRow>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .Skip(offset)
            .Take(limit)
            .ToList();
        Assert.IsTrue(samples.Count > 0, "The lifecycle probe requires at least one realFormula sample.");

        IReadOnlyDictionary<string, string> providerOptions = ParseOptionEntries(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OPTIONS"),
            "DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OPTIONS");
        IReadOnlyDictionary<string, string> sessionConfigEntries = ParseOptionEntries(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_SESSION_CONFIG"),
            "DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_SESSION_CONFIG");
        bool preallocateCpuOutputs = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_PREALLOCATE_CPU_OUTPUTS") == "1";

        PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yamlPath);
        int endToken = Find(tokenizer.Tokens, "</s>");
        int startToken = Find(tokenizer.Tokens, "<s>");
        int padToken = Find(tokenizer.Tokens, "<pad>");
        int unknownToken = Find(tokenizer.Tokens, "<unk>");
        Assert.IsTrue(endToken >= 0, "The official FormulaNet-L tokenizer must expose </s>.");
        PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-l");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
            descriptor,
            new PaddleDocumentFormulaSchema(tokenizer, endToken, startToken, padToken, unknownToken),
            modelSize: new VisualSize(768, 768),
            maximumSequenceLength: 4096);

        string reportPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "formula-formulanet-l-cuda-lifecycle.json"));
        string? reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);

        var rows = new List<object>(samples.Count);
        bool cudaRan = false;
        int processId = Process.GetCurrentProcess().Id;
        Exception? sessionException = null;
        var factory = new OpenCvVisualInputFactory();
        var cpuRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var cudaRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cuda");

        try
        {
            // One registry/session pair for the whole probe. Do not move these
            // declarations into the sample loop: the point is to exercise a
            // normal long-lived Session rather than a fresh-process workaround.
            using var cpuRegistry = new BackendRegistry().UseOnnxRuntime(new OnnxRuntimeOptions(executionProvider: OnnxRuntimeExecutionProvider.Cpu));
            using var cudaRegistry = new BackendRegistry().UseOnnxRuntime(new OnnxRuntimeOptions(
                executionProvider: OnnxRuntimeExecutionProvider.Cuda,
                cudaProviderOptions: providerOptions,
                sessionConfigEntries: sessionConfigEntries,
                preferPreallocatedCpuOutputs: preallocateCpuOutputs));
            using IInferenceSession cpuSession = cpuRegistry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), cpuRequest);
            using IInferenceSession cudaSession = cudaRegistry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), cudaRequest);
            const int sessionCreationCount = 1;
            var decoder = (PaddleDocumentFormulaDecoder)profile.VisualProfile.Decoder;

            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                RealFormulaRow sample = samples[sampleIndex];
                string imagePath = Path.Combine(datasetRoot, sample.ImageRelPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(imagePath)) throw new FileNotFoundException("realFormula image is missing", imagePath);

                using PreparedVisualInput input = factory.CreateFromFile(imagePath, profile.VisualProfile, inputId: sample.Image);
                InferenceOutputs cpuOutputs = cpuSession.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                InferenceOutputs cudaOutputs = cudaSession.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                cudaRan = true;
                ITensor cpuRaw = cpuOutputs.GetRequired(decoder.OutputName);
                ITensor cudaRaw = cudaOutputs.GetRequired(decoder.OutputName);
                long[] rawTokens = ReadIntegerSequence(cudaRaw);
                long[] cpuTokens = ReadIntegerSequence(cpuRaw);
                TokenDiagnostics diagnostics = AnalyzeTokens(rawTokens, tokenizer.Tokens.Count, endToken);
                TokenDiagnostics cpuDiagnostics = AnalyzeTokens(cpuTokens, tokenizer.Tokens.Count, endToken);
                double? rawDifference = TensorMaximumAbsDifference(cpuRaw, cudaRaw);

                object? decoderResult = null;
                string? decoderError = null;
                try
                {
                    PaddleDocumentFormulaResult decoded = (PaddleDocumentFormulaResult)decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, cudaOutputs, CancellationToken.None));
                    decoderResult = new
                    {
                        tokenCount = decoded.TokenIds.Count,
                        latexSha256 = FileShaText(decoded.Latex),
                        warningCount = decoded.Warnings.Count,
                        warnings = decoded.Warnings.ToArray()
                    };
                }
                catch (Exception exception)
                {
                    decoderError = exception.ToString();
                }

                rows.Add(new
                {
                    sampleIndex,
                    image = sample.Image,
                    imageSha256 = sample.ImageSha256,
                    processId,
                    sessionCreationCount,
                    cpuRaw = SummarizeIntegerTensor(cpuRaw),
                    cudaRaw = SummarizeIntegerTensor(cudaRaw),
                    cpuTokenDiagnostics = cpuDiagnostics,
                    cudaTokenDiagnostics = diagnostics,
                    tokenIdsMatch = cpuTokens.SequenceEqual(rawTokens),
                    rawOutputShapeMatch = cpuRaw.Shape.ToArray().SequenceEqual(cudaRaw.Shape.ToArray()),
                    rawMaximumAbsoluteDifference = rawDifference,
                    decoderResult,
                    decoderError
                });
            }
        }
        catch (Exception exception)
        {
            sessionException = exception;
            rows.Add(new
            {
                status = "session-failed",
                processId,
                sessionCreationCount = 1,
                error = exception.GetType().FullName,
                detail = exception.Message,
                exception = exception.ToString()
            });
        }

        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "deploysharp-formula-formulanet-l-cuda-lifecycle-v1",
            generatedAtUtc = DateTimeOffset.UtcNow,
            backend = "onnxruntime-cuda",
            model = "paddle-formula/pp-formulanet-l",
            modelSha256 = FileSha(modelPath),
            tokenizerSha256 = FileSha(yamlPath),
            processId,
            sessionCreationCount = 1,
            sampleOffset = offset,
            requestedSampleCount = limit,
            evaluatedSampleCount = rows.Count(row => row.GetType().GetProperty("sampleIndex") is not null),
            cudaProviderOptions = providerOptions,
            sessionConfigEntries,
            preferPreallocatedCpuOutputs = preallocateCpuOutputs,
            samples = rows,
            sessionException = sessionException?.ToString(),
            boundary = "One CPU Session and one CUDA Session are reused for three consecutive realFormula samples in one process. Raw Int64 outputs are recorded before decoding. This is a lifecycle contamination probe, not a formula accuracy or release-quality result. A pass here does not promote FormulaNet-L to ordinary CUDA support without parity and quality evidence."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(reportPath);

        if (!cudaRan)
            Assert.Inconclusive("The CUDA Session did not produce an output; see the lifecycle report: " + reportPath);
        Assert.IsTrue(rows.Any(row => row.GetType().GetProperty("sampleIndex") is not null), "No lifecycle sample was recorded. See " + reportPath);
    }

    private sealed record RealFormulaRow(string Image, string ImageRelPath, string ImageSha256, string ReferenceLatex);

    private sealed record TokenDiagnostics(
        int Length,
        long Minimum,
        long Maximum,
        int InvalidTokenCount,
        bool ContainsEos,
        int EosIndex,
        long[] FirstTokens);

    private static TokenDiagnostics AnalyzeTokens(long[] values, int vocabularySize, int eosToken)
    {
        int invalid = values.Count(value => value < 0 || value >= vocabularySize);
        int eosIndex = Array.IndexOf(values, (long)eosToken);
        return new TokenDiagnostics(
            values.Length,
            values.Length == 0 ? 0 : values.Min(),
            values.Length == 0 ? 0 : values.Max(),
            invalid,
            eosIndex >= 0,
            eosIndex,
            values.Take(Math.Min(12, values.Length)).ToArray());
    }

    private static int ReadPositiveInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;

    private static int ReadNonNegativeInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value >= 0 ? value : fallback;

    private static IReadOnlyDictionary<string, string> ParseOptionEntries(string? value, string variableName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = item.IndexOf('=');
            if (separator <= 0 || separator == item.Length - 1)
                throw new AssertFailedException(variableName + " entries must use key=value;key=value syntax.");
            result[item[..separator].Trim()] = item[(separator + 1)..].Trim();
        }
        return result;
    }

    private static long[] ReadIntegerSequence(ITensor tensor)
    {
        if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] values64) return values64;
        if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] values32) return values32.Select(value => (long)value).ToArray();
        throw new InvalidOperationException("Formula model output is not an integer token tensor: " + tensor.ElementType);
    }

    private static object SummarizeIntegerTensor(ITensor tensor)
    {
        long[] values = ReadIntegerSequence(tensor);
        return new
        {
            elementType = tensor.ElementType.ToString(),
            shape = tensor.Shape.ToArray(),
            length = values.Length,
            minimum = values.Length == 0 ? 0 : values.Min(),
            maximum = values.Length == 0 ? 0 : values.Max(),
            first = values.Take(Math.Min(12, values.Length)).ToArray()
        };
    }

    private static double? TensorMaximumAbsDifference(ITensor left, ITensor right)
    {
        if (left.ElementType != right.ElementType || !left.Shape.ToArray().SequenceEqual(right.Shape.ToArray())) return null;
        long[] leftValues = ReadIntegerSequence(left);
        long[] rightValues = ReadIntegerSequence(right);
        long maximum = 0;
        for (int index = 0; index < Math.Min(leftValues.Length, rightValues.Length); index++)
            maximum = Math.Max(maximum, Math.Abs(leftValues[index] - rightValues[index]));
        return maximum;
    }

    private static int Find(IReadOnlyList<string> values, string value)
    {
        for (int index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        return -1;
    }

    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string FileShaText(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
