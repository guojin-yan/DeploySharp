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
using JYPPX.DeploySharp.Visual.Models.Anomalib;
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
            string imagePath = files["DEPLOYSHARP_STAGE19_IMAGE"];
            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(
                imagePath,
                "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
            {
                sourceSize = probe.SourceSize;
            }

            PaddleOcrProfile detector = CreateDetector();
            PaddleOcrProfile recognizer = CreateRecognizer(files["DEPLOYSHARP_STAGE19_OCR_DICT"]);
            AnomalibProfile anomaly = CreateAnomaly();
            BriaRmbgProfile rmbg = CreateRmbg();

            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile);
            profiles.Register(recognizer.VisualProfile);
            profiles.Register(anomaly.VisualProfile);
            profiles.Register(rmbg.VisualProfile);
            profiles.Freeze();

            using var backends = new BackendRegistry();
            backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");

            OcrResult ocr = RunOcr(files, imagePath, sourceSize, detector, recognizer, profiles, backends, request);
            AnomalyDetectionResult anomalyResult = RunAnomaly(files, imagePath, anomaly, profiles, backends, request);
            BackgroundRemovalResult alphaResult = RunRmbg(files, imagePath, rmbg, profiles, backends, request);

            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "ocrRegions={0};ocrSha={1};anomalyScore={2:R};anomalySha={3};alphaSha={4}",
                ocr.Regions.Count,
                ocr.ComputeSha256(),
                anomalyResult.ImageScore,
                anomalyResult.ComputeSha256(),
                alphaResult.Alpha.ComputeSha256()));
            Console.WriteLine("DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_OK");
            return 0;
        }
        catch (OpenCvVisualException exception) when (exception.ErrorCode == OpenCvErrorCodes.NativeUnavailable)
        {
            return Skip("opencv-native-unavailable");
        }
        catch (OnnxRuntimeBackendException exception) when (
            exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable ||
            exception.ErrorCode == OnnxRuntimeErrorCodes.ExecutionProviderUnavailable)
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

    private static OcrResult RunOcr(IReadOnlyDictionary<string, string> files, string imagePath, VisualSize sourceSize, PaddleOcrProfile detector, PaddleOcrProfile recognizer, VisualProfileRegistry profiles, BackendRegistry backends, BackendRequest request)
    {
        ModelArtifact detectorArtifact = detector.CreateArtifact(files["DEPLOYSHARP_STAGE19_OCR_DET_MODEL"], OnnxRuntimeBackendProvider.BackendId);
        ModelArtifact recognizerArtifact = recognizer.CreateArtifact(files["DEPLOYSHARP_STAGE19_OCR_REC_MODEL"], OnnxRuntimeBackendProvider.BackendId);
        using var pipeline = new OcrPipeline(
            backends,
            profiles.Select(detectorArtifact, backends, request, VisualTaskId.TextDetection),
            request,
            profiles.Select(recognizerArtifact, backends, request, VisualTaskId.TextRecognition),
            request,
            recognizer.CropProfile ?? throw new InvalidOperationException("The Paddle recognition crop profile is missing."),
            new OcrPipelineOptions(maximumRecognitionBatch: 16));
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
            imagePath,
            detector.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
        OcrResult result = pipeline.Run(input);
        if (result.Regions.Count == 0) throw new InvalidOperationException("The external OCR validation image produced no text regions, so recognition did not run.");
        return result;
    }

    private static AnomalyDetectionResult RunAnomaly(IReadOnlyDictionary<string, string> files, string imagePath, AnomalibProfile profile, VisualProfileRegistry profiles, BackendRegistry backends, BackendRequest request)
    {
        ModelArtifact artifact = profile.CreateArtifact(files["DEPLOYSHARP_STAGE19_ANOMALIB_PADIM_MODEL"], OnnxRuntimeBackendProvider.BackendId);
        using var pipeline = new AnomalyPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.AnomalyDetection), request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            imagePath,
            profile.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreateAnomalibOptions(profile));
        return pipeline.Run(input);
    }

    private static BackgroundRemovalResult RunRmbg(IReadOnlyDictionary<string, string> files, string imagePath, BriaRmbgProfile profile, VisualProfileRegistry profiles, BackendRegistry backends, BackendRequest request)
    {
        ModelArtifact artifact = profile.CreateArtifact(files["DEPLOYSHARP_STAGE19_BRIA_RMBG14_MODEL"], OnnxRuntimeBackendProvider.BackendId);
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ForegroundMatting), request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            imagePath,
            profile.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
        return pipeline.Run(input).GetValue<BackgroundRemovalResult>();
    }

    private static PaddleOcrProfile CreateDetector()
    {
        return PaddleOcrProfiles.CreateDetection(
            new ModelId("external/ppocrv5-mobile-det"),
            new PaddleOcrArtifactContract(
                11,
                "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039",
                "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
                "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
                "Apache-2.0;external-artifact-redistribution-unverified",
                "ppocr-det-resize-long960-stride128-f32-v2",
                "ppocr-db-contour-minarea-unclip-v2"));
    }

    private static PaddleOcrProfile CreateRecognizer(string dictionaryPath)
    {
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.ppocrv5", "v5", true, DictionarySha);
        return PaddleOcrProfiles.CreateRecognition(
            new ModelId("external/ppocrv5-mobile-rec"),
            new PaddleOcrArtifactContract(
                7,
                "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9",
                "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
                "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
                "Apache-2.0;external-artifact-redistribution-unverified",
                "ppocr-rec-bgr-half-range-h48-v1",
                "ppocr-ctc-probability-greedy-v1",
                dictionarySha256: DictionarySha,
                dictionaryLicense: "official-repository-file-separate-review-required"),
            characters);
    }

    private static AnomalibProfile CreateAnomaly()
    {
        return AnomalibProfiles.CreatePadim(
            new ModelId("external/anomalib-padim"),
            new AnomalibArtifactContract(
                14,
                "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff",
                "ffde4cce3db38964f9cf627b524dd325401c6107",
                "pytorch-2.7.1-opset14"));
    }

    private static BriaRmbgProfile CreateRmbg()
    {
        return BriaRmbgProfiles.CreateRmbg14(
            new ModelId("external/bria-rmbg-1.4"),
            new BriaRmbgProfileOptions(
                11,
                new VisualSize(1024, 1024),
                "input",
                "output",
                "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f",
                "2ceba5a5efaec153162aedea169f76caf9b46cf8",
                "pytorch-2.1.0-opset11",
                "LicenseRef-BRIA-RMBG-1.4;external-artifact-redistribution-unverified"));
    }

    private static IReadOnlyDictionary<string, string>? ResolveFiles()
    {
        string[] variables =
        {
            "DEPLOYSHARP_STAGE19_IMAGE",
            "DEPLOYSHARP_STAGE19_OCR_DET_MODEL",
            "DEPLOYSHARP_STAGE19_OCR_REC_MODEL",
            "DEPLOYSHARP_STAGE19_OCR_DICT",
            "DEPLOYSHARP_STAGE19_ANOMALIB_PADIM_MODEL",
            "DEPLOYSHARP_STAGE19_BRIA_RMBG14_MODEL"
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
        Console.WriteLine("DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_SKIP reason=" + reason);
        return 0;
    }
}
