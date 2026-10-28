using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Tensors;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;

Console.OutputEncoding = Encoding.UTF8;
string imagePath = Path.GetFullPath(args.Length > 0 ? args[0] : "images/pp_structure_v3_demo.png");
string variant = args.Length > 1 ? args[1].ToLowerInvariant() : "v5-mobile";
string modelRoot = Path.GetFullPath(args.Length > 2 ? args[2] : "models");
string backend = args.Length > 3 ? args[3] : "ort-cpu";
string modelFormat = backend == "tensorrt" ? "tensorrt-engine" : "onnx";
using Mat sourceImage = ImageCodecs.ImRead(imagePath);
if (sourceImage.Empty) throw new IOException("Cannot decode input");
var preprocess = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(
    new VisualSize(sourceImage.Cols, sourceImage.Rows), resizeLong: 1600);
string outputDirectory = Path.GetFullPath(Path.Combine("results", backend, variant));
Directory.CreateDirectory(outputDirectory);
const int batch = 1;
const int channels = 1;

var ids = variant switch
{
    "v4-mobile" => ("ppocrv4/mobile-det", "ppocrv4/legacy-cls", "ppocrv4/mobile-rec"),
    "v4-server" => ("ppocrv4/server-det", "ppocrv4/legacy-cls", "ppocrv4/server-rec"),
    "v5-mobile" => ("ppocrv5/mobile-det", "ppocrv5/mobile-cls", "ppocrv5/mobile-rec"),
    "v5-server" => ("ppocrv5/server-det", "ppocrv5/server-cls", "ppocrv5/server-rec"),
    "v6-tiny" => ("ppocrv6/tiny-det", "ppocrv5/mobile-cls", "ppocrv6/tiny-rec"),
    "v6-small" => ("ppocrv6/small-det", "ppocrv5/mobile-cls", "ppocrv6/small-rec"),
    "v6-medium" => ("ppocrv6/medium-det", "ppocrv5/mobile-cls", "ppocrv6/medium-rec"),
    _ => throw new ArgumentException("版本应为 v4-mobile/server、v5-mobile/server 或 v6-tiny/small/medium。")
};
PaddleOcrModelDescriptor detRow = Find(ids.Item1);
PaddleOcrModelDescriptor clsRow = Find(ids.Item2);
PaddleOcrModelDescriptor recRow = Find(ids.Item3);
string dictionaryPath = Require(recRow.ReleaseArtifact!.DictionaryAssetName!);
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    dictionaryPath, "local." + variant, "1", useSpaceCharacter: true);
PaddleOcrProfile det = CreateProfile(detRow, maximumBatch: 1);
PaddleOcrProfile cls = CreateProfile(clsRow, maximumBatch: batch);
PaddleOcrProfile rec = CreateProfile(recRow, maximumBatch: batch, characters);

using var backends = new BackendRegistry();
using var classificationBackends = new BackendRegistry();
using var recognitionBackends = new BackendRegistry();
BackendId backendId = backend switch
{
    "ort-cpu" => OnnxRuntimeBackendProvider.BackendId,
    "openvino" => OpenVinoBackendProvider.BackendId,
    "opencv-dnn" => OpenCvDnnBackendProvider.BackendId,
    "tensorrt" => TensorRtBackendProvider.BackendId,
    _ => throw new ArgumentException("Unknown backend: " + backend)
};
string device = backend == "tensorrt" ? "cuda" : "cpu";
Register(backends, detRow, det);
Register(classificationBackends, clsRow, cls);
Register(recognitionBackends, recRow, rec);
var profiles = new VisualProfileRegistry();
profiles.Register(det.VisualProfile);
profiles.Register(cls.VisualProfile);
profiles.Register(rec.VisualProfile);
profiles.Freeze();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    backendId, device);

using var pipeline = new OcrPipeline(
    backends,
    Select(det, detRow, backends), request,
    classificationBackends, Select(cls, clsRow, classificationBackends), request, cls.CropProfile!,
    recognitionBackends, Select(rec, recRow, recognitionBackends), request, rec.CropProfile!,
    new OcrPipelineOptions(maximumConcurrency: 1, maximumRegions: 512,
        maximumRecognitionBatch: batch, maximumRecognitionPaddingRatio: 1.0,
        autoRotateVerticalText: true),
    detectionSessionOptions: new SessionOptions(1),
    orientationSessionOptions: new SessionOptions(channels),
    recognitionSessionOptions: new SessionOptions(channels),
    orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);

