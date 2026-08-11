using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

    private static int Main()
    {
        IReadOnlyDictionary<string, string>? files = ResolveFiles();
        if (files == null) return 0;

        try
        {
            string imagePath = files["DEPLOYSHARP_STAGE20_IMAGE"];
            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(
                imagePath,
                "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
            {
                sourceSize = probe.SourceSize;
            }

            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/stage20-detector"), Artifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "ppocr-det-resize32-imagenet-bgr-v1", "ppocr-db-managed-rectangle-v1"));
            PaddleOcrProfile classifier = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/stage20-classifier"), Artifact(11, "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(files["DEPLOYSHARP_STAGE20_OCR_DICT"], "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/stage20-recognizer"), Artifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters);

            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile);
            profiles.Register(classifier.VisualProfile);
            profiles.Register(recognizer.VisualProfile);
            profiles.Freeze();
            using var backends = new BackendRegistry();
            backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new OcrPipeline(
                backends,
                profiles.Select(detector.CreateArtifact(files["DEPLOYSHARP_STAGE20_OCR_DET_MODEL"], OnnxRuntimeBackendProvider.BackendId), backends, request, VisualTaskId.TextDetection), request,
                profiles.Select(classifier.CreateArtifact(files["DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL"], OnnxRuntimeBackendProvider.BackendId), backends, request, VisualTaskId.TextOrientationClassification), request,
                classifier.CropProfile ?? throw new InvalidOperationException("The classifier crop profile is missing."),
                profiles.Select(recognizer.CreateArtifact(files["DEPLOYSHARP_STAGE20_OCR_REC_MODEL"], OnnxRuntimeBackendProvider.BackendId), backends, request, VisualTaskId.TextRecognition), request,
                recognizer.CropProfile ?? throw new InvalidOperationException("The recognition crop profile is missing."),
                new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 16),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
            OcrResult result = pipeline.Run(input);
            if (result.Regions.Count == 0) throw new InvalidOperationException("The external validation image produced no text regions.");
            int classifiedRegions = 0;
            foreach (OcrRegionResult region in result.Regions)
            {
                if (region.Region.Metadata.ContainsKey("ocr.orientation.classIndex")) classifiedRegions++;
            }
            if (classifiedRegions != result.Regions.Count) throw new InvalidOperationException("The classifier did not run for every detected text region.");

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "ocr3Regions={0};classifiedRegions={1};ocr3Sha={2};orientationMs={3:F3}", result.Regions.Count, classifiedRegions, result.ComputeSha256(), result.Timing.OrientationClassification.TotalMilliseconds));
            Console.WriteLine("DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_OK");
            return 0;
        }
        catch (OpenCvVisualException exception) when (exception.ErrorCode == OpenCvErrorCodes.NativeUnavailable)
        {
            return Skip("opencv-native-unavailable");
        }
        catch (OnnxRuntimeBackendException exception) when (exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable || exception.ErrorCode == OnnxRuntimeErrorCodes.ExecutionProviderUnavailable)
        {
            return Skip("onnxruntime-native-unavailable");
        }
        catch (DllNotFoundException)
        {
            return Skip("native-library-unavailable");
        }
        catch (BadImageFormatException)
        {
            return Skip("native-library-incompatible");
        }
        catch (EntryPointNotFoundException)
        {
            return Skip("native-library-abi-incompatible");
        }
    }

    private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing, string? dictionarySha = null)
    {
        return new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "local-exporter-unverified", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? "" : "official-repository-file-separate-review-required");
    }

    private static IReadOnlyDictionary<string, string>? ResolveFiles()
    {
        string[] variables =
        {
            "DEPLOYSHARP_STAGE20_IMAGE",
            "DEPLOYSHARP_STAGE20_OCR_DET_MODEL",
            "DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL",
            "DEPLOYSHARP_STAGE20_OCR_REC_MODEL",
            "DEPLOYSHARP_STAGE20_OCR_DICT"
        };
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string variable in variables)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value) || !File.Exists(value))
            {
                Skip("missing-" + variable.ToLowerInvariant());
                return null;
            }
            files.Add(variable, Path.GetFullPath(value));
        }
        return files;
    }

    private static int Skip(string reason)
    {
        Console.WriteLine("DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_SKIP reason=" + reason);
        return 0;
    }
}
