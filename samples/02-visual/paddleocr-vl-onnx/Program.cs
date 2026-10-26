using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
            {
                PrintUsage();
                return 0;
            }

            if (args.Length >= 2 && (args[0] == "--release" || args[0] == "--bundle"))
                return await RunImageAsync(args);
            if (args.Length == 6)
                return await RunPatchesAsync(args);
            PrintUsage();
            return 2;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return 2;
        }
    }

    private static async Task<int> RunImageAsync(string[] args)
    {
        bool release = args[0] == "--release";
        string sourceRoot = Path.GetFullPath(args[1]);
        bool hasImage = args.Length >= 4 && !args[2].StartsWith("--", StringComparison.Ordinal);
        string? imagePath = hasImage ? Path.GetFullPath(args[2]) : null;
        PaddleOcrVlTask task = hasImage ? ParseTask(args[3]) : PaddleOcrVlTask.TextRecognition;
        bool offline = false;
        bool verifyOnly = false;
        bool rawExif = false;
        foreach (string flag in args.Skip(hasImage ? 4 : 2))
        {
            switch (flag)
            {
                case "--offline" when release && !offline: offline = true; break;
                case "--verify-only" when !verifyOnly: verifyOnly = true; break;
                case "--raw-exif" when hasImage && !rawExif: rawExif = true; break;
                default: throw new ArgumentException("Unsupported or duplicate option: " + flag);
            }
        }
        if (verifyOnly && rawExif) throw new ArgumentException("--raw-exif requires inference.");
        if (!verifyOnly && imagePath == null) throw new ArgumentException("An image path and task are required for inference.");
        if (!verifyOnly && !File.Exists(imagePath))
        {
            Console.Error.WriteLine("Input image not found: " + imagePath);
            return 2;
        }

        if (release)
        {
            using var client = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(sourceRoot,
                offline: offline, requestTimeout: TimeSpan.FromHours(2)));
            PaddleOcrReleaseBundleMaterialization downloaded = await client.GetBundleAsync("paddleocr-vl/1.6");
            sourceRoot = downloaded.TokenizerDirectoryPath;
        }

        if (verifyOnly)
        {
            PaddleOcrVlBundleManifest manifest = PaddleOcrVlBundleManifest.LoadFromDirectory(sourceRoot);
            manifest.VerifyFiles(sourceRoot);
            Console.WriteLine($"bundle={manifest.BundleId} graphs={(manifest.HasPrefillGraph ? 5 : 4)} verified={manifest.Artifacts.Count} directory={sourceRoot}");
            return 0;
        }

        PaddleOcrVlPreparedInput prepared = new OpenCvPaddleOcrVlInputFactory(
            128 * 1024 * 1024, normalizeExifOrientation: !rawExif).CreateFromFile(imagePath!, task);
        return await GenerateAsync(sourceRoot, sourceRoot, prepared, verified: true);
    }

    private static async Task<int> RunPatchesAsync(string[] args)
    {
        string modelRoot = Path.GetFullPath(args[0]);
        string bundleRoot = Path.GetFullPath(args[1]);
        string patchPath = Path.GetFullPath(args[2]);
        int width = ParsePositive(args[3], "width");
        int height = ParsePositive(args[4], "height");
        PaddleOcrVlTask task = ParseTask(args[5]);

        var plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(width, height), task);
        int elementCount = checked(plan.PatchCount * 3 * 14 * 14);
        byte[] bytes = File.ReadAllBytes(patchPath);
        if (bytes.Length != checked(elementCount * sizeof(float)))
            throw new InvalidDataException($"Patch file has {bytes.Length} bytes; expected {elementCount * sizeof(float)}.");
        var values = new float[elementCount];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);

        var prepared = new PaddleOcrVlPreparedInput(plan,
            new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), values,
                TensorBufferOwnership.Transfer));
        return await GenerateAsync(modelRoot, bundleRoot, prepared, verified: false);
    }

    private static async Task<int> GenerateAsync(string modelRoot, string bundleRoot, PaddleOcrVlPreparedInput prepared, bool verified)
    {
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
            enableMemoryPattern: false, enableCpuMemoryArena: false));
        var request = new BackendRequest(BackendCapabilities.TensorInference,
            OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var session = verified
            ? PaddleOcrVlOnnxSession.CreateVerified(registry, bundleRoot, request)
            : PaddleOcrVlOnnxSession.Create(registry, bundleRoot, request);
        var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
        PaddleOcrVlGenerationResult result = await session.GenerateAsync(prepared, tokenizer,
            new PaddleOcrVlGenerationOptions(maximumNewTokens: 512, timeout: TimeSpan.FromMinutes(15)));

        Console.WriteLine($"task={result.Task}");
        Console.WriteLine($"tokens={result.TokenIds.Count}");
        Console.WriteLine($"tokens_sha256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", result.TokenIds)))).ToLowerInvariant()}");
        Console.WriteLine($"text_sha256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Text))).ToLowerInvariant()}");
        Console.WriteLine($"finish={result.FinishReason}");
        Console.WriteLine($"elapsed_ms={result.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine(result.Text);
        return 0;
    }

    private static PaddleOcrVlTask ParseTask(string value)
        => Enum.TryParse(value, ignoreCase: true, out PaddleOcrVlTask parsed) &&
            Enum.IsDefined(typeof(PaddleOcrVlTask), parsed)
            ? parsed : throw new ArgumentException("Unknown PaddleOCR-VL task: " + value);

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  --release <cache-root> <image-path> <task> [--offline] [--raw-exif]");
        Console.WriteLine("  --bundle <bundle-root> <image-path> <task> [--raw-exif]");
        Console.WriteLine("  --release <cache-root> --verify-only [--offline]");
        Console.WriteLine("  --bundle <bundle-root> --verify-only");
        Console.WriteLine("  <model-root> <bundle-root> <patch-bin> <width> <height> <task>");
        Console.WriteLine("task: TextRecognition | TableRecognition | FormulaRecognition | ChartRecognition | TextSpotting | SealRecognition");
        Console.WriteLine("--raw-exif: preserve encoded JPEG orientation for comparison with the official Pillow processor");
        Console.WriteLine("patch-bin: little-endian float32 [grid_h,grid_w,3,14,14] from the official PaddleOCR-VL Processor");
    }

    private static int ParsePositive(string value, string name)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException(name + " must be a positive integer.");
}