// 此 Mat 只用于取得原图尺寸及绘图；OCR 输入在内部保留自己的源图。
if (!File.Exists(imagePath)) throw new FileNotFoundException("找不到图片。", imagePath);
using Mat canvas = ImageCodecs.ImRead(imagePath);
if (canvas.Empty) throw new IOException("无法解码输入图片。");
var sourceSize = new VisualSize(canvas.Cols, canvas.Rows);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
const int warmup = 2, repeats = 5;
var samples = new List<double>();
var prepareSamples = new List<double>();
var runSamples = new List<double>();
OcrResult result = null!;
for (int i = 0; i < warmup + repeats; i++)
{
    var total = Stopwatch.StartNew();
    var prepareWatch = Stopwatch.StartNew();
    using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory()
        .CreateFromFile(imagePath, det.VisualProfile.Input.Name, preprocess);
    prepareWatch.Stop();
    var runWatch = Stopwatch.StartNew();
    result = await pipeline.RunAsync(input,
        new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(120)), cancellation.Token);
    runWatch.Stop();
    total.Stop();
    if (i >= warmup)
    {
        samples.Add(total.Elapsed.TotalMilliseconds);
        prepareSamples.Add(prepareWatch.Elapsed.TotalMilliseconds);
        runSamples.Add(runWatch.Elapsed.TotalMilliseconds);
    }
}
double Percentile(List<double> values, double q) => values.Order().ElementAt((int)Math.Ceiling(q * values.Count) - 1);
Console.WriteLine(FormattableString.Invariant($"warmup={warmup};repeats={repeats};p50_ms={Percentile(samples, .5):F3};p95_ms={Percentile(samples, .95):F3}"));

Console.WriteLine($"backend={backend};device={device};variant={variant};batch={batch};channels={channels}");
Console.WriteLine($"regions={result.Regions.Count}");

for (int i = 0; i < result.Regions.Count; i++)
{
    OcrRegionResult item = result.Regions[i];
    if (i < 10)
        Console.WriteLine(FormattableString.Invariant(
            $"[{i + 1:D2}] score={item.Recognition.Confidence:F3};angle={OrientationDegrees(item.Region.Orientation)};{item.Recognition.Text}"));
    var points = item.Region.Polygon.Vertices;
    for (int j = 0; j < points.Count; j++)
    {
        var a = points[j];
        var b = points[(j + 1) % points.Count];
        Drawing.Line(canvas, new Point((int)a.X, (int)a.Y), new Point((int)b.X, (int)b.Y),
            new Scalar(80, 210, 30), 2);
    }
    var first = points[0];
    Drawing.PutText(canvas, (i + 1).ToString("D2"),
        new Point(Math.Clamp((int)first.X, 0, Math.Max(0, canvas.Cols - 35)),
            Math.Clamp((int)first.Y - 4, 18, Math.Max(18, canvas.Rows - 1))),
        HersheyFonts.HersheySimplex, 0.5, new Scalar(10, 10, 230), 1);
}
Console.WriteLine("console_excerpt=first_10_regions; complete_text_and_regions_saved_to_files");
File.WriteAllLines(Path.Combine(outputDirectory, "result.txt"), result.Regions.Select(x => x.Recognition.Text), new UTF8Encoding(false));
var report = new
{
    variant, backend, device, batch, channels, warmup, repeats,
    sourceSize = new { width = result.SourceSize.Width, height = result.SourceSize.Height },
    timing = new { prepareP50 = Percentile(prepareSamples, .5), runP50 = Percentile(runSamples, .5),
        totalP50 = Percentile(samples, .5), totalP95 = Percentile(samples, .95), samples },
    regions = result.Regions.Select((x, i) => new
    {
        index = i + 1, text = x.Recognition.Text, confidence = x.Recognition.Confidence,
        detectionScore = x.Region.Score, angle = OrientationDegrees(x.Region.Orientation),
        polygon = x.Region.Polygon.Vertices.Select(p => new { x = p.X, y = p.Y })
    })
};
File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
}), new UTF8Encoding(false));
if (!ImageCodecs.ImWrite(Path.Combine(outputDirectory, "result.jpg"), canvas)) throw new IOException("无法保存结果图。");
Console.WriteLine($"saved={outputDirectory}");

