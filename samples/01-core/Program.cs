using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

internal static class Program
{
    private static int Main()
    {
        var model = new ModelId("sample/contract-inspection");
        var artifact = new ModelArtifact(model, "onnx", "model.onnx", preferredBackend: null);
        var tensor = new Tensor<float>(new TensorShape(1, 3), new[] { 0.1f, 0.2f, 0.7f });
        Console.WriteLine($"DEPLOYSHARP_CORE_SAMPLE_OK model={artifact.ModelId.Value} shape={tensor.Shape}");
        return 0;
    }
}
