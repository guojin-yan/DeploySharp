using System.Text.Json;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

// Optional validation decorator; model semantics stay in the library's built-in decoder.
internal sealed class ReferenceDecoder : IVisualDecoder
{
    private readonly IVisualDecoder inner;
    private readonly string directory;
    private int validated;

    internal ReferenceDecoder(IVisualDecoder inner, string directory) { this.inner = inner; this.directory = directory; }
    public VisualTaskId Task => inner.Task;

    public object Decode(VisualDecodeContext context)
    {
        if (Interlocked.Exchange(ref validated, 1) == 0)
        {
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "rfdetr.json")));
            foreach (JsonElement port in metadata.RootElement.GetProperty("outputs").EnumerateArray())
            {
                string name = port.GetProperty("name").GetString()!;
                ITensor tensor = context.Outputs.GetRequired(name);
                var shape = new TensorShape(port.GetProperty("shape").EnumerateArray().Select(value => value.GetInt64()).ToArray());
                if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(shape)) throw new InvalidDataException("Raw output contract mismatch: " + name);
                // Dense mask logits are more sensitive to backend accumulation; the decoded binary masks
                // are independently checked against the official reference with a 0.01% pixel budget.
                Compare((float[])tensor.Buffer, ReadFloats(Path.Combine(directory, port.GetProperty("file").GetString()!)), name, name == "masks" ? .01f : .001f, .001f);
            }
        }
        return inner.Decode(context);
    }

    internal static void ValidateInput(PreparedVisualInput input, string directory)
        => Compare((float[])input.Tensor.Buffer, ReadFloats(Path.Combine(directory, "input.f32")), "input", .00001f, 0);

    private static float[] ReadFloats(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length % sizeof(float) != 0) throw new InvalidDataException("Invalid float tensor file: " + path);
        var values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    private static void Compare(float[] actual, float[] expected, string name, float absoluteTolerance, float relativeTolerance)
    {
        if (actual.Length != expected.Length) throw new InvalidDataException("Raw tensor element count mismatch: " + name);
        double squaredError = 0;
        float maximum = 0;
        int failures = 0;
        for (int index = 0; index < actual.Length; index++)
        {
            if (!float.IsFinite(actual[index]) || !float.IsFinite(expected[index])) throw new InvalidDataException("Non-finite tensor value: " + name);
            float error = Math.Abs(actual[index] - expected[index]);
            maximum = Math.Max(maximum, error);
            squaredError += (double)error * error;
            if (error > absoluteTolerance + relativeTolerance * Math.Abs(expected[index])) failures++;
        }
        Console.WriteLine($"tensor={name};shapeElements={actual.Length};finite=passed;maxAbs:{maximum:R};rmse:{Math.Sqrt(squaredError / actual.Length):R};outsideTolerance={failures}");
        if (failures != 0) throw new InvalidDataException($"Raw tensor {name} exceeded atol={absoluteTolerance}, rtol={relativeTolerance} at {failures} values.");
    }
}