static int OrientationDegrees(TextOrientation orientation) => orientation switch
{
    TextOrientation.Degrees0 => 0,
    TextOrientation.Clockwise90 => 90,
    TextOrientation.Degrees180 => 180,
    TextOrientation.CounterClockwise90 => 270,
    _ => throw new ArgumentOutOfRangeException(nameof(orientation))
};

PaddleOcrModelDescriptor Find(string id) => PaddleOcrModelCatalog.Find(new ModelId("paddleocr/" + id));

string Require(string file)
{
    string path = Path.Combine(modelRoot, file);
    return File.Exists(path) ? path : throw new FileNotFoundException("缺少模型或字典。", path);
}

PaddleOcrProfile CreateProfile(PaddleOcrModelDescriptor row, int maximumBatch, OcrCharacterSet? dictionary = null)
{
    // 本地加载：使用目录中的模型规格，不要求填写模型来源元数据。
    var contract = new PaddleOcrArtifactContract(row.ReleaseArtifact!.Opset,
        "", "", "", "Apache-2.0", "local-input-v1", "local-output-v1", modelFormat);
    if (row.Family == "det")
        return PaddleOcrProfiles.CreateDetection(row.ModelId, contract, row.InputName, row.OutputName,
            postprocess: new PaddleDbPostprocessOptions(maximumRegions: 512), maximumBatch: maximumBatch);
    return PaddleOcrModelCatalog.CreateProfile(row, contract, dictionary, maximumBatch);
}

VisualProfileSelection Select(PaddleOcrProfile profile, PaddleOcrModelDescriptor row, BackendRegistry registry) =>
    profiles.Select(profile.CreateArtifact(ModelPath(row)), registry, request, profile.VisualProfile.Task);

string ModelPath(PaddleOcrModelDescriptor row) => backend == "tensorrt"
    ? Path.ChangeExtension(Require(row.ReleaseArtifact!.AssetName), ".ubuntu-trt11.engine")
    : Require(row.ReleaseArtifact!.AssetName);

void Register(BackendRegistry registry, PaddleOcrModelDescriptor row, PaddleOcrProfile profile)
{
    switch (backend)
    {
        case "ort-cpu":
            registry.UseOnnxRuntime(); break;
        case "openvino":
            registry.UseOpenVino(new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency)); break;
        case "opencv-dnn":
            TensorShape inputShape = row.Family == "det"
                ? new TensorShape(1, 3, preprocess.ModelSize.Height, preprocess.ModelSize.Width)
                : row.Family == "rec" ? new TensorShape(1, 3, 48, -1) : profile.VisualProfile.Input.ShapePattern;
            TensorShape outputShape = row.Family == "det"
                ? new TensorShape(1, 1, preprocess.ModelSize.Height, preprocess.ModelSize.Width)
                : row.Family == "rec" ? new TensorShape(1, -1, profile.VisualProfile.Outputs[0].ShapePattern[2])
                : profile.VisualProfile.Outputs[0].ShapePattern;
            registry.UseOpenCvDnn(new OpenCvDnnOptions(new OpenCvDnnModelContract(
                profile.VisualProfile.ModelId,
                new[] { new TensorDescriptor(row.InputName, TensorElementType.Float32, inputShape) },
                new[] { new TensorDescriptor(row.OutputName, TensorElementType.Float32, outputShape) }),
                maxCachedDynamicShapes: 16));
            break;
        case "tensorrt":
            string engine = ModelPath(row);
            if (!File.Exists(engine))
            {
                TensorShape min, opt, max;
                if (row.Family == "det")
                    min = opt = max = new TensorShape(1, 3, preprocess.ModelSize.Height, preprocess.ModelSize.Width);
                else if (row.Family == "cls")
                    min = opt = max = profile.VisualProfile.Input.ShapePattern;
                else
                {
                    min = new TensorShape(1, 3, 48, 48);
                    opt = new TensorShape(1, 3, 48, 320);
                    max = new TensorShape(1, 3, 48, 3200);
                }
                Console.WriteLine("building=" + Path.GetFileName(engine));
                new TensorRtOnnxEngineBuilder().Build(
                    new ModelArtifact(row.ModelId, "onnx", Require(row.ReleaseArtifact!.AssetName)), engine,
                    new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt11,
                        optimizationLevel: 3, disableTf32: true,
                        inputProfiles: new[] { new TensorRtOnnxInputProfile(row.InputName, min, opt, max) }));
            }
            registry.UseTensorRT(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11));
            break;
    }
}
