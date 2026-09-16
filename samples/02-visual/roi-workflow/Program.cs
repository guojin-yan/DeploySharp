using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;

internal static class Program
{
    private static int Main()
    {
        VisualSize source = new VisualSize(1920, 1080);
        var snapshot = new VisualRoiSnapshot(source, new[]
        {
            new VisualRoi(
                "inspection-zone",
                new PolygonRoiGeometry(new[]
                {
                    new PointF(.08f, .12f), new PointF(.92f, .12f),
                    new PointF(.88f, .90f), new PointF(.12f, .90f)
                }),
                RoiCoordinateSpace.Normalized,
                priority: 10,
                executionMode: RoiExecutionMode.FilterResults,
                taskFilter: new[] { VisualTaskId.ObjectDetection }),
            new VisualRoi(
                "ignore-label",
                new RectangleRoiGeometry(new RectangleF(.45f, .35f, .10f, .20f)),
                RoiCoordinateSpace.Normalized,
                inclusionMode: RoiInclusionMode.Exclude,
                executionMode: RoiExecutionMode.FilterResults,
                taskFilter: new[] { VisualTaskId.ObjectDetection })
        });

        var manager = new VisualRoiManager(snapshot);
        string json = VisualRoiJsonSerializer.Serialize(manager.Snapshot);
        VisualRoiSnapshot installed = VisualRoiJsonSerializer.LoadAndReplace(manager, json);

        var detections = new DetectionResult(new[]
        {
            new Detection(new RectangleF(220, 180, 160, 180), new LabelScore(0, "part", .96f)),
            new Detection(new RectangleF(900, 500, 120, 160), new LabelScore(0, "part", .91f)),
            new Detection(new RectangleF(1300, 390, 100, 150), new LabelScore(0, "part", .89f))
        });
        RoiDetectionResult kept = VisualRoiDetectionFilter.Filter(detections, installed, VisualTaskId.ObjectDetection);
        if (kept.Detections.Count != 2) throw new InvalidOperationException("ROI Include/Exclude filtering failed.");

        var processor = new VisualRoiEventProcessor(
            installed,
            VisualTaskId.ObjectDetection,
            new[] { new VisualRoiLine("exit-line", "inspection-zone", new PointF(960, 100), new PointF(960, 980)) },
            new VisualRoiEventOptions(dwellDuration: TimeSpan.FromMilliseconds(100)));
        DateTimeOffset start = DateTimeOffset.UtcNow;
        processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(700, 450, 40, 40), start));
        IReadOnlyList<VisualRoiEvent> events = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1100, 450, 40, 40), start.AddMilliseconds(120)));

        var videoProcessor = new VisualRoiEventProcessor(
            installed,
            VisualTaskId.ObjectDetection,
            new[] { new VisualRoiLine("exit-line", "inspection-zone", new PointF(960, 100), new PointF(960, 980)) },
            new VisualRoiEventOptions(missingTrackTimeout: TimeSpan.FromMilliseconds(100)));
        VisualRoiVideoRunReport video = new VisualRoiVideoRunner<int>().RunAsync(
            Enumerable.Range(0, 8),
            (frame, _) => Task.FromResult<IReadOnlyList<VisualRoiTrackObservation>>(
                frame < 4
                    ? new[] { new VisualRoiTrackObservation("video-track", new RectangleF(700 + (frame * 120), 450, 40, 40), start.AddMilliseconds(frame * 33), frame) }
                    : Array.Empty<VisualRoiTrackObservation>()),
            frame => start.AddMilliseconds(frame * 33),
            videoProcessor,
            new VisualRoiVideoRunOptions(maximumFrames: 8),
            cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

        Console.WriteLine($"DEPLOYSHARP_VISUAL_ROI_OK snapshot={manager.Snapshot.Version} jsonBytes={json.Length} kept={kept.Detections.Count} events={events.Count} videoFrames={video.ProcessedFrames} videoEvents={video.EmittedEventCount} peakTracks={video.PeakActiveTrackCount} counts={string.Join(",", processor.GetCounts().Where(value => value.Value > 0).Select(value => value.Key + "=" + value.Value))}");
        return 0;
    }
}
