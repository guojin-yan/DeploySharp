using System.Collections.Concurrent;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

internal sealed class DemoRuntime : IDisposable
{
    private readonly BackendRegistry registry = new();
    public VisualPipeline Pipeline { get; }
    public CaptureDecoder Decoder { get; } = new();
    public OpenCvPreprocessOptions Preprocessing { get; }
    public string Artifact { get; }

    public DemoRuntime(string backend, string model, string engine, int sessions, int threads, int trtApi)
    {
        var id = new ModelId("blog/yolo26-sliding");
        BackendId backendId;
        string device, format = "onnx";
        Artifact = model;
        try
        {
            if (backend is "ort-cpu" or "ort-cuda")
            {
                registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: threads, interOpThreads: 1,
                    executionProvider: backend == "ort-cuda" ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu,
                    cudaProviderOptions: backend == "ort-cuda" ? new Dictionary<string,string> { ["use_tf32"]="0" } : null));
                backendId = OnnxRuntimeBackendProvider.BackendId; device = backend == "ort-cuda" ? "cuda" : "cpu";
            }
            else if (backend == "openvino")
            {
                registry.UseOpenVino(new OpenVinoOptions(device: "CPU", performanceHint: OpenVinoPerformanceHint.Latency,
                    streams: 1, inferenceThreads: threads, requestCount: 1));
                backendId = OpenVinoBackendProvider.BackendId; device = "CPU";
            }
            else if (backend == "opencv")
            {
                registry.UseOpenCvDnn(new OpenCvDnnOptions(new OpenCvDnnModelContract(id,
                    new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1,3,640,640)) },
                    new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1,300,6)) }), numThreads: threads));
                backendId = OpenCvDnnBackendProvider.BackendId; device = "cpu";
            }
            else if (backend == "tensorrt")
            {
                var api = (TensorRtApiVersion)trtApi;
                if (!File.Exists(engine))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(engine)!);
                    Console.WriteLine("Building local FP32 engine: " + engine);
                    new TensorRtOnnxEngineBuilder().Build(new ModelArtifact(id,"onnx",model), engine,
                        new TensorRtOnnxEngineBuildOptions(apiVersion: api,
                            precision: trtApi == 10 ? TensorRtOnnxEnginePrecision.Float32 : TensorRtOnnxEnginePrecision.RuntimeDefault,
                            disableTf32: true, optimizationLevel: 3));
                }
                registry.UseTensorRT(new TensorRtBackendOptions(api));
                backendId = TensorRtBackendProvider.BackendId; device = "cuda";
                Artifact = engine; format = "tensorrt-engine";
            }
            else throw new ArgumentException("Use ort-cpu, ort-cuda, openvino, opencv or tensorrt.");

            var preprocessing = new VisualPreprocessingOptions(new VisualSize(640,640), VisualResizeMode.Letterbox,
                VisualColorOrder.Rgb, VisualNormalizationOptions.Scale(255f), paddingColor: new VisualRgbColor(114,114,114));
            Preprocessing = preprocessing.ToOpenCvOptions();
            var profile = new VisualModelProfile("blog.yolo26.sliding",id,VisualTaskId.ObjectDetection,"1",format,
                new VisualInputBinding("images",TensorElementType.Float32,new TensorShape(1,3,640,640),VisualTensorLayout.Nchw),
                new[] {new VisualOutputBinding("output0",TensorElementType.Float32,new TensorShape(1,300,6))},
                YoloLabelSets.Coco80.Select((name,index)=>new VisualLabel(index,name)), Decoder, preprocessing: preprocessing);
            var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference,backendId,device);
            var selection = profiles.Select(new ModelArtifact(id,format,Artifact),registry,request,VisualTaskId.ObjectDetection);
            Pipeline = new VisualPipeline(registry,selection,request,new SessionOptions(maxConcurrency:sessions));
        }
        catch { registry.Dispose(); throw; }
    }
    public void Dispose() { Pipeline.Dispose(); registry.Dispose(); }
}

// Observes the published decoder without changing its thresholds, tensor contract or results.
// Capture is used for illustration only; benchmarks leave it disabled.
internal sealed class CaptureDecoder : IVisualDecoder
{
    private readonly YoloDetectionDecoder inner = new(new YoloDetectionOutputContract("output0",YoloDetectionOutputKind.EndToEnd,80),
        new YoloDetectionDecoderOptions(scoreThreshold:.25f));
    private readonly ConcurrentDictionary<PreparedVisualInput,SlidingWindow> pending = new();
    private readonly ConcurrentQueue<WindowOutput> outputs = new();
    public VisualTaskId Task => VisualTaskId.ObjectDetection;
    public bool Capture { get; set; }
    public void Attach(PreparedVisualInput input, SlidingWindow window) { if(Capture) pending.TryAdd(input,window); }
    public WindowOutput[] Drain()
    {
        var result=outputs.ToArray().OrderBy(x=>x.Index).ToArray();
        outputs.Clear(); pending.Clear(); return result;
    }
    public object Decode(VisualDecodeContext context)
    {
        var result=(DetectionResult)inner.Decode(context);
        if(Capture && pending.TryRemove(context.Input,out var window))
        {
            var tensor=context.GetOutput<float>("output0");
            var values=(float[])tensor.Buffer;
            if(tensor.Shape.ToString()!="[1,300,6]" || values.Any(value=>!float.IsFinite(value)))
                throw new InvalidDataException("Unexpected output shape or non-finite model output.");
            outputs.Enqueue(new WindowOutput(window.Index,BoxRow.From(window.Bounds),tensor.Shape.ToString(),
                (float[])values.Clone(),
                result.Detections.Select(d=>DetectionRow.From(d,window.Index)).ToArray()));
        }
        return result;
    }
}

internal sealed record BoxRow(float X,float Y,float Width,float Height)
{
    public static BoxRow From(JYPPX.DeploySharp.Geometry.RectangleF b)=>new(b.X,b.Y,b.Width,b.Height);
}
internal sealed record DetectionRow(int ClassId,string Label,float Score,float X,float Y,float Width,float Height,int? WindowIndex)
{
    public static DetectionRow From(Detection d,int? window=null)=>new(d.Label.Index,d.Label.Label,d.Label.Score,
        d.Box.X,d.Box.Y,d.Box.Width,d.Box.Height,window);
}
internal sealed record WindowOutput(int Index,BoxRow Bounds,string OutputShape,float[] RawValues,DetectionRow[] Detections);
