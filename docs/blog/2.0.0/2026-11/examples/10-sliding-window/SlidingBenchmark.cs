using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class SlidingBenchmark
{
    public static async Task RunAsync(DemoRuntime runtime,string[] files,SlidingWindowDetectionOptions options,
        string output,int warmup,int iterations,object identity,CancellationToken token)
    {
        if(warmup<5 || iterations<50 || iterations>10000)throw new ArgumentException("Use at least 5 warmups and 50 measured calls; maximum 10000 iterations.");
        var factory=new OpenCvVisualInputFactory();
        var runner=new SlidingWindowDetectionRunner(runtime.Pipeline);
        var process=Process.GetCurrentProcess();
        var cases=new List<object>();
        string[] gpuBefore=GpuSnapshot();
        foreach(string file in files)
        {
            byte[] bytes=File.ReadAllBytes(file);
            var source=OpenCvImageSource.FromBytes(bytes);
            var full=new List<Sample>(iterations);
            var sliding=new List<Sample>(iterations);
            for(int round=-warmup;round<iterations;round++)for(int offset=0;offset<2;offset++)
            {
                token.ThrowIfCancellationRequested();
                bool tiled=((round+warmup)+offset)%2==1;
                long allocation=GC.GetTotalAllocatedBytes(true);
                long start=Stopwatch.GetTimestamp();
                var decoded=factory.DecodeForRois(source,token);
                long decodeDone=Stopwatch.GetTimestamp();
                int count,windows;
                try
                {
                    if(tiled)
                    {
                        var result=await runner.RunAsync(decoded.SourceSize,options,
                            (tile,ct)=>decoded.PrepareRectangle(new RectangleRoiGeometry(tile.Bounds),"images",runtime.Preprocessing,ct),
                            cancellationToken:token);
                        count=result.Detections.Detections.Count; windows=result.WindowCount;
                    }
                    else
                    {
                        using var input=decoded.PrepareRectangle(new RectangleRoiGeometry(new RectangleF(0,0,
                            decoded.SourceSize.Width,decoded.SourceSize.Height)),"images",runtime.Preprocessing,token);
                        count=((DetectionResult)runtime.Pipeline.Run(input).Value).Detections.Count; windows=1;
                    }
                }
                finally { decoded.Dispose(); }
                long done=Stopwatch.GetTimestamp();
                long allocated=GC.GetTotalAllocatedBytes(true)-allocation;
                if(round>=0)(tiled?sliding:full).Add(new Sample(Ms(start,done),Ms(start,decodeDone),Ms(decodeDone,done),allocated,count,windows));
            }
            process.Refresh();
            var summary=new {full=Stats(full.Select(s=>s.TotalMs)),sliding=Stats(sliding.Select(s=>s.TotalMs)),
                fullMeanAllocatedBytes=full.Average(s=>s.ManagedAllocatedBytes),slidingMeanAllocatedBytes=sliding.Average(s=>s.ManagedAllocatedBytes),
                workingSetBytes=process.WorkingSet64,processCumulativePeakWorkingSetBytes=process.PeakWorkingSet64};
            cases.Add(new{image=Path.GetFileName(file),inputSha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                summary,fullSamples=full,slidingSamples=sliding});
            Console.WriteLine($"{Path.GetFileName(file)}: full_p50={summary.full.P50:F3} ms, sliding_p50={summary.sliding.P50:F3} ms, windows={sliding[0].Windows}");
        }
        File.WriteAllText(Path.Combine(output,"benchmark.json"),JsonSerializer.Serialize(new{
            createdAt=DateTimeOffset.Now,identity,warmupPerRoute=warmup,iterationsPerRoute=iterations,
            protocol="Reuse encoded bytes; decode once per image/route. Include preparation, device transfer, synchronous-completed inference, decoder, source-coordinate merge/global NMS and release. Exclude file IO, drawing, trace recording, model/Engine creation. Full and sliding routes alternate. Decode and prepare/infer/merge wall stages are grouped; no per-tile percentile sums. Process peak is cumulative; managed allocations exclude native/GPU allocations; GPU memory is sampled outside timing, not peak memory.",
            gpuBefore,gpuAfter=GpuSnapshot(),cases},JsonDefaults.Options));
    }
    private static double Ms(long a,long b)=>Stopwatch.GetElapsedTime(a,b).TotalMilliseconds;
    private static Distribution Stats(IEnumerable<double> source)
    {
        double[] x=source.Order().ToArray();
        return new(x.Average(),x[(int)Math.Ceiling(x.Length*.5)-1],x[(int)Math.Ceiling(x.Length*.95)-1],x[0],x[^1]);
    }
    private static string[] GpuSnapshot()
    {
        try
        {
            var start=new ProcessStartInfo("nvidia-smi"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("--query-gpu=name,driver_version,pstate,clocks.current.graphics,clocks.current.memory,utilization.gpu,memory.used,power.draw");
            start.ArgumentList.Add("--format=csv,noheader");
            using var p=Process.Start(start)!;
            var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(3000)){p.Kill(true);p.WaitForExit();return new[]{"nvidia-smi timeout"};}
            return (p.ExitCode==0?stdout.GetAwaiter().GetResult():stderr.GetAwaiter().GetResult()).Trim().Split('\n');
        }
        catch(Exception error)when(error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {return new[]{error.GetType().Name+": "+error.Message};}
    }
    private sealed record Sample(double TotalMs,double DecodeMs,double PrepareInferMergeReleaseMs,long ManagedAllocatedBytes,int Count,int Windows);
    private sealed record Distribution(double Mean,double P50,double P95,double Minimum,double Maximum);
}
