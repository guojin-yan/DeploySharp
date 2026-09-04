using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

internal static class TestImageResolver
{
    private const string FileName = "bus.jpg";
    private const string Sha256 = "33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c";
    private const string Url = "https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1/bus.jpg";
    private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

    public static string Resolve(string requested, bool useDefault, bool offline)
    {
        if (File.Exists(requested)) return Path.GetFullPath(requested);
        if (!useDefault) return Path.GetFullPath(requested);
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_IMAGE_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharp", "TestImages");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, FileName);
        if (offline)
        {
            Validate(path);
            return path;
        }
        Ensure(path);
        return path;
    }

    private static void Ensure(string path)
    {
        if (File.Exists(path)) { Validate(path); return; }
        string partial = path + ".partial";
        try
        {
            using HttpResponseMessage response = Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
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
        Validate(path);
    }

    private static void Validate(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The default DeploySharp test image is not cached. Run once online before --offline.", path);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        if (!string.Equals(hash, Sha256, StringComparison.Ordinal)) throw new InvalidDataException("Test image SHA-256 mismatch: " + path);
    }
}
