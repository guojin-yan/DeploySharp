using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly string[] AllKinds = { "clip", "sam", "blip" };
    private static readonly string[] AllBackends = { "onnxruntime", "onnxruntime-cuda", "openvino", "opencv-dnn", "tensorrt", "tensorrt-cuda" };

    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.Help) { PrintUsage(); return 0; }
            var rows = new List<ResultRow>();
            foreach (string kind in options.Kinds)
                foreach (string backend in options.Backends)
                    rows.Add(Run(kind, backend, options));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
            using (var writer = new StreamWriter(options.Output, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(ResultRow.Header);
                foreach (ResultRow row in rows) writer.WriteLine(row.ToCsv());
            }
            Console.WriteLine("DEPLOYSHARP_SPECIAL_VISUAL_REPORT=" + Path.GetFullPath(options.Output));
            Console.WriteLine("DEPLOYSHARP_SPECIAL_VISUAL_ROWS=" + rows.Count.ToString(Invariant));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("DEPLOYSHARP_SPECIAL_VISUAL_FATAL=" + Detail(exception));
            return 2;
        }
    }

    private static ResultRow Run(string kind, string backend, Options options)
    {
        if (backend is "opencv-dnn" or "tensorrt" or "tensorrt-cuda")
        {
            string reason = backend == "opencv-dnn"
                ? "The complete pipeline contains integer or non-image multi-input subgraphs that the OpenCV DNN v1 contract does not admit."
                : "The exact public multi-artifact profile is ONNX-bound and cannot substitute TensorRT engine artifacts without changing its audited identity contract.";
            return ResultRow.Unsupported(kind, backend, reason);
        }

        try
        {
            using BackendRegistry registry = CreateRegistry(backend, out BackendRequest request);
            IReadOnlyList<Measurement> measurements = kind switch
            {
                "clip" => RunClip(registry, request, options),
                "sam" => RunSam(registry, request, options),
                "blip" => RunBlip(registry, request, options),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            return ResultRow.Pass(kind, backend, Measurement.Average(measurements));
        }
        catch (Exception exception)
        {
            string detail = Detail(exception);
            string status = detail.Contains("unsupported", StringComparison.OrdinalIgnoreCase) || detail.Contains("not compatible", StringComparison.OrdinalIgnoreCase) ? "unsupported" : "unavailable";
            return ResultRow.Error(kind, backend, status, detail);
        }
    }

    private static BackendRegistry CreateRegistry(string backend, out BackendRequest request)
    {
        var registry = new BackendRegistry();
        if (backend == "openvino")
        {
            registry.UseOpenVino();
            request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            return registry;
        }

        OnnxRuntimeExecutionProvider provider = backend == "onnxruntime-cuda" ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu;
        registry.UseOnnxRuntime(new OnnxRuntimeOptions(executionProvider: provider, cudaDeviceId: 0));
        request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, provider == OnnxRuntimeExecutionProvider.Cuda ? "cuda" : "cpu");
        return registry;
    }

    private static IReadOnlyList<Measurement> RunClip(BackendRegistry registry, BackendRequest request, Options options)
    {
        string root = RequiredDirectory(options.ModelRoot, "clip", "clip-vit-base-patch32");
        string imageEncoder = Required(root, "image-encoder.onnx", "clip-image-encoder-opset17.onnx");
        string textEncoder = Required(root, "text-encoder.onnx", "clip-text-encoder-opset17.onnx");
        VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
        var bundle = new VisionLanguageArtifactBundle(profile,
            profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageEncoder, request.BackendId),
            profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textEncoder, request.BackendId));
        using var session = new VisionLanguageEmbeddingSession(registry, bundle, request);
        TextTokenBatch prompts = ClipPrompts(profile);

        Measurement One()
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch stage = Stopwatch.StartNew();
            using PreparedVisualInput input = new OpenCvVisionLanguageInputFactory().CreateFromFile(options.Image, profile);
            double preprocess = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            VisionLanguageImageEmbedding image = session.EncodeImage(input);
            double primary = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            VisionLanguageTextEmbedding text = session.EncodeText(prompts);
            double secondary = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            VisionLanguageClassificationResult classification = VisionLanguageScorer.Classify(profile, image, text, new[] { new ZeroShotLabelPrompt("bus", new[] { 0 }), new ZeroShotLabelPrompt("person", new[] { 1 }), new ZeroShotLabelPrompt("dog", new[] { 2 }) });
            string fingerprint = Hash(image.Sha256 + "|" + text.Sha256 + "|" + classification.Classification.TopPrediction?.Label);
            double postprocess = stage.Elapsed.TotalMilliseconds;
            total.Stop();
            return new Measurement(preprocess, primary, secondary, postprocess, total.Elapsed.TotalMilliseconds, fingerprint, 0, 0);
        }

        return Measure(options, One);
    }

    private static IReadOnlyList<Measurement> RunSam(BackendRegistry registry, BackendRequest request, Options options)
    {
        string root = RequiredDirectory(options.ModelRoot, "sam-v1-vit-b");
        string encoder = Required(root, "image-encoder.onnx", "sam_vit_b_image_encoder_opset17.onnx");
        string decoder = Required(root, "prompt-mask-decoder.onnx", "sam_vit_b_prompt_mask_decoder_opset17_legacy.onnx");
        PromptableSegmentationProfile profile = PromptableSegmentationProfiles.CreateSamV1(
            "benchmark/sam-v1-vit-b", new ModelId("external/sam-v1-vit-b-encoder"), new ModelId("external/sam-v1-vit-b-prompt-mask-decoder"),
            "95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a", "b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a",
            "dca509fe793f601edb92606367a655c15ac00fdf", "torch-2.9.1+cpu;opset17", "official-export;torch-2.9.1+cpu;opset17");
        var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
        {
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoder, request.BackendId)),
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoder, request.BackendId))
        });
        using var session = new PromptableSegmentationImageSession(registry, bundle, request);

        Measurement One()
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch stage = Stopwatch.StartNew();
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(options.SamImage);
            double preprocess = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            PromptableImageEmbedding embedding = session.SetImage(input);
            double primary = stage.Elapsed.TotalMilliseconds;
            int width = input.SourceSize.Width; int height = input.SourceSize.Height;
            var points = new[] { new PromptPoint(width * .5f, height * .5f, PromptPointLabel.Foreground), new PromptPoint(width * .25f, height * .25f, PromptPointLabel.Background) };
            var box = new RectangleF(width * .2f, height * .1f, width * .6f, height * .8f);
            stage.Restart();
            PromptableSegmentationResult first = session.Predict(new PromptableSegmentationPrompt(points, box, returnMultipleMasks: true, promptId: "benchmark"));
            PromptableMaskFeedback feedback = first.Candidates[0].LowResolutionLogits.CreateFeedback();
            PromptableSegmentationResult refined = session.Predict(new PromptableSegmentationPrompt(points, box, feedback, returnMultipleMasks: false, promptId: "benchmark-refined"));
            double secondary = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            string fingerprint = Hash(embedding.Summaries[0].Sha256 + "|" + refined.Candidates[0].Quality.ToString("R", Invariant) + "|" + refined.Segmentation.Instances[0].Mask.ForegroundPixelCount.ToString(Invariant));
            double postprocess = stage.Elapsed.TotalMilliseconds;
            total.Stop();
            return new Measurement(preprocess, primary, secondary, postprocess, total.Elapsed.TotalMilliseconds, fingerprint, 0, 0);
        }

        return Measure(options, One);
    }

    private static IReadOnlyList<Measurement> RunBlip(BackendRegistry registry, BackendRequest request, Options options)
    {
        string root = RequiredDirectory(options.ModelRoot, "blip-caption-base");
        string vision = Required(root, "vision-encoder.onnx", Path.Combine("converted-opset17", "vision_encoder.onnx"));
        string decoder = Required(root, "language-decoder.onnx", Path.Combine("converted-opset17", "text_decoder_full_prefix.onnx"));
        string vocabulary = Required(root, "vocab.txt", "bert-base-uncased-vocab.txt");
        GenerativeVisionLanguageProfile profile = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
        var tokenizer = new BlipBertTokenizer(vocabulary, profile.Tokenizer);
        var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
        {
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, request.BackendId!.Value)),
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoder, request.BackendId.Value))
        });
        using var session = new GenerativeVisionLanguageSession(registry, bundle, request);

        Measurement One()
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch stage = Stopwatch.StartNew();
            using PreparedVisualInput input = new OpenCvGenerativeVisionLanguageInputFactory().CreateFromFile(options.Image, profile);
            double preprocess = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            GenerativeVisionLanguageImageState state = session.SetImage(input);
            double primary = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            GenerativeVisionLanguageResult result = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
            double secondary = stage.Elapsed.TotalMilliseconds;
            stage.Restart();
            string fingerprint = Hash(state.ValueSha256 + "|" + result.Generation.Text + "|" + result.Identity.Identity);
            double postprocess = stage.Elapsed.TotalMilliseconds;
            total.Stop();
            return new Measurement(preprocess, primary, secondary, postprocess, total.Elapsed.TotalMilliseconds, fingerprint, 0, 0);
        }

        return Measure(options, One);
    }

    private static IReadOnlyList<Measurement> Measure(Options options, Func<Measurement> measure)
    {
        for (int index = 0; index < options.Warmup; index++) _ = measure();
        var values = new List<Measurement>(options.Iterations);
        for (int index = 0; index < options.Iterations; index++) values.Add(measure());
        return values;
    }

    private static TextTokenBatch ClipPrompts(VisionLanguageEmbeddingProfile profile)
    {
        int[][] rows = { new[] { 49406, 320, 1125, 539, 320, 2840, 49407 }, new[] { 49406, 320, 1125, 539, 320, 2533, 49407 }, new[] { 49406, 320, 1125, 539, 320, 1929, 49407 } };
        long[] ids = Enumerable.Repeat(49407L, 3 * 77).ToArray(); long[] mask = new long[ids.Length];
        for (int row = 0; row < rows.Length; row++) for (int column = 0; column < rows[row].Length; column++) { ids[(row * 77) + column] = rows[row][column]; mask[(row * 77) + column] = 1; }
        return new TextTokenBatch(new[] { "a photo of a bus", "a photo of a person", "a photo of a dog" }, ids, 3, 77, profile.Tokenizer.TokenizerId, profile.Tokenizer.Sha256, mask);
    }

    private static string Required(string root, params string[] relativeCandidates)
    {
        foreach (string relative in relativeCandidates)
        {
            string path = Path.Combine(root, relative);
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        throw new FileNotFoundException("Required model asset was not found under " + root + ": " + string.Join(", ", relativeCandidates));
    }

    private static string RequiredDirectory(string root, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            string path = Path.Combine(root, candidate);
            if (Directory.Exists(path)) return Path.GetFullPath(path);
        }
        throw new DirectoryNotFoundException("Required model directory was not found under " + root + ": " + string.Join(", ", candidates));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Detail(Exception exception) => exception.GetType().Name + ": " + exception.Message.Replace('\r', ' ').Replace('\n', ' ');
    private static string Device(string backend) => backend == "openvino" ? "CPU" : backend.Contains("cuda", StringComparison.Ordinal) || backend == "tensorrt" ? "cuda" : "cpu";

    private static void PrintUsage() => Console.WriteLine("Usage: DeploySharp.SpecialVisualBenchmark --model-root <path> --image <path> [--sam-image <path>] [--kind all|clip,sam,blip] [--backend all|list] [--warmup N] [--iterations N] [--output path]");

    private readonly record struct Measurement(double Preprocess, double PrimaryInference, double SecondaryInference, double Postprocess, double Total, string Fingerprint, double TotalP50, double TotalP95)
    {
        public static Measurement Average(IReadOnlyList<Measurement> values)
        {
            string[] hashes = values.Select(value => value.Fingerprint).Distinct(StringComparer.Ordinal).ToArray();
            if (hashes.Length != 1) throw new InvalidOperationException("The complete pipeline result changed between measured iterations.");
            double[] totals = values.Select(value => value.Total).OrderBy(value => value).ToArray();
            return new Measurement(values.Average(value => value.Preprocess), values.Average(value => value.PrimaryInference), values.Average(value => value.SecondaryInference), values.Average(value => value.Postprocess), values.Average(value => value.Total), hashes[0], Percentile(totals, .5), Percentile(totals, .95));
        }

        private static double Percentile(double[] ordered, double percentile)
        {
            if (ordered.Length == 0) return 0;
            double position = (ordered.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return ordered[lower];
            double fraction = position - lower;
            return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
        }
    }

    private sealed record ResultRow(string Kind, string Backend, string Status, Measurement? Measurement, string DetailText)
    {
        public const string Header = "kind,backend,device,status,preprocess_ms,primary_inference_ms,secondary_inference_ms,postprocess_ms,total_ms,total_p50_ms,total_p95_ms,result_sha256,detail";
        public static ResultRow Pass(string kind, string backend, Measurement value) { var row = new ResultRow(kind, backend, "pass", value, "complete multi-artifact pipeline; model loading excluded"); Console.WriteLine(row.ToLog()); return row; }
        public static ResultRow Unsupported(string kind, string backend, string detail) { var row = new ResultRow(kind, backend, "unsupported", null, detail); Console.WriteLine(row.ToLog()); return row; }
        public static ResultRow Error(string kind, string backend, string status, string detail) { var row = new ResultRow(kind, backend, status, null, detail); Console.WriteLine(row.ToLog()); return row; }
        public string ToLog() => "DEPLOYSHARP_SPECIAL_VISUAL kind=" + Kind + ";backend=" + Backend + ";status=" + Status + (Measurement.HasValue ? ";totalMs=" + Measurement.Value.Total.ToString("F3", Invariant) + ";totalP50Ms=" + Measurement.Value.TotalP50.ToString("F3", Invariant) + ";totalP95Ms=" + Measurement.Value.TotalP95.ToString("F3", Invariant) + ";resultSha256=" + Measurement.Value.Fingerprint : ";detail=" + DetailText);
        public string ToCsv()
        {
            Measurement value = Measurement.GetValueOrDefault();
            string[] timing = Measurement.HasValue ? new[] { N(value.Preprocess), N(value.PrimaryInference), N(value.SecondaryInference), N(value.Postprocess), N(value.Total), N(value.TotalP50), N(value.TotalP95), Csv(value.Fingerprint) } : new[] { "", "", "", "", "", "", "", "" };
            return string.Join(",", new[] { Csv(Kind), Csv(Backend), Csv(Device(Backend)), Csv(Status) }.Concat(timing).Concat(new[] { Csv(DetailText) }));
        }
        private static string N(double value) => value.ToString("F3", Invariant);
        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private sealed record Options(IReadOnlyList<string> Kinds, IReadOnlyList<string> Backends, string ModelRoot, string Image, string SamImage, int Warmup, int Iterations, string Output, bool Help)
    {
        public static Options Parse(string[] args)
        {
            string kinds = "all", backends = "all";
            string? modelRoot = null, image = null, samImage = null;
            int warmup = 3, iterations = 10; bool help = false;
            string output = Path.Combine("artifacts", "local-model-benchmarks", "special-visual-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", Invariant) + ".csv");
            for (int index = 0; index < args.Length; index++)
            {
                string value = args[index];
                if (value is "--help" or "-h") { help = true; continue; }
                string Next() => ++index < args.Length ? args[index] : throw new ArgumentException("Missing value for " + value);
                if (value == "--kind") kinds = Next(); else if (value == "--backend") backends = Next(); else if (value == "--model-root") modelRoot = Next(); else if (value == "--image") image = Next(); else if (value == "--sam-image") samImage = Next(); else if (value == "--warmup") warmup = int.Parse(Next(), Invariant); else if (value == "--iterations") iterations = int.Parse(Next(), Invariant); else if (value == "--output") output = Next(); else throw new ArgumentException("Unknown option: " + value);
            }
            if (help) return new Options(Array.Empty<string>(), Array.Empty<string>(), "", "", "", warmup, iterations, output, true);
            if (string.IsNullOrWhiteSpace(modelRoot) || string.IsNullOrWhiteSpace(image)) throw new ArgumentException("--model-root and --image are required.");
            if (warmup < 0 || iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations));
            return new Options(Select(kinds, AllKinds), Select(backends, AllBackends), Path.GetFullPath(modelRoot), Path.GetFullPath(image), Path.GetFullPath(samImage ?? image), warmup, iterations, Path.GetFullPath(output), false);
        }

        private static IReadOnlyList<string> Select(string value, IReadOnlyList<string> allowed)
        {
            if (value.Equals("all", StringComparison.OrdinalIgnoreCase)) return allowed.ToArray();
            string[] selected = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (selected.Length == 0 || selected.Any(item => !allowed.Contains(item, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Unsupported selection: " + value);
            return selected.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
