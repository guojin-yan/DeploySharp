using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

internal static class TestImageResolver
{
    private const string ReleaseBaseUrl = "https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1";
    private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly IReadOnlyDictionary<string, Definition> Definitions = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
    {
        ["bus"] = new Definition("bus.jpg", "33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c"),
        ["classification"] = new Definition("demo_7.jpg", "839511d6f6e7688d319b01d6977fc2cc642cb91e8fae4a2e0562c1322d894bf2"),
        ["pose"] = new Definition("demo_9.jpg", "68bbb631ebb95e3c9ff5c82bcf8baf445d938771501014440541bf4dbc71c1b6"),
        ["obb"] = new Definition("plane.png", "dde925501ff0f2bddb7e28198fdd0586620f7a7ef587412717f666b7ea6584c9"),
    };

    public static string Resolve(string? requested, string kind)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return Path.GetFullPath(requested);
        string family = kind.Contains("cls", StringComparison.OrdinalIgnoreCase) ? "classification"
            : kind.Contains("pose", StringComparison.OrdinalIgnoreCase) ? "pose"
            : kind.Contains("obb", StringComparison.OrdinalIgnoreCase) ? "obb"
            : "bus";
        Definition definition = Definitions[family];
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_IMAGE_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharp", "TestImages");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, definition.FileName);
        Ensure(path, definition);
        return path;
    }

    private static void Ensure(string path, Definition definition)
    {
        if (File.Exists(path))
        {
            Validate(path, definition);
            return;
        }

        string partial = path + ".partial";
        try
        {
            using HttpResponseMessage response = Client.GetAsync(ReleaseBaseUrl + "/" + definition.FileName, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.NotFound) throw new FileNotFoundException("The DeploySharp test-image release asset is not available yet.", path);
            response.EnsureSuccessStatusCode();
            using Stream source = response.Content.ReadAsStream();
            using FileStream destination = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            throw;
        }
        File.Move(partial, path, true);
        Validate(path, definition);
    }

    private static void Validate(string path, Definition definition)
    {
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        if (!string.Equals(hash, definition.Sha256, StringComparison.Ordinal)) throw new InvalidDataException("Test image SHA-256 mismatch: " + path);
    }

    private sealed record Definition(string FileName, string Sha256);
}
