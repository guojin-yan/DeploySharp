using System.Globalization;
using System.Text.Json;
using DeploySharpApp.Contracts;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

namespace DeploySharpApp.BackendHost;

internal static class VisualReleaseInferenceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<WorkerResponse?> TryRunAsync(WorkerRequest request, Action<double>? progress, CancellationToken cancellationToken)
    {
        if (!string.Equals(Value(request.Payload, "operation"), AppOperationKind.Vision.ToString(), StringComparison.OrdinalIgnoreCase)) return null;
        bool useOnnxRuntime = Contains(request.BackendId, "onnxruntime");
        bool useOpenVino = Contains(request.BackendId, "openvino");
        bool useTensorRt = Contains(request.BackendId, "tensorrt");
        if (!useOnnxRuntime && !useOpenVino && !useTensorRt) return null;
        string id = request.ModelId ?? string.Empty;
        if (!IsReleaseVisualId(id)) return null;
        string requestedDevice = Value(request.Payload, "device") ?? "cpu";
        if (useTensorRt && !string.Equals(requestedDevice, "cuda", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-TENSORRT-DEVICE-INVALID", "TensorRT visual inference requires the CUDA device; it never falls back to CPU.", AppRuntimeState.Unavailable, "requestedDevice=" + requestedDevice);
        if (!useTensorRt && string.Equals(requestedDevice, "cuda", StringComparison.OrdinalIgnoreCase))
            return Error(request, useOpenVino ? "DSAPP-OPENVINO-DEVICE-UNAVAILABLE" : "DSAPP-CUDA-UNAVAILABLE", useOpenVino
                ? "CUDA was requested for the OpenVINO visual adapter, but this release verifies only the CPU device; no CPU fallback is performed."
                : "CUDA was requested for the ONNX Runtime visual adapter, but this Worker build only enables the audited CPU provider; no CPU fallback is performed.", AppRuntimeState.Unavailable, "requestedDevice=cuda;provider=" + (useOpenVino ? "openvino-cpu" : "onnxruntime-cpu"));
        string? pathValue = Value(request.Payload, "modelPath");
        string? imagePath = Value(request.Payload, "inputPath");
        if (string.IsNullOrWhiteSpace(pathValue) || string.IsNullOrWhiteSpace(imagePath)) return Error(request, "DSAPP-VISUAL-INPUT-REQUIRED", "Release visual inference requires a model path and an image path.", AppRuntimeState.Unavailable);
        string modelPath = Path.GetFullPath(pathValue);
        imagePath = Path.GetFullPath(imagePath);
        if (!File.Exists(modelPath)) return Error(request, "DSAPP-MODEL-NOT-FOUND", "The selected Release model file does not exist.", AppRuntimeState.Unavailable, modelPath);
        if (!File.Exists(imagePath)) return Error(request, "DSAPP-IMAGE-NOT-FOUND", "The selected visual input image does not exist.", AppRuntimeState.Unavailable, imagePath);
        try
        {
            progress?.Invoke(.5);
            object? profile = CreateProfile(id, request);
            if (profile is null) return Error(request, "DSAPP-VISUAL-PROFILE-UNSUPPORTED", "The Release manifest has no executable visual profile in this Worker build.", AppRuntimeState.Unsupported, id);
            if (useTensorRt && profile is PromptableSegmentationProfile)
                return Error(request, "DSAPP-TENSORRT-MULTI-ENGINE-BUILD-REQUIRED", "SAM requires separately built and identity-bound encoder and decoder engines; the Worker will not reuse one engine for both ONNX graphs.", AppRuntimeState.Unsupported, id);
            TensorRtPreparedEngine? preparedEngine = null;
            string runtimeModelPath = modelPath;
            if (useTensorRt)
            {
                preparedEngine = TensorRtOnnxEngineAdapter.ResolveOrBuild(request, modelPath, progress, cancellationToken);
                if (!preparedEngine.Succeeded) return TensorRtError(request, preparedEngine);
                runtimeModelPath = preparedEngine.EnginePath!;
            }
            using var registry = new BackendRegistry();
            BackendId backendId;
            string device;
            if (useTensorRt)
            {
                registry.UseTensorRT(ParseTensorRtOptions(request.Payload));
                backendId = TensorRtBackendProvider.BackendId;
                device = "cuda";
            }
            else if (useOpenVino)
            {
                registry.UseOpenVino();
                backendId = OpenVinoBackendProvider.BackendId;
                device = "CPU";
            }
            else
            {
                registry.UseOnnxRuntime();
                backendId = OnnxRuntimeBackendProvider.BackendId;
                device = "cpu";
            }
            var backendRequest = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            string visualProfileId = ProfileId(profile);
            VisualInferenceResult result;
            if (profile is PromptableSegmentationProfile sam)
            {
                result = RunSam(registry, sam, runtimeModelPath, request, imagePath, backendId, backendRequest, cancellationToken);
            }
            else if (profile is YoloDetectionProfile detection)
            {
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, detection.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(detection));
                result = RunPipeline(registry, detection.CreateArtifact(modelPath, backendId), detection.VisualProfile, input, backendRequest, preparedEngine);
            }
            else if (profile is YoloMultiTaskProfile multi)
            {
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, multi.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(multi));
                result = RunPipeline(registry, multi.CreateArtifact(modelPath, backendId), multi.VisualProfile, input, backendRequest, preparedEngine);
            }
            else if (profile is PortableDetectorProfile portable)
            {
                using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, portable);
                result = RunPipeline(registry, portable.CreateArtifact(modelPath, backendId), portable.VisualProfile, input, backendRequest, preparedEngine);
            }
            else if (profile is PaddleOcrProfile paddle)
            {
                using PreparedVisualInput input = CreatePaddleInput(imagePath, paddle);
                result = RunPipeline(registry, paddle.CreateArtifact(modelPath, backendId), paddle.VisualProfile, input, backendRequest, preparedEngine);
            }
            else if (profile is AnomalibProfile anomaly)
            {
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, anomaly.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreateAnomalibOptions(anomaly));
                result = RunPipeline(registry, anomaly.CreateArtifact(modelPath, backendId), anomaly.VisualProfile, input, backendRequest, preparedEngine);
            }
            else if (profile is BriaRmbgProfile matting)
            {
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, matting.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreateBriaRmbgOptions(matting));
                result = RunPipeline(registry, matting.CreateArtifact(modelPath, backendId), matting.VisualProfile, input, backendRequest, preparedEngine);
            }
            else return Error(request, "DSAPP-VISUAL-PROFILE-UNSUPPORTED", "The selected Release visual profile is not executable by this Worker.", AppRuntimeState.Unsupported, id);
            progress?.Invoke(.95);
            var sourceInfo = SixLabors.ImageSharp.Image.Identify(imagePath) ?? throw new InvalidDataException("The source image dimensions could not be identified.");
            string output = VisualResultJson.Serialize(result.Value, sourceInfo.Width, sourceInfo.Height);
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["output"] = output,
                ["backendId"] = backendId.Value,
                ["device"] = device,
                ["execution"] = "worker",
                ["visualProfileId"] = visualProfileId
            };
            if (preparedEngine is not null)
            {
                payload["engineBuildState"] = preparedEngine.Built ? "built" : "cache-hit";
                payload["enginePath"] = preparedEngine.EnginePath ?? string.Empty;
                payload["engineIdentityPath"] = preparedEngine.IdentityPath ?? string.Empty;
                payload["engineSha256"] = preparedEngine.EngineSha256 ?? string.Empty;
            }
            progress?.Invoke(1);
            string buildMessage = preparedEngine is null ? string.Empty : preparedEngine.Message + " ";
            return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, buildMessage + "Release visual model inference completed with the audited main-library profile on " + backendId.Value + ".", payload);
        }
        catch (FileNotFoundException exception) { return Error(request, "DSAPP-MODEL-NOT-FOUND", "A Release visual bundle file is missing.", AppRuntimeState.Unavailable, exception.FileName ?? exception.Message); }
        catch (DllNotFoundException exception) { return Error(request, "DSAPP-NATIVE-MISSING", "A native library required by the selected visual backend is missing.", AppRuntimeState.MissingNative, exception.Message); }
        catch (BadImageFormatException exception) { return Error(request, "DSAPP-NATIVE-ABI-MISMATCH", "A native library required by the selected visual backend has an incompatible ABI.", AppRuntimeState.Unavailable, exception.Message); }
        catch (JYPPX.DeploySharp.Errors.DeploySharpException exception) { return Error(request, exception.ErrorCode, exception.Message, AppRuntimeState.Unavailable, exception.TechnicalDetails); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Error(request, "DSAPP-VISUAL-CANCELLED", "Release visual inference was cancelled.", AppRuntimeState.Unavailable); }
        catch (Exception exception) { return Error(request, "DSAPP-VISUAL-INFERENCE-FAILED", "Release visual inference failed.", AppRuntimeState.Unavailable, exception.ToString()); }
    }

    private static VisualInferenceResult RunPipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile, PreparedVisualInput input, BackendRequest request, TensorRtPreparedEngine? preparedEngine = null)
    {
        if (preparedEngine is not null)
        {
            profile = new VisualModelProfile(profile.ProfileId + ".tensorrt", profile.ModelId, profile.Task, profile.Version, "tensorrt-engine", profile.Input, profile.Outputs, profile.Labels, profile.Decoder, profile.RequiredCapabilities, profile.MinimumBackendVersion);
            artifact = new ModelArtifact(profile.ModelId, "tensorrt-engine", preparedEngine.EnginePath!, preparedEngine.EngineSha256, TensorRtBackendProvider.BackendId);
        }
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.Task), request);
        return pipeline.Run(input);
    }

    private static TensorRtBackendOptions ParseTensorRtOptions(IReadOnlyDictionary<string, string> payload)
        => new(ParseTensorRtApiVersion(Value(payload, "apiVersion") ?? Value(payload, "tensorRtApiVersion")), IntOption(payload, "optimizationProfile", 0), LongOption(payload, "maximumEngineBytes", int.MaxValue), Value(payload, "cudaTargetArchitecture"), BoolOption(payload, "cacheImmutableHostInputsOnDevice", false));

    private static TensorRtApiVersion ParseTensorRtApiVersion(string? value) => value switch
    {
        "8" => TensorRtApiVersion.TensorRt8,
        "11" => TensorRtApiVersion.TensorRt11,
        _ => TensorRtApiVersion.TensorRt10
    };

    private static WorkerResponse TensorRtError(WorkerRequest request, TensorRtPreparedEngine failure)
    {
        AppRuntimeState state = failure.Code.Contains("NATIVE", StringComparison.OrdinalIgnoreCase) || failure.Code.Contains("RUNTIME", StringComparison.OrdinalIgnoreCase) ? AppRuntimeState.MissingNative : AppRuntimeState.Unavailable;
        var payload = new Dictionary<string, string>(failure.Details, StringComparer.Ordinal)
        {
            ["state"] = state.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = failure.Code, ["execution"] = "worker"
        };
        return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, failure.Message, payload);
    }

    private static string ProfileId(object profile) => profile switch
    {
        PromptableSegmentationProfile value => value.ProfileId,
        YoloDetectionProfile value => value.VisualProfile.ProfileId,
        YoloMultiTaskProfile value => value.VisualProfile.ProfileId,
        PortableDetectorProfile value => value.VisualProfile.ProfileId,
        PaddleOcrProfile value => value.VisualProfile.ProfileId,
        AnomalibProfile value => value.VisualProfile.ProfileId,
        BriaRmbgProfile value => value.VisualProfile.ProfileId,
        _ => throw new NotSupportedException("The selected Release profile does not expose a visual profile identifier.")
    };

    private static PreparedVisualInput CreatePaddleInput(string imagePath, PaddleOcrProfile profile)
    {
        var factory = new OpenCvVisualInputFactory();
        if (profile.Family == PaddleOcrFamily.PaddleOcrDet)
        {
            using var probe = factory.CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            return factory.CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
        }
        if (profile.Family == PaddleOcrFamily.PaddleOcrCls)
            return factory.CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceTextLineOrientationOptions());
        return factory.CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceRecognitionOptions());
    }

    private static VisualInferenceResult RunSam(BackendRegistry registry, PromptableSegmentationProfile profile, string encoderPath, WorkerRequest request, string imagePath, BackendId backendId, BackendRequest backendRequest, CancellationToken cancellationToken)
    {
        string? decoderPath = AssetPath(request, "prompt-mask-decoder.onnx") ?? AssetPath(request, "sam-v1-vit-b/prompt-mask-decoder.onnx");
        if (decoderPath is null || !File.Exists(decoderPath)) throw new FileNotFoundException("The SAM prompt-mask decoder is required for a complete bundle.", decoderPath ?? "prompt-mask-decoder.onnx");
        PromptableSegmentationArtifactContract encoderContract = profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
        PromptableSegmentationArtifactContract decoderContract = profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder);
        var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
        {
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, encoderContract.CreateArtifact(encoderPath, backendId)),
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, decoderContract.CreateArtifact(decoderPath, backendId))
        });
        using var session = new PromptableSegmentationImageSession(registry, bundle, backendRequest);
        using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(imagePath);
        session.SetImage(input, cancellationToken: cancellationToken);
        float pointX = NormalizedOption(request, "samPointX", .5f);
        float pointY = NormalizedOption(request, "samPointY", .5f);
        float left = NormalizedOption(request, "samBoxLeft", .1f);
        float top = NormalizedOption(request, "samBoxTop", .1f);
        float right = NormalizedOption(request, "samBoxRight", .9f);
        float bottom = NormalizedOption(request, "samBoxBottom", .9f);
        if (right <= left || bottom <= top) throw new ArgumentException("SAM normalized prompt box must have right > left and bottom > top.");
        RectangleF box = new(input.SourceSize.Width * left, input.SourceSize.Height * top, input.SourceSize.Width * (right - left), input.SourceSize.Height * (bottom - top));
        var points = new[] { new PromptPoint(input.SourceSize.Width * pointX, input.SourceSize.Height * pointY, PromptPointLabel.Foreground) };
        bool multiple = BoolOption(request, "samReturnMultipleMasks", false);
        PromptableSegmentationResult prediction = session.Predict(new PromptableSegmentationPrompt(points, box, returnMultipleMasks: multiple, promptId: "deploysharp-web"), cancellationToken: cancellationToken);
        return new VisualInferenceResult(prediction.Segmentation, VisualTaskId.PromptableSegmentation, profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).ModelId, backendId, InferenceTiming.Zero);
    }

    private static object? CreateProfile(string id, WorkerRequest request)
    {
        string lower = id.ToLowerInvariant();
        string hash = Value(request.Payload, "modelSha256") ?? new string('a', 64);
        if (lower == "segmentation/sam-v1-vit-b") return PromptableSegmentationProfiles.CreateSamV1(
            "promptable.sam.v1.vit-b-dca509f.preview",
            new ModelId("segmentation/sam-v1-vit-b/image-encoder"),
            new ModelId("segmentation/sam-v1-vit-b/prompt-mask-decoder"),
            "95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a",
            "b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a",
            "dca509fe793f601edb92606367a655c15ac00fdf",
            "traceable official-image-encoder wrapper; torch-2.9.1+cpu; opset17",
            "official scripts/export_onnx_model.py plus dynamo=false; torch-2.9.1+cpu; opset17");
        if (lower.StartsWith("yolo/")) return CreateYolo(lower, hash, request);
        if (lower.StartsWith("deim/")) return PortableDetectorProfiles.CreateDEIMv2(new ModelId(id), PortableOptions(id, hash, PortableDetectorFamily.DEIMv2Det, "images", new VisualSize(640, 640), YoloLabelSets.Coco80, Opset(request, 16), request, boxes: "boxes", labelsName: "labels", scores: "scores"));
        if (lower.StartsWith("rf-detr/"))
        {
            bool segment = lower.Contains("segment");
            PortableDetectorProfileOptions options = PortableOptions(id, hash, segment ? PortableDetectorFamily.RFDETRSeg : PortableDetectorFamily.RFDETRDet, "input", new VisualSize(segment ? 432 : 512, segment ? 432 : 512), segment ? Enumerable.Range(0, 90).Select(i => "class" + i) : Enumerable.Range(0, 5).Select(i => "class" + i), Opset(request, 17), request, segment ? 200 : 300, segment ? "4245" : null, boxes: "dets", labelsName: "labels");
            return segment ? PortableDetectorProfiles.CreateRFDETRSeg(new ModelId(id), options) : PortableDetectorProfiles.CreateRFDETR(new ModelId(id), options);
        }
        if (lower.StartsWith("rt-detr/"))
        {
            bool raw = lower.Contains("raw-query");
            bool ir = lower.EndsWith("-ir");
            var options = PortableOptions(id, hash, PortableDetectorFamily.RTDETRDet, "image", new VisualSize(640, 640), YoloLabelSets.Coco80, Opset(request, 16), request, 300, null, ir ? "openvino-ir" : "onnx", raw ? "stack_7.tmp_0_slice_0" : "save_infer_model/scale_0.tmp_0", raw ? "stack_8.tmp_0_slice_0" : null, null, raw ? null : "save_infer_model/scale_1.tmp_0", raw ? PortableDetectorCountShape.Scalar : PortableDetectorCountShape.BatchVector);
            return raw ? PortableDetectorProfiles.CreateRTDETRRaw(new ModelId(id), options) : PortableDetectorProfiles.CreateRTDETR(new ModelId(id), options);
        }
        if (lower.StartsWith("pp-yoloe/")) return PortableDetectorProfiles.CreatePPYOLOE(new ModelId(id), PortableOptions(id, hash, PortableDetectorFamily.PPYOLOEDet, "image", new VisualSize(640, 640), YoloLabelSets.Coco80, Opset(request, 11), request, boxes: "save_infer_model/scale_0.tmp_0", count: "save_infer_model/scale_1.tmp_0", countShape: PortableDetectorCountShape.BatchVector));
        if (lower.StartsWith("paddleocr/") && lower.Contains("-det")) return PaddleOcrProfiles.CreateDetection(new ModelId(id), PaddleArtifact(hash, request, Opset(request, 11)));
        if (lower.StartsWith("paddleocr/") && lower.Contains("-rec"))
        {
            string? dictionary = AssetPath(request, "labels");
            dictionary ??= Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(Value(request.Payload, "modelPath")!))!, "*.txt", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (dictionary is null) throw new FileNotFoundException("PaddleOCR dictionary file is required for full CTC decoding.");
            string dictionarySha = Value(request.Payload, "paddleDictionarySha256") ?? "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";
            return PaddleOcrProfiles.CreateRecognition(new ModelId(id), PaddleArtifact(hash, request, Opset(request, lower.Contains("server-rec") ? 10 : 7), dictionarySha), PaddleOcrProfiles.LoadCharacterSet(dictionary, "release.ppocr", "v5", true, dictionarySha));
        }
        if (lower.StartsWith("paddleocr/") && lower.Contains("-cls")) return PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId(id), PaddleArtifact(hash, request, Opset(request, 7)), rejectionThreshold: 0f);
        if (lower.StartsWith("anomalib/")) return AnomalibProfiles.CreatePadim(new ModelId(id), new AnomalibArtifactContract(Opset(request, 14), hash, UpstreamRevision(request), Exporter(request)));
        if (lower.StartsWith("bria/rmbg-"))
        {
            bool version20 = lower.StartsWith("bria/rmbg-2", StringComparison.Ordinal);
            var options = new BriaRmbgProfileOptions(Opset(request, version20 ? 14 : 11), version20 ? new VisualSize(1024, 1024) : new VisualSize(1024, 1024), version20 ? "pixel_values" : "input", version20 ? "alphas" : "output", hash, UpstreamRevision(request), Exporter(request), Value(request.Payload, "visualLicense") ?? "External", upstreamRepository: UpstreamRepository(request));
            return version20 ? BriaRmbgProfiles.CreateRmbg20(new ModelId(id), options) : BriaRmbgProfiles.CreateRmbg14(new ModelId(id), options);
        }
        return null;
    }

    private static object CreateYolo(string id, string hash, WorkerRequest request)
    {
        string[] parts = id.Split('/');
        string familyToken = parts.Length > 1 ? parts[1] : "v8";
        YoloDetectionFamily family = familyToken switch { "v5" => YoloDetectionFamily.YoloV5, "v6" => YoloDetectionFamily.YoloV6, "v7" => YoloDetectionFamily.YoloV7, "v9" => YoloDetectionFamily.YoloV9, "v10" => YoloDetectionFamily.YoloV10, "v11" => YoloDetectionFamily.YoloV11, "v12" => YoloDetectionFamily.YoloV12, "v13" => YoloDetectionFamily.YoloV13, "v26" => YoloDetectionFamily.YoloV26, _ => YoloDetectionFamily.YoloV8 };
        bool classification = id.Contains("classify", StringComparison.OrdinalIgnoreCase);
        bool segmentation = id.Contains("segment", StringComparison.OrdinalIgnoreCase);
        bool pose = id.Contains("pose", StringComparison.OrdinalIgnoreCase);
        bool obb = id.Contains("obb", StringComparison.OrdinalIgnoreCase);
        int fallbackOpset = family switch
        {
            YoloDetectionFamily.YoloV5 => segmentation ? 17 : 12,
            YoloDetectionFamily.YoloV6 or YoloDetectionFamily.YoloV7 => 12,
            YoloDetectionFamily.YoloV8 => classification || pose || obb ? 17 : segmentation ? 12 : 19,
            YoloDetectionFamily.YoloV13 => 17,
            _ => 19
        };
        int opset = Opset(request, fallbackOpset);
        string commit = UpstreamRevision(request);
        string exporter = Exporter(request);
        if (id.Contains("classify")) return YoloMultiTaskProfiles.CreateClassification(new ModelId(id), hash, Enumerable.Range(0, 1000).Select(i => "class" + i), commit, exporter, new YoloClassificationProfileOptions(opset, new VisualSize(224, 224), topK: 5));
        if (id.Contains("segment"))
        {
            int candidates = id.Contains("v26") ? 300 : id.Contains("v5") ? 25200 : 8400;
            return YoloMultiTaskProfiles.CreateInstanceSegmentation(family, new ModelId(id), hash, YoloLabelSets.Coco80, commit, exporter, new YoloPackedProfileOptions(opset, candidates, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: candidates)));
        }
        if (id.Contains("pose")) return YoloMultiTaskProfiles.CreatePose(family, new ModelId(id), hash, commit, exporter, new YoloPackedProfileOptions(opset, id.Contains("v26") ? 300 : 8400, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: id.Contains("v26") ? 300 : 8400)));
        if (id.Contains("obb")) return YoloMultiTaskProfiles.CreateObb(family, new ModelId(id), hash, YoloLabelSets.Dota15, commit, exporter, new YoloPackedProfileOptions(opset, id.Contains("v26") ? 300 : 21504, new VisualSize(1024, 1024), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: id.Contains("v26") ? 300 : 21504)));
        YoloDetectionOutputKind kind = family is YoloDetectionFamily.YoloV5 or YoloDetectionFamily.YoloV6 ? YoloDetectionOutputKind.RawCandidateMajor : family == YoloDetectionFamily.YoloV7 ? YoloDetectionOutputKind.BatchedEndToEnd : family is YoloDetectionFamily.YoloV10 or YoloDetectionFamily.YoloV26 ? YoloDetectionOutputKind.EndToEnd : YoloDetectionOutputKind.RawAttributeMajor;
        return YoloDetectionProfiles.Create(family, new ModelId(id), hash, YoloLabelSets.Coco80, commit, exporter, new YoloDetectionProfileOptions(opset, outputName: family == YoloDetectionFamily.YoloV7 ? "output" : family == YoloDetectionFamily.YoloV6 ? "outputs" : "output0", outputKind: kind));
    }

    private static PortableDetectorProfileOptions PortableOptions(string id, string hash, PortableDetectorFamily family, string input, VisualSize size, IEnumerable<string> labels, int opset, WorkerRequest request, int queryCount = -1, string? masks = null, string format = "onnx", string? boxes = null, string? labelsName = null, string? scores = null, string? count = null, PortableDetectorCountShape countShape = PortableDetectorCountShape.Scalar)
        => new(opset, size, labels, modelFormat: format, inputName: input, artifactSha256: hash, upstreamRepository: UpstreamRepository(request), upstreamCommit: UpstreamRevision(request), exporterVersion: Exporter(request), license: Value(request.Payload, "visualLicense") ?? "External", scoreThreshold: .25f, maximumCandidates: Math.Max(300, queryCount), maximumResults: Math.Min(300, Math.Max(1, queryCount < 0 ? 300 : queryCount)), topK: 300, boxesOutputName: boxes, labelsOutputName: labelsName, scoresOutputName: scores, countOutputName: count, masksOutputName: masks, rfDetrQueryCount: queryCount, rfDetrIncludesNoObjectClass: family is PortableDetectorFamily.RFDETRDet or PortableDetectorFamily.RFDETRSeg, hasDynamicBatchAxis: format == "onnx", paddleCountShape: countShape);

    private static PaddleOcrArtifactContract PaddleArtifact(string hash, WorkerRequest request, int opset = 7, string? dictionarySha256 = null) => new(opset, hash, UpstreamRevision(request), Exporter(request), Value(request.Payload, "visualLicense") ?? "External", UpstreamRepository(request), Exporter(request), dictionarySha256: dictionarySha256, dictionaryLicense: "External");
    private static string? AssetPath(WorkerRequest request, string role)
    {
        string? json = Value(request.Payload, "visualAssetPathsJson");
        if (json is null) return null;
        try
        {
            var paths = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (paths is null) return null;
            foreach (KeyValuePair<string, string> pair in paths)
                if ((string.Equals(pair.Key, role, StringComparison.OrdinalIgnoreCase) || pair.Key.EndsWith("/" + role, StringComparison.OrdinalIgnoreCase) || pair.Key.EndsWith("\\" + role, StringComparison.OrdinalIgnoreCase) || pair.Key.EndsWith(":" + role, StringComparison.OrdinalIgnoreCase)) && File.Exists(pair.Value)) return Path.GetFullPath(pair.Value);
            return null;
        }
        catch (JsonException) { return null; }
    }
    private static bool IsReleaseVisualId(string id) => id.Equals("segmentation/sam-v1-vit-b", StringComparison.OrdinalIgnoreCase) || id.StartsWith("yolo/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("deim/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("rf-detr/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("rt-detr/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("pp-yoloe/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("paddleocr/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("anomalib/", StringComparison.OrdinalIgnoreCase) || id.StartsWith("bria/rmbg-", StringComparison.OrdinalIgnoreCase);
    private static bool Contains(string? value, string token) => value?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    private static int Opset(WorkerRequest request, int fallback) => int.TryParse(Value(request.Payload, "visualOpset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;
    private static string UpstreamRepository(WorkerRequest request) => Value(request.Payload, "visualUpstreamRepository") ?? "release-manifest";
    private static string UpstreamRevision(WorkerRequest request) => Value(request.Payload, "visualUpstreamRevision") ?? "release-manifest";
    private static string Exporter(WorkerRequest request)
    {
        string name = Value(request.Payload, "visualExporter") ?? "release-manifest";
        string? version = Value(request.Payload, "visualExporterVersion");
        return version is null ? name : name + "/" + version;
    }
    private static float NormalizedOption(WorkerRequest request, string key, float fallback)
    {
        string? text = Value(request.Payload, key);
        if (text is null) return fallback;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value) || value < 0f || value > 1f) throw new ArgumentException(key + " must be a finite value in [0,1].");
        return value;
    }
    private static bool BoolOption(WorkerRequest request, string key, bool fallback) => bool.TryParse(Value(request.Payload, key), out bool value) ? value : fallback;
    private static bool BoolOption(IReadOnlyDictionary<string, string> values, string key, bool fallback) => bool.TryParse(Value(values, key), out bool value) ? value : fallback;
    private static int IntOption(IReadOnlyDictionary<string, string> values, string key, int fallback) => int.TryParse(Value(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    private static long LongOption(IReadOnlyDictionary<string, string> values, string key, long fallback) => long.TryParse(Value(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : fallback;
    private static string? Value(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    private static WorkerResponse Error(WorkerRequest request, string code, string message, AppRuntimeState state, string? detail = null)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal) { ["state"] = state.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = code, ["execution"] = "worker" };
        if (detail is not null) payload["technicalDetail"] = detail;
        return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, message, payload);
    }
}

internal static class VisualResultJson
{
    public static string Serialize(object value, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source image dimensions must be positive.");
        if (value is DetectionResult detection) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "detection", sourceWidth, sourceHeight, detections = detection.Detections.Select(item => new { x = item.Box.X, y = item.Box.Y, width = item.Box.Width, height = item.Box.Height, label = item.Label.Label, classIndex = item.Label.Index, score = item.Label.Score }) }, JsonOptions);
        if (value is ClassificationResult classification) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "classification", sourceWidth, sourceHeight, predictions = classification.Predictions.Select(item => new { label = item.Label, classIndex = item.Index, score = item.Score }) }, JsonOptions);
        if (value is InstanceSegmentationResult segmentation) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "segmentation", sourceWidth, sourceHeight, instances = segmentation.Instances.Select(item => new { item.SourceIndex, item.ClassIndex, item.Label, item.Score, box = new { x = item.BoundingBox.X, y = item.BoundingBox.Y, width = item.BoundingBox.Width, height = item.BoundingBox.Height }, mask = Downsample(item.Mask.ToArray(), item.Mask.Width, item.Mask.Height, item.Mask.CoordinateSpace.ToString(), item.Mask.OriginX, item.Mask.OriginY) }) }, JsonOptions);
        if (value is PoseEstimationResult pose) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "pose", sourceWidth, sourceHeight, instances = pose.Instances.Select(item => new { item.SourceIndex, item.Score, box = item.BoundingBox, keypoints = item.Keypoints.Select(point => new { point.Index, x = point.Point.X, y = point.Point.Y, score = point.Score }) }) }, JsonOptions);
        if (value is JYPPX.DeploySharp.Visual.OrientedDetectionResult oriented) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "obb", sourceWidth, sourceHeight, detections = oriented.Detections.Select(item => new { item.SourceIndex, item.ClassIndex, item.Label, item.Score, points = new[] { item.Quadrilateral.First, item.Quadrilateral.Second, item.Quadrilateral.Third, item.Quadrilateral.Fourth } }) }, JsonOptions);
        if (value is TextDetectionResult ocrDetection) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "ocr-detection", sourceWidth, sourceHeight, regions = ocrDetection.Regions.Select(region => new { region.SourceIndex, score = region.Score, points = region.Polygon.Vertices.Select(point => new { x = point.X, y = point.Y }) }) }, JsonOptions);
        if (value is TextRecognitionBatchResult ocrRecognition) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "ocr-recognition", sourceWidth, sourceHeight, items = ocrRecognition.Items.Select(item => new { item.SourceRegionIndex, item.Text, item.Confidence, item.CharacterSetId, item.CharacterSetVersion, item.CharacterSetSha256, tokens = item.Tokens.Select(token => new { token.Timestep, token.ClassIndex, token.Confidence, token.Text, token.IsBlank, token.IsCollapsedRepeat, token.IsUnknown, token.Emitted }) }) }, JsonOptions);
        if (value is OcrOrientationResult orientation) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "ocr-orientation", sourceWidth, sourceHeight, orientation = orientation.Orientation.ToString(), acceptedOrientation = orientation.AcceptedOrientation?.ToString(), orientation.ClassIndex, orientation.Confidence, orientation.Scores, orientation.Rejected, correctedWidth = orientation.CorrectedImageSize.Width, correctedHeight = orientation.CorrectedImageSize.Height, orientation.Warnings }, JsonOptions);
        if (value is AnomalyDetectionResult anomaly) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "anomaly", sourceWidth, sourceHeight, score = anomaly.ImageScore, threshold = anomaly.Threshold, anomalousPixelRatio = anomaly.AnomalousPixelRatio, map = Downsample(anomaly.NormalizedMap.ToArray(), anomaly.NormalizedMap.Width, anomaly.NormalizedMap.Height) }, JsonOptions);
        if (value is BackgroundRemovalResult matting) return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = "foreground-matting", sourceWidth, sourceHeight, alpha = Downsample(matting.Alpha.ToArray(), matting.Alpha.Width, matting.Alpha.Height) }, JsonOptions);
        return JsonSerializer.Serialize(new { schema = "deploysharp.visual.result.v1", kind = value.GetType().Name, sourceWidth, sourceHeight, value }, JsonOptions);
    }
    private static object Downsample(byte[] values, int width, int height, string coordinateSpace = "SourceImage", int originX = 0, int originY = 0)
    {
        (int sampleWidth, int sampleHeight) = SampleSize(width, height);
        var samples = new byte[sampleWidth * sampleHeight];
        for (int y = 0; y < sampleHeight; y++) for (int x = 0; x < sampleWidth; x++) samples[y * sampleWidth + x] = values[Math.Min(height - 1, y * height / sampleHeight) * width + Math.Min(width - 1, x * width / sampleWidth)];
        return new { width, height, sampleWidth, sampleHeight, coordinateSpace, originX, originY, values = samples };
    }
    private static object Downsample(float[] values, int width, int height)
    {
        (int sampleWidth, int sampleHeight) = SampleSize(width, height);
        var samples = new float[sampleWidth * sampleHeight];
        for (int y = 0; y < sampleHeight; y++) for (int x = 0; x < sampleWidth; x++) samples[y * sampleWidth + x] = values[Math.Min(height - 1, y * height / sampleHeight) * width + Math.Min(width - 1, x * width / sampleWidth)];
        return new { width, height, sampleWidth, sampleHeight, values = samples };
    }
    private static (int Width, int Height) SampleSize(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Mask dimensions must be positive.");
        double scale = Math.Min(1d, Math.Sqrt(4096d / checked((double)width * height)));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
