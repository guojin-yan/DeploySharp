using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

string Option(string key,string fallback)
{
    int i=Array.IndexOf(args,key);
    return i<0 ? fallback : i+1<args.Length ? args[i+1] : throw new ArgumentException("Missing value: "+key);
}
int Integer(string key,int fallback)=>int.Parse(Option(key,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
string model=Path.GetFullPath(Option("--model","models/yolo26n.onnx"));
string backend=Option("--backend","ort-cpu");
string output=Path.GetFullPath(Option("--output","results/"+backend));
string images=Path.GetFullPath(Option("--images","demo-inputs"));
int sessions=Integer("--sessions",1), threads=Integer("--threads",2), window=Integer("--window",640);
int trtApi=Integer("--trt-api",OperatingSystem.IsWindows()?10:11);
float overlap=float.Parse(Option("--overlap","0.2"),CultureInfo.InvariantCulture);
if(sessions is <1 or >8 || threads is <1 or >128 || window is <64 or >8192 || trtApi is not (10 or 11))
    throw new ArgumentException("Use sessions 1..8, threads 1..128, window 64..8192, trt-api 10 or 11.");
if(!File.Exists(model))throw new FileNotFoundException("Download the model first.",model);
string[] files=Directory.GetFiles(images,"*.jpg").Order(StringComparer.Ordinal).ToArray();
if(files.Length==0)throw new InvalidDataException("No JPG images.");
string engine=Path.GetFullPath(Option("--engine",Path.ChangeExtension(model,
    $".{(OperatingSystem.IsWindows()?"win":"linux")}-trt{trtApi}-fp32-b1.engine")));
Directory.CreateDirectory(output);
using var cancellation=new CancellationTokenSource();
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
var token=cancellation.Token;
using var runtime=new DemoRuntime(backend,model,engine,sessions,threads,trtApi);
var factory=new OpenCvVisualInputFactory();
var runner=new SlidingWindowDetectionRunner(runtime.Pipeline);
var options=new SlidingWindowDetectionOptions(new VisualSize(window,window),overlap:overlap,
    globalIouThreshold:.45f,nmsMode:DetectionNmsMode.ClassAware,maximumWindows:128,maximumDetections:1000,
    includeFullImagePass:!args.Contains("--no-full-pass"),coordinateMode:SlidingWindowCoordinateMode.Source,
    maximumPreparedPixels:64_000_000);
var identity=new { deploySharp="2.0.0",backend,sessions,threads,trtApi,model,artifact=runtime.Artifact,
    sourceRevision=Option("--revision","unspecified"),precision="FP32, TF32 disabled on CUDA/TensorRT",
    preprocessing="1x3x640x640 RGB/Letterbox/divide255/padding114",scoreThreshold=.25f,
    window,overlap,options.IncludeFullImagePass,options.MaximumWindows,options.MaximumPreparedPixels,
    environment=new{os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,
        architecture=RuntimeInformation.ProcessArchitecture.ToString(),logicalProcessors=Environment.ProcessorCount},
    modelSha256=Hash(File.ReadAllBytes(model)),artifactSha256=Hash(File.ReadAllBytes(runtime.Artifact)) };
if(args.Contains("--benchmark"))
{
    await SlidingBenchmark.RunAsync(runtime,files,options,output,Integer("--warmup",5),Integer("--iterations",50),identity,token);
    return;
}

var reports=new List<object>();
foreach(string file in files)
{
    token.ThrowIfCancellationRequested();
    string name=Path.GetFileNameWithoutExtension(file),folder=Path.Combine(output,name);
    Directory.CreateDirectory(folder);
    using var decoded=factory.DecodeForRois(OpenCvImageSource.FromFile(file),token);
    var size=decoded.SourceSize;
    var whole=new RectangleRoiGeometry(new RectangleF(0,0,size.Width,size.Height));
    DetectionResult full;
    using(var input=decoded.PrepareRectangle(whole,"images",runtime.Preprocessing,token))
        full=(DetectionResult)runtime.Pipeline.Run(input).Value;
    var fullRows=full.Detections.Select(d=>DetectionRow.From(d)).ToArray();
    SlidingDrawing.Save(file,Path.Combine(folder,"01-full.jpg"),fullRows);

    var settings=new List<(string Name,SlidingWindowDetectionOptions Options)>{("sliding",options)};
    if(args.Contains("--compare"))
    {
        settings.Add(("no-full",MakeOptions(window,overlap,false)));
        settings.Add(("zero-overlap",MakeOptions(window,0,false)));
    }
    var runs=new List<object>();
    foreach(var setting in settings)
    {
        runtime.Decoder.Capture=true;
        SlidingWindowDetectionResult result;
        WindowOutput[] raw;
        try
        {
            result=await runner.RunAsync(size,setting.Options,(tile,ct)=>
            {
                var input=decoded.PrepareRectangle(new RectangleRoiGeometry(tile.Bounds),"images",runtime.Preprocessing,ct);
                runtime.Decoder.Attach(input,tile); return input;
            },cancellationToken:token);
        }
        finally { runtime.Decoder.Capture=false; raw=runtime.Decoder.Drain(); }
        var rows=result.DetectionsWithProvenance.Select(d=>DetectionRow.From(d.Detection,d.Window.Index)).ToArray();
        var rawRows=raw.SelectMany(x=>x.Detections).ToArray();
        EnsureBoxes(rows,size); EnsureBoxes(rawRows,size);
        EnsureCoverage(result.Windows,size,setting.Options.IncludeFullImagePass);
        if(raw.Length!=result.WindowCount)throw new InvalidDataException("Missing window trace.");
        SlidingDrawing.Save(file,Path.Combine(folder,setting.Name+"-windows.jpg"),Array.Empty<DetectionRow>(),result.Windows);
        SlidingDrawing.Save(file,Path.Combine(folder,setting.Name+"-raw.jpg"),rawRows);
        SlidingDrawing.Save(file,Path.Combine(folder,setting.Name+"-merged.jpg"),rows);
        runs.Add(new {route=setting.Name,setting.Options.Overlap,setting.Options.IncludeFullImagePass,
            windows=raw,rawCount=rawRows.Length,mergedCount=rows.Length,detections=rows});
        Console.WriteLine($"{name}/{setting.Name}: windows={result.WindowCount}, raw={rawRows.Length}, merged={rows.Length}, full={fullRows.Length}");
    }
    if(args.Contains("--roi"))
    {
        float[] values=Option("--roi","").Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        if(values.Length!=4)throw new ArgumentException("--roi x,y,width,height");
        var geometry=new RectangleRoiGeometry(new RectangleF(values[0],values[1],values[2],values[3]));
        var roi=new VisualRoi("inspection",geometry,executionMode:RoiExecutionMode.SlidingWindow);
        var result=await runner.RunRoiAsync(size,roi,geometry,options,
            (tile,ct)=>Task.FromResult(decoded.PrepareRectangle(new RectangleRoiGeometry(tile.Bounds),"images",runtime.Preprocessing,ct)),
            cancellationToken:token);
        var rows=result.DetectionsWithProvenance.Select(d=>DetectionRow.From(d.Detection,d.Window.Index)).ToArray();
        EnsureBoxes(rows,size);
        SlidingDrawing.Save(file,Path.Combine(folder,"roi-merged.jpg"),rows,result.Windows);
        runs.Add(new{route="roi",roiId=roi.Id,geometry=BoxRow.From(geometry.Bounds),windows=result.Windows.Select(w=>BoxRow.From(w.Bounds)),detections=rows});
    }
    var report=new {image=Path.GetFileName(file),inputSha256=Hash(File.ReadAllBytes(file)),source=new{width=size.Width,height=size.Height},full=fullRows,runs};
    File.WriteAllText(Path.Combine(folder,"detections.json"),JsonSerializer.Serialize(report,JsonDefaults.Options));
    reports.Add(report);
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{createdAt=DateTimeOffset.Now,identity,images=reports},JsonDefaults.Options));
Console.WriteLine("Saved: "+output);

static SlidingWindowDetectionOptions MakeOptions(int size,float overlap,bool full)=>new(new VisualSize(size,size),overlap,
    globalIouThreshold:.45f,maximumWindows:128,maximumDetections:1000,includeFullImagePass:full,
    coordinateMode:SlidingWindowCoordinateMode.Source,maximumPreparedPixels:64_000_000);
static void EnsureBoxes(IEnumerable<DetectionRow> rows,VisualSize size)
{
    foreach(var b in rows)if(!float.IsFinite(b.X+b.Y+b.Width+b.Height+b.Score) || b.Width<=0 || b.Height<=0 ||
        b.X<-.01 || b.Y<-.01 || b.X+b.Width>size.Width+.01 || b.Y+b.Height>size.Height+.01)
        throw new InvalidDataException("Detection outside source coordinates.");
}
static void EnsureCoverage(IReadOnlyList<SlidingWindow> all,VisualSize size,bool fullPass)
{
    var tiles=all.Skip(fullPass?1:0).Select(w=>w.Bounds).ToArray();
    float[] bands=tiles.SelectMany(b=>new[]{b.Y,b.Bottom}).Distinct().Order().ToArray();
    if(bands[0]!=0 || bands[^1]!=size.Height)throw new InvalidDataException("Vertical coverage gap.");
    for(int i=1;i<bands.Length;i++)
    {
        float y=(bands[i-1]+bands[i])/2,right=0;
        foreach(var b in tiles.Where(b=>b.Y<=y && b.Bottom>y).OrderBy(b=>b.X))
        { if(b.X>right)throw new InvalidDataException("Horizontal coverage gap."); right=Math.Max(right,b.Right); }
        if(right!=size.Width)throw new InvalidDataException("Right edge not covered.");
    }
}
static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
}
