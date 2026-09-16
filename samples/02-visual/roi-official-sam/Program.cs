using System.Globalization;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;

// python family checkpoint video output source-revision [frame-count] [normalized x,y,w,h]
if (args.Length < 6)
{
    Console.WriteLine("OfficialSamRoi <python> <sam2|sam3> <checkpoint> <video> <output-directory> <official-commit> [frames=6] [roi=0.25,0.2,0.5,0.7]");
    return 2;
}
string family = args[1];
if (family != "sam2" && family != "sam3") throw new ArgumentException("Family must be sam2 or sam3.");
int frames = args.Length > 6 ? int.Parse(args[6], CultureInfo.InvariantCulture) : 6;
if (frames < 3 || frames > 100) throw new ArgumentOutOfRangeException(nameof(frames), "This short smoke sample accepts 3..100 frames.");
float[] region = (args.Length > 7 ? args[7] : "0.25,0.2,0.5,0.7").Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
if (region.Length != 4) throw new ArgumentException("ROI requires normalized x,y,width,height.");
string output = Path.GetFullPath(args[4]);
Directory.CreateDirectory(output);
PromptableSegmentationProfile profile = family == "sam2"
    ? PromptableSegmentationProfiles.CreateSam2VideoBlocker("official/sam2/roi", args[5], "Official PyTorch worker; not an ONNX backend session.", maximumObjects: 1, maximumFrames: frames)
    : PromptableSegmentationProfiles.CreateSam3VideoBlocker("official/sam3/roi", args[5], "Official PyTorch tracker; not an ONNX backend session.", maximumObjects: 1, maximumFrames: frames);
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; timeout.Cancel(); };
await using var predictor = new OfficialSamVideoPredictor(profile, args[0], new[]
{
    "--family", family, "--checkpoint", Path.GetFullPath(args[2]), "--video", Path.GetFullPath(args[3]),
    "--output", output, "--frames", frames.ToString(CultureInfo.InvariantCulture), "--revision", args[5]
});
JsonElement initialized = await predictor.InitializeAsync(timeout.Token);
int width = initialized.GetProperty("width").GetInt32();
int height = initialized.GetProperty("height").GetInt32();
var snapshot = new VisualRoiSnapshot(new VisualSize(width, height), new[]
{
    new VisualRoi("subject", new RectangleRoiGeometry(new RectangleF(region[0], region[1], region[2], region[3])),
        RoiCoordinateSpace.Normalized, taskFilter: new[] { VisualTaskId.PromptableVideoSegmentation })
});
var planner = new VisualRoiVideoPromptPlanner(profile);
var runner = new VisualRoiVideoPromptRunner<int, JsonElement>(planner, predictor);
var events = new VisualRoiEventProcessor(snapshot, VisualTaskId.PromptableVideoSegmentation,
    options: new VisualRoiEventOptions(dwellDuration: TimeSpan.FromMilliseconds(100)));
var results = new List<object>();
for (int frame = 0; frame < frames; frame++)
{
    var mode = frame == 0 ? VisualRoiVideoFrameMode.Initialize : frame == frames / 2 ? VisualRoiVideoFrameMode.Correct : VisualRoiVideoFrameMode.Propagate;
    var result = await runner.RunAsync(frame, snapshot, frame, mode, new VisualRoiPromptOptions(maximumPoints: 1), cancellationToken: timeout.Token);
    DateTimeOffset timestamp = DateTimeOffset.UnixEpoch.AddMilliseconds(frame * 1000.0 / initialized.GetProperty("fps").GetDouble());
    var tracks = result.Result.GetProperty("objects").EnumerateArray().Where(item => item.GetProperty("pixels").GetInt64() > 0).Select(item =>
    {
        float x = item.GetProperty("center_x").GetSingle();
        float y = item.GetProperty("center_y").GetSingle();
        return new VisualRoiTrackObservation(item.GetProperty("roi_id").GetString()!, new RectangleF(x - .5f, y - .5f, 1, 1), timestamp, frame);
    }).ToArray();
    var emitted = events.ProcessFrame(tracks).Concat(events.Advance(timestamp)).Select(item => new
    {
        Kind = item.Kind.ToString(), item.TrackId, item.RoiId, item.Timestamp, item.FrameIndex, item.Count
    }).ToArray();
    results.Add(new { Frame = frame, Mode = mode.ToString(), WallMilliseconds = result.Elapsed.TotalMilliseconds, Prediction = result.Result, Events = emitted });
    Console.WriteLine($"OFFICIAL_SAM_ROI_FRAME family={family} frame={frame} mode={mode} masks={tracks.Length} elapsed_ms={result.Elapsed.TotalMilliseconds:F3}");
}
await runner.ResetAsync(timeout.Token);
if (planner.IsInitialized) throw new InvalidOperationException("Reset failed to clear planner state.");
var replay = await runner.RunAsync(0, snapshot, 0, VisualRoiVideoFrameMode.Initialize, new VisualRoiPromptOptions(maximumPoints: 1), cancellationToken: timeout.Token);
bool resetConsistent = JsonSerializer.Serialize(replay.Result.GetProperty("objects")) == JsonSerializer.Serialize(((JsonElement)JsonSerializer.SerializeToElement(results[0]).GetProperty("Prediction")).GetProperty("objects"));
if (!resetConsistent) throw new InvalidOperationException("Reset/reinitialize changed first-frame masks.");
await runner.ResetAsync(timeout.Token);
string report = Path.Combine(output, "report.json");
await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { SchemaVersion = 1, Status = "pass", Family = family, Initialization = initialized, Frames = results, ResetConsistent = resetConsistent, SourceRevision = args[5] }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
Console.WriteLine("DEPLOYSHARP_OFFICIAL_SAM_ROI_OK=" + report);
return 0;
