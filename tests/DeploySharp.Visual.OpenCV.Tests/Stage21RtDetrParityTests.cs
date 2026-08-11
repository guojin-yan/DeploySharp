using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Compares canonical RT-DETR fields across real CPU backends without publishing benchmark or accuracy claims. / 比较真实 CPU 后端的 RT-DETR 规范字段，不发布基准或精度结论。</summary>
    [TestClass]
    public sealed class Stage21RtDetrParityTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DecodedAndRawContractsMatchAcrossOrtAndOpenVinoOnRealImage()
        {
            RequireExternal();
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_DETR_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(image)) Assert.Inconclusive("The configured local validation image does not exist: " + image);

            TimedResult ortDecoded = RunDecoded(false, image);
            TimedResult openVinoDecoded = RunDecoded(true, image);
            Compare(ortDecoded.Result, openVinoDecoded.Result, .002f, .25f);

            TimedResult ortRaw = RunRaw(false, image);
            TimedResult openVinoRaw = RunRaw(true, image);
            Compare(ortRaw.Result, openVinoRaw.Result, .002f, .25f);

            Console.WriteLine(
                "STAGE21_RTDETR_PARITY inputSha=" + FileSha256(image) +
                ";decodedCount=" + ortDecoded.Result.Detections.Count.ToString(CultureInfo.InvariantCulture) +
                ";decodedOrtSha=" + CanonicalSha256(ortDecoded.Result) +
                ";decodedOpenVinoSha=" + CanonicalSha256(openVinoDecoded.Result) +
                ";decodedOrtMs=" + ortDecoded.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";decodedOpenVinoMs=" + openVinoDecoded.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";rawCount=" + ortRaw.Result.Detections.Count.ToString(CultureInfo.InvariantCulture) +
                ";rawOrtSha=" + CanonicalSha256(ortRaw.Result) +
                ";rawOpenVinoSha=" + CanonicalSha256(openVinoRaw.Result) +
                ";rawOrtMs=" + ortRaw.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";rawOpenVinoMs=" + openVinoRaw.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        private static TimedResult RunDecoded(bool openVino, string image)
        {
            string path = openVino
                ? @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.xml"
                : @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.onnx";
            if (!File.Exists(path)) Assert.Inconclusive("The configured local RT-DETR decoded artifact does not exist: " + path);
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(640, 640),
                YoloLabelSets.Coco80,
                modelFormat: openVino ? "openvino-ir" : "onnx",
                inputName: "image",
                artifactSha256: openVino ? "9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d" : "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db",
                upstreamRepository: "local-authorized-read-only",
                upstreamCommit: "external-review-required",
                exporterVersion: openVino ? "external-openvino-ir" : "inspected-paddle2onnx",
                license: "External",
                scoreThreshold: .4f,
                boxesOutputName: "save_infer_model/scale_0.tmp_0",
                countOutputName: openVino ? "cast_5.tmp_0" : "save_infer_model/scale_1.tmp_0",
                hasDynamicBatchAxis: openVino ? false : true,
                paddleCountShape: PortableDetectorCountShape.BatchVector);
            return Run(PortableDetectorProfiles.CreateRTDETR(new ModelId("external/stage21-rtdetr-decoded-" + (openVino ? "openvino" : "ort")), options), path, openVino, image);
        }

        private static TimedResult RunRaw(bool openVino, string image)
        {
            const string path = @"E:\Model\RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx";
            if (!File.Exists(path)) Assert.Inconclusive("The configured local RT-DETR raw artifact does not exist: " + path);
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(640, 640),
                YoloLabelSets.Coco80,
                inputName: "image",
                artifactSha256: "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad",
                upstreamRepository: "local-authorized-read-only",
                upstreamCommit: "external-review-required",
                exporterVersion: "inspected-paddle2onnx-raw",
                license: "External",
                scoreThreshold: .4f,
                maximumCandidates: 300,
                maximumResults: 100,
                topK: 300,
                boxesOutputName: "stack_7.tmp_0_slice_0",
                labelsOutputName: "stack_8.tmp_0_slice_0",
                rfDetrQueryCount: 300,
                hasDynamicBatchAxis: true);
            return Run(PortableDetectorProfiles.CreateRTDETRRaw(new ModelId("external/stage21-rtdetr-raw-" + (openVino ? "openvino" : "ort")), options), path, openVino, image);
        }

        private static TimedResult Run(PortableDetectorProfile profile, string path, bool openVino, string image)
        {
            BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backend, device);
            using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, backend), registry, request, profile.VisualProfile.Task), request);
            using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), image, profile);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            watch.Stop();
            return new TimedResult(result, watch.Elapsed);
        }

        private static void Compare(DetectionResult expected, DetectionResult actual, float scoreTolerance, float boxTolerance)
        {
            Assert.AreEqual(expected.Detections.Count, actual.Detections.Count, "Threshold decisions must produce the same result count.");
            for (int index = 0; index < expected.Detections.Count; index++)
            {
                Detection left = expected.Detections[index];
                Detection right = actual.Detections[index];
                Assert.AreEqual(left.Label.Index, right.Label.Index, "Class order differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Label.Label, right.Label.Label, "Label order differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Label.Score, right.Label.Score, scoreTolerance, "Score differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Box.X, right.Box.X, boxTolerance, "Box X differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Box.Y, right.Box.Y, boxTolerance, "Box Y differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Box.Width, right.Box.Width, boxTolerance, "Box width differs at " + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(left.Box.Height, right.Box.Height, boxTolerance, "Box height differs at " + index.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string CanonicalSha256(DetectionResult result)
        {
            var builder = new StringBuilder();
            foreach (Detection detection in result.Detections)
            {
                builder.Append(detection.Label.Index.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Label.Label).Append('|')
                    .Append(detection.Label.Score.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Width.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Height.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            using SHA256 sha = SHA256.Create();
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static string FileSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Hex(sha.ComputeHash(stream));
        }

        private static string Hex(byte[] values)
        {
            var builder = new StringBuilder(values.Length * 2);
            foreach (byte value in values) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_RTDETR_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE21_RTDETR_EXTERNAL=1 to run RT-DETR field parity.");
        }

        private sealed class TimedResult
        {
            internal TimedResult(DetectionResult result, TimeSpan elapsed) { Result = result; Elapsed = elapsed; }
            internal DetectionResult Result { get; }
            internal TimeSpan Elapsed { get; }
        }
    }
}
