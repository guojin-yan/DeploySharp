using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    /// <summary>Audits the exact local SAM 2 external-data graph contract without claiming an official export. / 审计精确本机 SAM 2 external-data 图合同，但不宣称官方导出。</summary>
    [TestClass]
    public sealed class Stage22Sam2ExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalSam2ImageSubgraphsRunExactNamedPortsOnOpenVinoCpu()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_RUN_EXTERNAL_MODELS"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_RUN_EXTERNAL_MODELS=1 to run authorized local SAM evidence.");
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM2_ONNX_DIR") ?? @"E:\Model\sam\SAM2\sam2.1-hiera-tiny-ONNX\onnx";
            string encoderPath = Path.Combine(root, "vision_encoder.onnx");
            string decoderPath = Path.Combine(root, "prompt_encoder_mask_decoder.onnx");
            foreach (string path in new[] { encoderPath, encoderPath + "_data", decoderPath, decoderPath + "_data" }) if (!File.Exists(path)) Assert.Inconclusive("A required local SAM 2 file is missing: " + path);

            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            using IInferenceSession encoder = registry.CreateSession(new ModelArtifact(new ModelId("external/sam2-image-encoder"), "onnx", encoderPath, preferredBackend: OpenVinoBackendProvider.BackendId), request);
            CollectionAssert.AreEqual(new[] { "pixel_values" }, encoder.Metadata.Inputs.Select(value => value.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "image_embeddings.0", "image_embeddings.1", "image_embeddings.2" }, encoder.Metadata.Outputs.Select(value => value.Name).ToArray());
            var watch = Stopwatch.StartNew();
            InferenceOutputs embeddings = encoder.Run(InferenceInputs.Create("pixel_values", new Tensor<float>(new TensorShape(1, 3, 1024, 1024), new float[3 * 1024 * 1024])), CancellationToken.None);
            watch.Stop();
            double encoderMilliseconds = watch.Elapsed.TotalMilliseconds;

            using IInferenceSession decoder = registry.CreateSession(new ModelArtifact(new ModelId("external/sam2-prompt-mask-decoder"), "onnx", decoderPath, preferredBackend: OpenVinoBackendProvider.BackendId), request);
            CollectionAssert.AreEqual(new[] { "input_points", "input_labels", "input_boxes", "image_embeddings.0", "image_embeddings.1", "image_embeddings.2" }, decoder.Metadata.Inputs.Select(value => value.Name).ToArray());
            var inputs = new InferenceInputs(new List<NamedTensor>
            {
                new NamedTensor("input_points", new Tensor<float>(new TensorShape(1, 1, 1, 2), new[] { 512f, 512f })),
                new NamedTensor("input_labels", new Tensor<long>(new TensorShape(1, 1, 1), new long[] { 1 })),
                new NamedTensor("input_boxes", new Tensor<float>(new TensorShape(1, 1, 4), new[] { 240f, 100f, 780f, 950f })),
                new NamedTensor("image_embeddings.0", embeddings.GetRequired("image_embeddings.0")),
                new NamedTensor("image_embeddings.1", embeddings.GetRequired("image_embeddings.1")),
                new NamedTensor("image_embeddings.2", embeddings.GetRequired("image_embeddings.2"))
            });
            watch.Restart();
            InferenceOutputs outputs = decoder.Run(inputs, CancellationToken.None);
            watch.Stop();
            CollectionAssert.AreEqual(new[] { "iou_scores", "pred_masks", "object_score_logits" }, outputs.Select(value => value.Name).ToArray());
            Assert.AreEqual(new TensorShape(1, 1, 3), outputs.GetRequired("iou_scores").Shape);
            Assert.AreEqual(new TensorShape(1, 1, 3, 256, 256), outputs.GetRequired("pred_masks").Shape);
            Console.WriteLine("STAGE22_SAM2_OPENVINO encoderMs=" + encoderMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";decoderMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";evidence=local-contract-only-not-official-export");
        }
    }
}
