using JYPPX.DeploySharp.ModelFactory;

bool offline = args.Contains("--offline");
string[] positional = args.Where(value => value != "--offline").ToArray();
string cacheRoot = Path.GetFullPath(positional.Length > 0 ? positional[0] : "model-cache");
string modelId = positional.Length > 1 ? positional[1] : "paddleocr/ppocrv5/mobile-cls";
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
using var client = new PaddleOcrReleaseClient(
    new PaddleOcrReleaseClientOptions(cacheRoot, offline: offline));
try
{
    PaddleOcrReleaseModelMaterialization model =
        await client.GetModelAsync(modelId, cancellationToken: cancellation.Token);
    Console.WriteLine($"offline={offline};model={modelId}");
    Console.WriteLine("model_path=" + model.ModelPath);
    Console.WriteLine("dictionary_path=" + (model.DictionaryPath ?? "(none)"));
}
catch (ModelFactoryException exception)
{
    foreach (var diagnostic in exception.Diagnostics)
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    Environment.ExitCode = 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("模型准备已取消。");
    Environment.ExitCode = 2;
}
