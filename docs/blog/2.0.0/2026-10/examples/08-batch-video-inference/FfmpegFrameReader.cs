using System.Diagnostics;
using System.Text.Json;
using JYPPX.OpenCvSharp.Core;

// The decoder is independent of the inference backend and the OpenCV video codec build.
internal sealed class FfmpegFrameReader : IDisposable
{
    private readonly Process _decoder;
    private readonly Task<string> _errors;
    private readonly CancellationTokenRegistration _cancel;
    public int Width { get; }
    public int Height { get; }

    public FfmpegFrameReader(string video, string ffmpeg, string ffprobe, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var probe = Process.Start(StartInfo(ffprobe, "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=width,height", "-of", "json", video))
            ?? throw new IOException("Cannot start ffprobe.");
        using var probeCancel = token.Register(() => Kill(probe));
        Task<string> probeError = probe.StandardError.ReadToEndAsync();
        string metadata = probe.StandardOutput.ReadToEnd(); probe.WaitForExit();
        token.ThrowIfCancellationRequested();
        if (probe.ExitCode != 0) throw new IOException("ffprobe: " + probeError.GetAwaiter().GetResult());
        using var json = JsonDocument.Parse(metadata);
        var stream = json.RootElement.GetProperty("streams")[0];
        Width = stream.GetProperty("width").GetInt32(); Height = stream.GetProperty("height").GetInt32();
        if (Width <= 0 || Height <= 0 || (long)Width * Height > 40_000_000) throw new InvalidDataException("Unsupported video dimensions.");
        _decoder = Process.Start(StartInfo(ffmpeg, "-nostdin", "-v", "error", "-threads", "1", "-noautorotate",
            "-i", video, "-map", "0:v:0", "-an", "-sn", "-pix_fmt", "bgr24", "-threads", "1", "-f", "rawvideo", "pipe:1"))
            ?? throw new IOException("Cannot start ffmpeg.");
        _errors = _decoder.StandardError.ReadToEndAsync();
        _cancel = token.Register(() => Kill(_decoder));
    }

    public unsafe bool Read(Mat image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        image.Create(Height, Width, MatType.CV_8UC3);
        int rowBytes = checked(Width * 3);
        int stride = checked((int)image.Step.ToUInt64());
        byte* data = (byte*)image.Data;
        // Read pipe bytes directly into independent native pixels; also handle row padding.
        int rows = stride == rowBytes ? 1 : Height;
        int bytesPerRead = rows == 1 ? checked(rowBytes * Height) : rowBytes;
        for (int y = 0; y < rows; y++)
        {
            var row = new Span<byte>(data + checked(y * stride), bytesPerRead);
            int filled = 0;
            while (filled < row.Length)
            {
                token.ThrowIfCancellationRequested();
                int read = _decoder.StandardOutput.BaseStream.Read(row[filled..]);
                if (read == 0)
                {
                    token.ThrowIfCancellationRequested();
                    _decoder.WaitForExit();
                    if (_decoder.ExitCode != 0) throw new IOException("ffmpeg: " + _errors.GetAwaiter().GetResult());
                    if (y != 0 || filled != 0) throw new EndOfStreamException("A partial video frame was received.");
                    return false;
                }
                filled += read;
            }
        }
        return true;
    }

    public void Dispose()
    {
        _cancel.Dispose(); Kill(_decoder); _decoder.WaitForExit();
        _errors.GetAwaiter().GetResult(); _decoder.Dispose();
    }

    private static ProcessStartInfo StartInfo(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }
}
