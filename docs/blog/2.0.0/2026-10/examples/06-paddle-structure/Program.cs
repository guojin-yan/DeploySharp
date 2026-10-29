using System.Text;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 3 || args[0] is not ("page" or "table"))
{
    Console.WriteLine("Usage: dotnet run -c Release -- <page|table> <image> <models> [ort-cpu|openvino]");
    return 1;
}
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    string mode = args[0];
    string backend = args.Length > 3 ? args[3] : "ort-cpu";
    using var demo = new StructureDemo(args[1], args[2], backend, mode);
    if (mode == "page") await demo.RunPageAsync(cancellation.Token);
    else await demo.RunTableAsync(cancellation.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
finally { Console.CancelKeyPress -= cancel; }
