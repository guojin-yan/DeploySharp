using System.Text;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

Console.OutputEncoding = Encoding.UTF8;
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    AppOptions options = AppOptions.Parse(args, "products.json");
    using var backends = new BackendRegistry();
    if (options.Backend == "ort-cpu") backends.UseOnnxRuntime(); else backends.UseOpenVino();
    var request = new BackendRequest(BackendCapabilities.TensorInference,
        options.Backend == "ort-cpu" ? OnnxRuntimeBackendProvider.BackendId : OpenVinoBackendProvider.BackendId,
        options.Backend == "ort-cpu" ? "cpu" : "CPU");
    using var catalog = new ProductCatalog(backends, request, options);
    return await catalog.RunAsync(cancellation.Token);
}
catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 2; }
catch (Exception error) when (cancellation.IsCancellationRequested) { Console.Error.WriteLine("已取消：" + error.Message); return 2; }
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
finally { Console.CancelKeyPress -= cancel; }

internal sealed record AppOptions(string Backend, string Models, string Manifest, string Output)
{
    public static AppOptions Parse(string[] args, string manifest)
    {
        var values = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key is not ("--backend" or "--models" or "--manifest" or "--out") || i + 1 == args.Length || !values.TryAdd(key, args[++i]))
                throw new ArgumentException("Usage: --backend ort-cpu|openvino --models models --manifest <json> --out results");
        }
        string backend = values.GetValueOrDefault("--backend", "ort-cpu");
        if (backend is not ("ort-cpu" or "openvino")) throw new ArgumentException("Supported backends: ort-cpu, openvino");
        return new(backend, Path.GetFullPath(values.GetValueOrDefault("--models", "models")),
            Path.GetFullPath(values.GetValueOrDefault("--manifest", manifest)),
            Path.GetFullPath(values.GetValueOrDefault("--out", "results")));
    }
}
