using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine("Usage: dotnet run -- <model-root> <bundle-root> <patch-bin> <width> <height> <task>");
            Console.Error.WriteLine("task: TextRecognition | TableRecognition | FormulaRecognition | ChartRecognition | TextSpotting | SealRecognition");
            Console.Error.WriteLine("patch-bin: little-endian float32 [grid_h,grid_w,3,14,14] from the official PaddleOCR-VL Processor");
            return 2;
        }

        string modelRoot = Path.GetFullPath(args[0]);
        string bundleRoot = Path.GetFullPath(args[1]);
        string patchPath = Path.GetFullPath(args[2]);
        int width = ParsePositive(args[3], "width");
        int height = ParsePositive(args[4], "height");
        if (!Enum.TryParse(args[5], ignoreCase: true, out PaddleOcrVlTask task))
            throw new ArgumentException("Unknown PaddleOCR-VL task: " + args[5]);

        var plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(width, height), task);
        int elementCount = checked(plan.PatchCount * 3 * 14 * 14);
        byte[] bytes = File.ReadAllBytes(patchPath);
        if (bytes.Length != checked(elementCount * sizeof(float)))
            throw new InvalidDataException($"Patch file has {bytes.Length} bytes; expected {elementCount * sizeof(float)}.");
        var values = new float[elementCount];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);

        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
            enableMemoryPattern: false, enableCpuMemoryArena: false));
        var request = new BackendRequest(BackendCapabilities.TensorInference,
            OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var session = PaddleOcrVlOnnxSession.Create(registry, bundleRoot, request);
        var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
        var prepared = new PaddleOcrVlPreparedInput(plan,
            new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), values,
                TensorBufferOwnership.Transfer));
        PaddleOcrVlGenerationResult result = await session.GenerateAsync(prepared, tokenizer,
            new PaddleOcrVlGenerationOptions(maximumNewTokens: 512));

        Console.WriteLine($"task={result.Task}");
        Console.WriteLine($"tokens={result.TokenIds.Count}");
        Console.WriteLine($"finish={result.FinishReason}");
        Console.WriteLine($"elapsed_ms={result.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine(result.Text);
        return 0;
    }

    private static int ParsePositive(string value, string name)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException(name + " must be a positive integer.");
}
