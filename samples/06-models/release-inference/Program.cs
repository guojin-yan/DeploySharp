using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private const string ReleaseTag = "models-visual.1";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.ShowHelp)
            {
                Options.PrintUsage();
                return 0;
            }

            if (!File.Exists(options.ImagePath)) throw new FileNotFoundException("The input image does not exist.", options.ImagePath);
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            var factoryOptions = new ModelFactoryOptions(options.CacheRoot, offline: options.Offline, requestTimeout: TimeSpan.FromMinutes(10), maximumRetries: 3);
            using var factory = new ModelFactoryClient(catalog, factoryOptions);
            ModelSelection selection = factory.Select(new ModelQuery(
                modelId: options.ModelId,
                backend: "onnxruntime",
                format: "onnx",
                precision: options.Precision,
                quantization: options.Quantization,
                includePreview: true));

            Console.WriteLine("selected=" + selection.Entry.ModelId + ";artifact=" + selection.Artifact.ArtifactId);
            var progress = new Progress<ModelDownloadProgress>(ReportProgress);
            MaterializedModel materialized = await factory.GetModelAsync(selection, progress).ConfigureAwait(false);
            ModelArtifact artifact = materialized.Package.ToCoreArtifacts().Single();
            Console.WriteLine("model=" + artifact.Location + ";cache=" + materialized.PackageRoot);

            if (string.Equals(options.ModelId, "anomalib/padim/mvtec-bottle", StringComparison.OrdinalIgnoreCase))
            {
                return RunPadim(options, artifact);
            }

            return RunRmbg(options, artifact);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("DEPLOYSHARP_MODEL_RELEASE_INFERENCE_ERROR: " + exception.Message);
            return 2;
        }
    }

    private static int RunRmbg(Options options, ModelArtifact artifact)
    {
        BriaRmbgProfile profile;
        if (string.Equals(options.ModelId, "bria/rmbg-1.4", StringComparison.OrdinalIgnoreCase))
        {
            profile = BriaRmbgProfiles.CreateRmbg14(
                new ModelId(options.ModelId),
                new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", artifact.Sha256!, "2ceba5a5", "pytorch-2.1.0-opset11", "LicenseRef-BRIA-RMBG-1.4"));
        }
        else if (string.Equals(options.ModelId, "bria/rmbg-2.0", StringComparison.OrdinalIgnoreCase))
        {
            profile = BriaRmbgProfiles.CreateRmbg20(
                new ModelId(options.ModelId),
                new BriaRmbgProfileOptions(14, new VisualSize(1024, 1024), "pixel_values", "alphas", artifact.Sha256!, "5df4c9c7", "local-exporter-unverified-opset14", "LicenseRef-BRIA-RMBG-2.0"));
        }
        else
        {
            throw new ArgumentException("This sample supports bria/rmbg-1.4, bria/rmbg-2.0, and anomalib/padim/mvtec-bottle.");
        }

        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ForegroundMatting), request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            options.ImagePath,
            profile.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
        BackgroundRemovalResult result = pipeline.Run(input).GetValue<BackgroundRemovalResult>();
        string output = options.OutputPath ?? Path.Combine(Environment.CurrentDirectory, "deploysharp-alpha.pgm");
        WritePgm(output, result.Alpha.Width, result.Alpha.Height, result.Alpha.ToArray());
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "result=alpha;size={0}x{1};sha256={2};output={3}", result.Alpha.Width, result.Alpha.Height, result.Alpha.ComputeSha256(), output));
        return 0;
    }

    private static int RunPadim(Options options, ModelArtifact artifact)
    {
        var profile = AnomalibProfiles.CreatePadim(
            new ModelId(options.ModelId),
            new AnomalibArtifactContract(14, artifact.Sha256!, "ffde4cce3db38964f9cf627b524dd325401c6107", "pytorch-2.7.1-opset14"));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new AnomalyPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.AnomalyDetection), request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            options.ImagePath,
            profile.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreateAnomalibOptions(profile));
        AnomalyDetectionResult result = pipeline.Run(input);
        string output = options.OutputPath ?? Path.Combine(Environment.CurrentDirectory, "deploysharp-anomaly-mask.pgm");
        WritePgm(output, result.Mask.Width, result.Mask.Height, result.Mask.ToArray());
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "result=anomaly;score={0:R};anomalous-pixel-ratio={1:R};sha256={2};output={3}", result.ImageScore, result.AnomalousPixelRatio, result.ComputeSha256(), output));
        return 0;
    }

    private static void ReportProgress(ModelDownloadProgress value)
    {
        if (value.Stage != ModelDownloadStage.Downloading || value.TotalBytes <= 0 || value.ReceivedBytes == 0 || value.ReceivedBytes == value.TotalBytes)
        {
            Console.WriteLine("progress=" + value.AssetId + ";stage=" + value.Stage + ";bytes=" + value.ReceivedBytes + "/" + (value.TotalBytes > 0 ? value.TotalBytes.ToString(CultureInfo.InvariantCulture) : "?"));
        }
    }

    private static void WritePgm(string path, int width, int height, float[] values)
    {
        if (values.Length != checked(width * height)) throw new ArgumentException("Output dimensions do not match the value count.");
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true);
        writer.WriteLine("P5");
        writer.WriteLine(width.ToString(CultureInfo.InvariantCulture) + " " + height.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("255");
        writer.Flush();
        var bytes = new byte[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            float value = Math.Max(0f, Math.Min(1f, values[index]));
            bytes[index] = (byte)Math.Round(value * 255f, MidpointRounding.ToEven);
        }
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WritePgm(string path, int width, int height, byte[] values)
    {
        if (values.Length != checked(width * height)) throw new ArgumentException("Output dimensions do not match the value count.");
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true);
        writer.WriteLine("P5");
        writer.WriteLine(width.ToString(CultureInfo.InvariantCulture) + " " + height.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("255");
        writer.Flush();
        stream.Write(values, 0, values.Length);
    }

    private sealed class Options
    {
        public string ModelId { get; private set; } = "bria/rmbg-2.0";
        public string Precision { get; private set; } = "fp32";
        public string Quantization { get; private set; } = "none";
        public string ImagePath { get; private set; } = string.Empty;
        public string? OutputPath { get; private set; }
        public string CacheRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharp", "ModelFactory");
        public bool Offline { get; private set; }
        public bool ShowHelp { get; private set; }

        public static Options Parse(IReadOnlyList<string> args)
        {
            var options = new Options();
            for (int index = 0; index < args.Count; index++)
            {
                string value = args[index];
                if (value == "--help" || value == "-h") { options.ShowHelp = true; continue; }
                if (value == "--offline") { options.Offline = true; continue; }
                string name = value;
                string? inline = null;
                int equals = value.IndexOf('=');
                if (equals >= 0) { name = value.Substring(0, equals); inline = value.Substring(equals + 1); }
                string argument = inline ?? (++index < args.Count ? args[index] : throw new ArgumentException("Missing value for " + name));
                switch (name.ToLowerInvariant())
                {
                    case "--model-id": options.ModelId = argument; break;
                    case "--precision": options.Precision = argument; break;
                    case "--quantization": options.Quantization = argument; break;
                    case "--image": options.ImagePath = Path.GetFullPath(argument); break;
                    case "--output": options.OutputPath = Path.GetFullPath(argument); break;
                    case "--cache": options.CacheRoot = Path.GetFullPath(argument); break;
                    default: throw new ArgumentException("Unknown option: " + name);
                }
            }
            if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.ImagePath)) throw new ArgumentException("--image <path> is required.");
            return options;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("DeploySharp Model Release Inference sample");
            Console.WriteLine("dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none --image <image>");
            Console.WriteLine("dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image <image>");
            Console.WriteLine("dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id anomalib/padim/mvtec-bottle --image <image>");
            Console.WriteLine("Options: --image <path> --model-id <id> --precision <id> --quantization <id> --cache <path> --output <path> --offline");
        }
    }
}
