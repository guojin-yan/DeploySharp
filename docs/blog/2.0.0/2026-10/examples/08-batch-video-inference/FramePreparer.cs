using System.Collections.Concurrent;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using Images = JYPPX.OpenCvSharp.ImgProc.Cv2;

// A decoded Mat goes straight to the final FP32 NCHW buffer: no JPEG round-trip.
internal sealed class FramePreparer
{
    public const int Side = 640;
    private readonly ConcurrentDictionary<int, ConcurrentBag<float[]>> _buffers = new();
    private int _active;
    private int _peak;
    public int Active => Volatile.Read(ref _active);
    public int Peak => Volatile.Read(ref _peak);
    public void ResetPeak() => _peak = Active;

    public PreparedVisualInput Prepare(IReadOnlyList<Frame> frames, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int count = frames.Count;
        var pool = _buffers.GetOrAdd(count, _ => new ConcurrentBag<float[]>());
        if (!pool.TryTake(out float[]? pixels)) pixels = new float[checked(count * 3 * Side * Side)];
        int active = Interlocked.Increment(ref _active);
        int peak;
        do { peak = _peak; } while (active > peak && Interlocked.CompareExchange(ref _peak, active, peak) != peak);
        var lease = new BufferLease(() => { pool.Add(pixels); Interlocked.Decrement(ref _active); });
        try
        {
            var descriptors = new VisualInputFrame[count];
            for (int row = 0; row < count; row++)
                descriptors[row] = Write(frames[row], pixels, row * 3 * Side * Side, token);
            var first = descriptors[0];
            return new PreparedVisualInput("images",
                new Tensor<float>(new TensorShape(count, 3, Side, Side), pixels, TensorBufferOwnership.Transfer),
                first.SourceSize, first.ModelSize, count, VisualTensorLayout.Nchw, first.Transform,
                inputId: first.InputId, ownership: PreparedInputOwnership.Owned,
                ownedResource: lease, batchFrames: descriptors);
        }
        catch { lease.Dispose(); throw; }
    }

    private static unsafe VisualInputFrame Write(Frame frame, float[] target, int start, CancellationToken token)
    {
        Mat source = frame.Image;
        if (source.Channels != 3) throw new InvalidDataException("Video frames must be BGR, 3 channels.");
        var sourceSize = new VisualSize(source.Cols, source.Rows);
        double scale = Math.Min((double)Side / source.Cols, (double)Side / source.Rows);
        int width = Math.Clamp((int)Math.Round(source.Cols * scale), 1, Side);
        int height = Math.Clamp((int)Math.Round(source.Rows * scale), 1, Side);
        int left = (Side - width) / 2, top = (Side - height) / 2;
        using var resized = new Mat();
        Images.Resize(source, resized, new Size(width, height), interpolation: InterpolationFlags.Linear);
        int plane = Side * Side;
        Array.Fill(target, 114f / 255f, start, 3 * plane);
        int stride = checked((int)resized.Step.ToUInt64());
        byte* data = (byte*)resized.Data;
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            byte* input = data + y * stride;
            int output = start + (y + top) * Side + left;
            for (int x = 0; x < width; x++)
            {
                target[output + x] = input[x * 3 + 2] / 255f;
                target[output + plane + x] = input[x * 3 + 1] / 255f;
                target[output + 2 * plane + x] = input[x * 3] / 255f;
            }
        }
        // Use actual rounded resize dimensions, so the inverse transform matches the pixels.
        var transform = new ImageTransform(ImageTransformKind.Letterbox, sourceSize,
            new VisualSize(Side, Side), (float)width / source.Cols, (float)height / source.Rows, left, top);
        return new VisualInputFrame(sourceSize, new VisualSize(Side, Side), transform, frame.Index.ToString());
    }

    private sealed class BufferLease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

internal sealed record Frame(int Index, Mat Image) : IDisposable
{
    public void Dispose() => Image.Dispose();
}
