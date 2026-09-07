using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeploySharpApp.BackendHost;

internal sealed class NativeWorkerEnvironment : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> VariableMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["cudaRoot"] = "JYPPX_CUDA_ROOT",
        ["cudnnRoot"] = "JYPPX_CUDNN_ROOT",
        ["tensorRtRoot"] = "JYPPX_TENSORRT_ROOT",
        ["bridgePath"] = "JYPPX_NATIVE_BRIDGE_PATH",
        ["tensorRtApiVersion"] = "DEPLOYSHARP_TENSORRT_API_VERSION"
    };

    private readonly IReadOnlyDictionary<string, string?> _previous;

    private NativeWorkerEnvironment(IReadOnlyDictionary<string, string?> previous) => _previous = previous;

    public static NativeWorkerEnvironment Apply(IReadOnlyDictionary<string, string>? payload)
    {
        var previous = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var searchRoots = new List<string>();
        if (payload is not null)
        {
            foreach (KeyValuePair<string, string> mapping in VariableMap)
            {
                if (!payload.TryGetValue(mapping.Key, out string? value) || string.IsNullOrWhiteSpace(value)) continue;
                previous[mapping.Value] = Environment.GetEnvironmentVariable(mapping.Value);
                string normalized = Normalize(mapping.Key, value);
                Environment.SetEnvironmentVariable(mapping.Value, normalized);
                if (mapping.Key.EndsWith("Root", StringComparison.OrdinalIgnoreCase)) searchRoots.Add(normalized);
                else if (mapping.Key.Equals("bridgePath", StringComparison.OrdinalIgnoreCase)) searchRoots.Add(Path.GetDirectoryName(normalized)!);
            }
        }
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        previous["PATH"] = path;
        IEnumerable<string> nativeSearch = searchRoots.SelectMany(root => new[] { root, Path.Combine(root, "bin"), Path.Combine(root, "lib") }).Where(Directory.Exists);
        string prefix = string.Join(Path.PathSeparator.ToString(), nativeSearch.Distinct(StringComparer.OrdinalIgnoreCase));
        if (prefix.Length > 0) Environment.SetEnvironmentVariable("PATH", prefix + Path.PathSeparator + path);
        return new NativeWorkerEnvironment(previous);
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, string?> pair in _previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }

    private static string Normalize(string key, string value)
    {
        string trimmed = value.Trim();
        return key.Equals("tensorRtApiVersion", StringComparison.OrdinalIgnoreCase) ? trimmed : Path.GetFullPath(trimmed);
    }
}
