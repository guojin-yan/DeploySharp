using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

internal static class Program
{
    private static int Main()
    {
        var profile = new VisualModelProfile(
            "sample/classifier.onnx.v1",
            new ModelId("sample/classifier"),
            VisualTaskId.ImageClassification,
            "1",
            "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
            new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
            new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 2));
        var registry = new VisualProfileRegistry();
        registry.Register(profile);
        registry.Freeze();
        Console.WriteLine($"DEPLOYSHARP_VISUAL_SAMPLE_OK profile={registry.GetRequired(profile.ProfileId).ProfileId} frozen={registry.IsFrozen}");
        return 0;
    }
}
