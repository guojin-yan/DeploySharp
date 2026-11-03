using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Codecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Contains("--help"))
            {
                Console.WriteLine("RF-DETR current-source sample (net10.0, Windows x64)");
                Console.WriteLine("--contract <rfdetr.json> [--model <onnx>] [--image <path>] [--backend ort|cuda|openvino|opencv|tensorrt] [--engine <local-engine>] [--output <directory>] [--concurrency 1|2] [--raw-input <input.f32>] [--validate <export-directory>]");
                return 0;
            }
            if (args.Length % 2 != 0) throw new ArgumentException("Every option requires a value.");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] allowed = { "--contract", "--model", "--image", "--backend", "--engine", "--output", "--concurrency", "--raw-input", "--validate" };
            for (int index = 0; index < args.Length; index += 2)
            {
                if (!allowed.Contains(args[index])) throw new ArgumentException("Unknown option: " + args[index]);
                options.Add(args[index], args[index + 1]);
            }
            string contractPath = Path.GetFullPath(options["--contract"]);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(contractPath));
            JsonElement contract = document.RootElement;
            string modelPath = Path.GetFullPath(options.GetValueOrDefault("--model", Path.Combine(Path.GetDirectoryName(contractPath)!, contract.GetProperty("model").GetProperty("path").GetString()!)));
            string image = TestImageResolver.Resolve(options.GetValueOrDefault("--image", ""), !options.ContainsKey("--image"), false);
            string? referenceDirectory = options.TryGetValue("--validate", out string? referencePath) ? Path.GetFullPath(referencePath) : null;
            if (referenceDirectory != null) VerifyIdentity(image, contract.GetProperty("image"));
            string output = Path.GetFullPath(options.GetValueOrDefault("--output", "artifacts/rf-detr"));
            string backend = options.GetValueOrDefault("--backend", "ort");
            if (options.ContainsKey("--engine") && backend != "tensorrt") throw new ArgumentException("--engine requires --backend tensorrt.");
            int concurrency = int.Parse(options.GetValueOrDefault("--concurrency", "1"), System.Globalization.CultureInfo.InvariantCulture);
            if (concurrency < 1 || concurrency > 2) throw new ArgumentOutOfRangeException(nameof(concurrency));
            Directory.CreateDirectory(output);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, value) => { value.Cancel = true; cancellation.Cancel(); };
            VerifyIdentity(modelPath, contract.GetProperty("model"));
            string manifestPath = Path.Combine(Path.GetDirectoryName(contractPath)!, "modelpack.json");
            if (File.Exists(manifestPath))
            {
                ModelPackageLoader.Load(manifestPath);
                Console.WriteLine("localModelPack=verified");
            }
            VisualModelProfile profile = CreateProfile(contract);
            bool pose = profile.Task == VisualTaskId.PoseEstimation;
            using var registry = new BackendRegistry();
            BackendRequest request = Register(registry, profile, backend);
            var artifact = new ModelArtifact(profile.ModelId, "onnx", modelPath, contract.GetProperty("model").GetProperty("sha256").GetString());
            if (backend == "tensorrt")
            {
                if (options.TryGetValue("--engine", out string? existingEngine))
                {
                    string enginePath = Path.GetFullPath(existingEngine);
                    using Stream engineStream = File.OpenRead(enginePath);
                    artifact = new ModelArtifact(profile.ModelId, "tensorrt-engine", enginePath, Convert.ToHexString(SHA256.HashData(engineStream)).ToLowerInvariant());
                }
                else
                {
                    var build = new TensorRtOnnxEngineBuilder().Build(artifact, Path.Combine(output, "model.engine"),
                        new TensorRtOnnxEngineBuildOptions(TensorRtApiVersion.TensorRt10, TensorRtOnnxEnginePrecision.Float32, workspaceBytes: 268435456, optimizationLevel: 0, overwrite: true,
                            disableTf32: true), cancellation.Token);
                    artifact = new ModelArtifact(profile.ModelId, "tensorrt-engine", build.EnginePath, build.EngineSha256);
                }
                profile = CloneFormat(profile, "tensorrt-engine");
            }
            VisualModelProfile preparationProfile = profile;
            if (referenceDirectory != null) profile = WithDecoder(profile, new ReferenceDecoder(profile.Decoder, referenceDirectory));
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.Task), request, new SessionOptions(concurrency));
            var factory = new RFDETRInputFactory();
            using PreparedVisualInput input = options.TryGetValue("--raw-input", out string? rawInput)
                ? RawInput(rawInput, preparationProfile, contract)
                : factory.Create(OpenCvImageSource.FromFile(image), preparationProfile, cancellationToken: cancellation.Token);
            if (referenceDirectory != null) ReferenceDecoder.ValidateInput(input, referenceDirectory);
            VisualInferenceResult result = await pipeline.RunAsync(input, cancellationToken: cancellation.Token);
            object owned = result.Value;
            if (concurrency == 2)
            {
                // Three separately prepared inputs exercise independent native sessions and an uneven final item.
                using PreparedVisualInput second = factory.Create(OpenCvImageSource.FromFile(image), preparationProfile, inputId: "second");
                using PreparedVisualInput third = factory.Create(OpenCvImageSource.FromFile(image), preparationProfile, inputId: "third");
                IReadOnlyList<VisualInferenceResult> many = await pipeline.RunManyAsync(new[] { input, second, third }, cancellationToken: cancellation.Token);
                if (many.Count != 3 || many.Any(value => Count(value.Value) != Count(owned))) throw new InvalidDataException("Independent-session results did not preserve all three inputs.");
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                try { await pipeline.RunAsync(input, cancellationToken: canceled.Token); throw new InvalidDataException("Cancellation was not observed."); }
                catch (VisualException exception) when (exception.ErrorCode == VisualErrorCodes.Cancelled) { }
                Console.WriteLine("independentSessions=2;orderedResults=3;preCanceled=passed;trueBatch=1");
            }
            if (referenceDirectory != null) Validate(owned, referenceDirectory);
            object serializable = pose ? WritePose(image, output, (PoseEstimationResult)owned) : WriteSegmentation(image, output, (InstanceSegmentationResult)owned);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { modelId = profile.ModelId.Value, backend, source = input.SourceSize, result = serializable }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"model={profile.ModelId};backend={result.BackendId};count={Count(owned)};output={output}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 2;
        }
    }

    private static int Count(object result) => result is PoseEstimationResult pose ? pose.Instances.Count : ((InstanceSegmentationResult)result).Instances.Count;

    private static void VerifyIdentity(string path, JsonElement expected)
    {
        using Stream stream = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (stream.Length != expected.GetProperty("size").GetInt64() || hash != expected.GetProperty("sha256").GetString()) throw new InvalidDataException("The model size/SHA-256 differs from rfdetr.json. Export a new contract for replacement models.");
    }

    private static VisualModelProfile CreateProfile(JsonElement contract)
    {
        string[] labels = contract.GetProperty("labels").EnumerateArray().Select(value => value.GetString()!).ToArray();
        int size = contract.GetProperty("modelSize").GetInt32();
        var id = new ModelId(contract.GetProperty("modelId").GetString()!);
        string task = contract.GetProperty("task").GetString()!;
        var options = new PortableDetectorProfileOptions(contract.GetProperty("opset").GetInt32(), new VisualSize(size, size), labels,
            inputName: contract.GetProperty("inputName").GetString()!, artifactSha256: contract.GetProperty("model").GetProperty("sha256").GetString(),
            upstreamRepository: "https://github.com/roboflow/rf-detr", upstreamCommit: contract.GetProperty("upstreamCommit").GetString()!, exporterVersion: "rfdetr-" + contract.GetProperty("upstreamVersion").GetString() + "-opset17", license: "Apache-2.0",
            scoreThreshold: contract.GetProperty("threshold").GetSingle(), maximumResults: task == "keypoint" ? 100 : 300, topK: task == "keypoint" ? 100 : 300,
            masksOutputName: task == "segment" ? contract.GetProperty("outputs")[2].GetProperty("name").GetString() : null,
            rfDetrIncludesNoObjectClass: contract.TryGetProperty("includesNoObjectClass", out JsonElement noObject) && noObject.GetBoolean(),
            maximumMaskPixels: 256L * 1024 * 1024, rfDetrQueryCount: contract.GetProperty("queryCount").GetInt32());
        VisualModelProfile profile = task == "keypoint" ? RFDETRKeypointProfiles.CreatePreview(id, options) : task == "segment" ? PortableDetectorProfiles.CreateRFDETRSeg(id, options).VisualProfile : throw new ArgumentException("Only segment and keypoint export contracts are supported.");
        // The exported artifact has concrete shapes even when the reusable decoder accepts arbitrary mask dimensions.
        var outputs = contract.GetProperty("outputs").EnumerateArray().Select(port => new VisualOutputBinding(port.GetProperty("name").GetString()!, TensorElementType.Float32,
            new TensorShape(port.GetProperty("shape").EnumerateArray().Select(value => value.GetInt64()).ToArray()))).ToArray();
        return new VisualModelProfile(profile.ProfileId, profile.ModelId, profile.Task, profile.Version, profile.ModelFormat, profile.Input, outputs, profile.Labels, profile.Decoder, preprocessing: profile.Preprocessing);
    }

    private static BackendRequest Register(BackendRegistry registry, VisualModelProfile profile, string backend)
    {
        switch (backend)
        {
            case "ort":
            case "cuda":
                registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, executionProvider: backend == "cuda" ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu,
                    cudaProviderOptions: backend == "cuda" ? new Dictionary<string, string> { ["use_tf32"] = "0" } : null));
                return new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, backend == "cuda" ? "cuda" : "cpu");
            case "openvino":
                registry.UseOpenVino(new OpenVinoOptions(inferenceThreads: 4));
                return new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            case "opencv":
                registry.UseOpenCvDnn(new OpenCvDnnOptions(new OpenCvDnnModelContract(profile.ModelId,
                    new[] { new TensorDescriptor(profile.Input.Name, TensorElementType.Float32, profile.Input.ShapePattern) },
                    profile.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), imageInputNames: new[] { profile.Input.Name })));
                return new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            case "tensorrt":
                registry.UseTensorRT(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE")));
                return new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            default: throw new ArgumentException("Unknown backend: " + backend);
        }
    }

    private static VisualModelProfile CloneFormat(VisualModelProfile profile, string format) => new VisualModelProfile(profile.ProfileId, profile.ModelId, profile.Task, profile.Version, format,
        profile.Input, profile.Outputs, profile.Labels, profile.Decoder, preprocessing: profile.Preprocessing);

    private static VisualModelProfile WithDecoder(VisualModelProfile profile, IVisualDecoder decoder) => new VisualModelProfile(profile.ProfileId, profile.ModelId, profile.Task, profile.Version, profile.ModelFormat,
        profile.Input, profile.Outputs, profile.Labels, decoder, preprocessing: profile.Preprocessing);

    private static PreparedVisualInput RawInput(string path, VisualModelProfile profile, JsonElement contract)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != profile.Input.ShapePattern.GetElementCount() * sizeof(float)) throw new InvalidDataException("Raw input shape mismatch.");
        var values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        var dimensions = contract.GetProperty("sourceSize").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        var source = new VisualSize(dimensions[0], dimensions[1]);
        var size = profile.Preprocessing!.ModelSize!.Value;
        return new PreparedVisualInput(profile.Input.Name, new Tensor<float>(profile.Input.ShapePattern, values), source, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, size));
    }

    private static void Validate(object result, string referenceDirectory)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(referenceDirectory, "reference.json")));
        JsonElement expected = document.RootElement;
        var scores = expected.GetProperty("scores").EnumerateArray().Select(value => value.GetSingle()).ToArray();
        var labels = expected.GetProperty("labels").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        if (Count(result) != scores.Length) throw new InvalidDataException($"Official reference count differs: expected {scores.Length}, actual {Count(result)}.");
        float maximumScoreError = 0, maximumCoordinateError = 0;
        byte[] masks = result is InstanceSegmentationResult ? File.ReadAllBytes(Path.Combine(referenceDirectory, "reference-masks.u8")) : Array.Empty<byte>();
        long mismatch = 0;
        var unused = new HashSet<int>(Enumerable.Range(0, scores.Length));
        for (int index = 0; index < Count(result); index++)
        {
            PoseInstance? pose = result is PoseEstimationResult poses ? poses.Instances[index] : null;
            InstanceSegmentationInstance? instance = result is InstanceSegmentationResult segmentation ? segmentation.Instances[index] : null;
            var box = pose?.BoundingBox ?? instance!.BoundingBox;
            int label = pose?.ClassIndex ?? instance!.ClassIndex;
            float score = pose?.Score ?? instance!.Score;
            int match = unused.Where(value => labels[value] == label).OrderBy(value => Math.Abs(scores[value] - score)).First();
            unused.Remove(match);
            float[] expectedBox = expected.GetProperty("boxes")[match].EnumerateArray().Select(value => value.GetSingle()).ToArray();
            maximumScoreError = Math.Max(maximumScoreError, Math.Abs(scores[match] - score));
            float[] actualBox = { box.X, box.Y, box.Right, box.Bottom };
            for (int coordinate = 0; coordinate < 4; coordinate++) maximumCoordinateError = Math.Max(maximumCoordinateError, Math.Abs(actualBox[coordinate] - expectedBox[coordinate]));
            if (pose != null) for (int point = 0; point < 17; point++)
            {
                JsonElement target = expected.GetProperty("keypoints")[match][point];
                maximumCoordinateError = Math.Max(maximumCoordinateError, Math.Max(Math.Abs(pose.Keypoints[point].Point.X - target[0].GetSingle()), Math.Abs(pose.Keypoints[point].Point.Y - target[1].GetSingle())));
                maximumScoreError = Math.Max(maximumScoreError, Math.Abs(pose.Keypoints[point].Score - target[2].GetSingle()));
            }
            if (instance != null)
            {
                byte[] actual = instance.Mask.ToArray();
                for (int pixel = 0; pixel < actual.Length; pixel++) if ((actual[pixel] != 0) != (masks[match * actual.Length + pixel] != 0)) mismatch++;
            }
        }
        double ratio = masks.Length == 0 ? 0 : (double)mismatch / masks.Length;
        Console.WriteLine($"officialReference=count:{scores.Length};scoreMax:{maximumScoreError:R};coordinateMax:{maximumCoordinateError:R};maskMismatchRatio:{ratio:R}");
        if (maximumScoreError > .002f || maximumCoordinateError > 1f || ratio > .0001) throw new InvalidDataException("Official reference tolerance exceeded (score 0.002, coordinate 1px, masks 0.01%).");
    }

    private static object WritePose(string image, string directory, PoseEstimationResult result)
    {
        using Mat canvas = Codecs.ImRead(image);
        foreach (PoseInstance pose in result.Instances)
        {
            foreach (PoseSkeletonEdge edge in result.Topology.Edges)
            {
                PoseKeypoint left = pose.Keypoints[edge.FirstKeypointIndex], right = pose.Keypoints[edge.SecondKeypointIndex];
                if (left.Score > .4 && right.Score > .4) Drawing.Line(canvas, Point(left), Point(right), new Scalar(80, 220, 80), 3);
            }
            foreach (PoseKeypoint point in pose.Keypoints.Where(value => value.Score > .4)) Drawing.Circle(canvas, Point(point), 4, new Scalar(40, 60, 255), -1);
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        return new { topology = result.Topology.Keypoints.Select(value => value.Label), instances = result.Instances.Select(value => new { value.SourceIndex, value.ClassIndex, value.Score, value.BoundingBox, keypoints = value.Keypoints.Select(point => new { point.Index, point.Point.X, point.Point.Y, point.Score, point.IsValid, visibility = point.Visibility.ToString() }) }) };
    }

    private static Point Point(PoseKeypoint point) => new Point((int)Math.Round(point.Point.X), (int)Math.Round(point.Point.Y));

    private static object WriteSegmentation(string image, string directory, InstanceSegmentationResult result)
    {
        using Mat canvas = Codecs.ImRead(image);
        var records = new List<object>();
        for (int index = 0; index < result.Instances.Count; index++)
        {
            InstanceSegmentationInstance item = result.Instances[index];
            byte[] mask = item.Mask.ToArray();
            using var bitmap = new Mat(item.Mask.Height, item.Mask.Width, MatType.CV_8UC1);
            int step = checked((int)canvas.Step.ToUInt64()), maskStep = checked((int)bitmap.Step.ToUInt64());
            var row = new byte[canvas.Cols * 3];
            var maskRow = new byte[canvas.Cols];
            for (int y = 0; y < canvas.Rows; y++)
            {
                IntPtr address = IntPtr.Add(canvas.Data, y * step);
                Marshal.Copy(address, row, 0, row.Length);
                for (int x = 0; x < canvas.Cols; x++)
                {
                    bool foreground = mask[y * canvas.Cols + x] != 0;
                    maskRow[x] = foreground ? (byte)255 : (byte)0;
                    if (foreground) { row[x * 3] = (byte)(row[x * 3] * .6); row[x * 3 + 1] = (byte)(row[x * 3 + 1] * .6 + 90); }
                }
                Marshal.Copy(row, 0, address, row.Length);
                Marshal.Copy(maskRow, 0, IntPtr.Add(bitmap.Data, y * maskStep), maskRow.Length);
            }
            string name = $"mask-{index:D2}.png";
            Save(Path.Combine(directory, name), bitmap);
            File.WriteAllBytes(Path.Combine(directory, $"mask-{index:D2}.u8"), mask);
            records.Add(new { item.SourceIndex, item.ClassIndex, item.Label, item.Score, item.BoundingBox, mask = name });
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        return new { instances = records };
    }

    private static void Save(string path, Mat image)
    {
        if (!Codecs.ImWrite(path, image)) throw new IOException("Could not save " + path);
    }
}
