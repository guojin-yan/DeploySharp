using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;

namespace DeploySharp.ModelFactory.Tests
{
    internal sealed class CatalogFixture
    {
        public const string Tag = "models-20260804.1";
        public const string ModelId = "tests/gguf";
        public const string ArtifactId = "gguf.cpu";
        public byte[] ModelBytes { get; } = Encoding.UTF8.GetBytes("GGUF-test-model-bytes");
        public byte[] TestInputBytes { get; } = Encoding.UTF8.GetBytes("Hello DeploySharp");
        public byte[] ExpectedResultBytes { get; } = Encoding.UTF8.GetBytes("{\"text\":\"synthetic expected result\"}");
        public byte[] ManifestBytes { get; }
        public ValidatedModelCatalog Catalog { get; }
        public ModelSelection Selection { get; }

        public CatalogFixture(ModelCatalogStatus status = ModelCatalogStatus.Supported, string format = "gguf", string backend = "llama-sharp", bool portable = true, string? modelPath = null, string? catalogModelPath = null)
        {
            string path = modelPath ?? "models/model.gguf";
            string assetPath = catalogModelPath ?? path;
            var modelFile = new ModelFileDocument(path, Hash(ModelBytes), ModelBytes.LongLength, "application/octet-stream", ModelFileRole.Model);
            var manifestArtifact = new ModelArtifactDocument(ArtifactId, format, ModelArtifactLocationKind.File, path, new[] { backend }, new[] { modelFile }, quantization: "q4_k_m", portable: portable);
            var manifest = new ModelPackageDocument(
                "2.0", ModelId, "Test GGUF", "llama", "text-generation", "1.0",
                new ModelExporterDocument("llama.cpp", "test", "abc123"),
                new ModelSourceDocument("https://example.com/model", "https://example.com/project", "abc123", "DeploySharp", null, "Apache-2.0", null, true),
                DateTimeOffset.Parse("2026-08-04T00:00:00Z"), null,
                Array.Empty<ModelTensorSignatureDocument>(), Array.Empty<ModelTensorSignatureDocument>(), new[] { manifestArtifact });
            ManifestBytes = Encoding.UTF8.GetBytes(ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(manifest)));

            ModelCatalogRelease? release = status == ModelCatalogStatus.External ? null : new ModelCatalogRelease("guojin-yan", "DeploySharp", Tag, "0123456789abcdef");
            var assets = new List<ModelCatalogAsset>();
            if (status != ModelCatalogStatus.External || format == "tensorrt")
            {
                assets.Add(Asset("manifest", ModelCatalogAssetKind.Manifest, "manifest.json", ManifestBytes));
                assets.Add(Asset("model", ModelCatalogAssetKind.Model, assetPath, ModelBytes));
            }

            var artifact = new ModelCatalogArtifact(
                ArtifactId, format, new[] { backend }, "fp16", "q4_k_m", portable,
                status == ModelCatalogStatus.External && assets.Count == 0 ? null : "manifest",
                assets,
                new ModelCatalogConversion("llama.cpp", "test", "abc123", "Test-only reproducible fixture"));
            var tests = status == ModelCatalogStatus.External ? Array.Empty<ModelCatalogAsset>() : new[]
            {
                Asset("prompt", ModelCatalogAssetKind.TestInput, "inputs/prompt.txt", TestInputBytes),
                Asset("expected", ModelCatalogAssetKind.TestExpected, "expected/result.json", ExpectedResultBytes)
            };
            var entry = new ModelCatalogEntry(
                ModelId, "Test GGUF", "llama", "text-generation", "1.0", status, "Small contract fixture",
                new ModelSourceDocument("https://example.com/model", "https://example.com/project", "abc123", "DeploySharp", null, "Apache-2.0", null, status != ModelCatalogStatus.External),
                release, new[] { artifact }, tests, expectedResultAssetId: status == ModelCatalogStatus.External ? null : "expected", documentationPath: "models/tests-gguf.md");
            var document = new ModelCatalogDocument("1.0", "2026-08-04T00:00:00Z", "tests.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry });
            Catalog = ModelCatalogValidator.Validate(document);
            Selection = ModelCatalogQuery.Select(Catalog, new ModelQuery(modelId: ModelId, includePreview: true))[0];
        }

        public ModelCatalogAsset Asset(string id, ModelCatalogAssetKind kind, string relativePath, byte[] bytes)
        {
            string fileName = relativePath.Substring(relativePath.LastIndexOf('/') + 1);
            return new ModelCatalogAsset(id, kind, Tag, new Uri("https://github.com/guojin-yan/DeploySharp/releases/download/" + Tag + "/" + fileName), relativePath, bytes.LongLength, Hash(bytes), "application/octet-stream", "Apache-2.0");
        }

        public Dictionary<string, byte[]> Responses()
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [Selection.Artifact.Assets[0].DownloadUri!.AbsoluteUri] = ManifestBytes,
                [Selection.Artifact.Assets[1].DownloadUri!.AbsoluteUri] = ModelBytes,
                [Selection.Entry.TestInputs[0].DownloadUri!.AbsoluteUri] = TestInputBytes,
                [Selection.Entry.TestInputs[1].DownloadUri!.AbsoluteUri] = ExpectedResultBytes
            };
        }

        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }

    internal sealed class ScriptedHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses;
        private readonly Queue<HttpStatusCode> _statuses = new Queue<HttpStatusCode>();
        private readonly TimeSpan _delay;
        private int _active;
        private int _requestCount;

        public ScriptedHttpHandler(Dictionary<string, byte[]> responses, TimeSpan? delay = null)
        {
            _responses = responses;
            _delay = delay ?? TimeSpan.Zero;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);
        public int MaximumActive { get; private set; }
        public string? LastUserAgent { get; private set; }
        public bool ThrowNetworkError { get; set; }
        public bool WrongContentLength { get; set; }

        public void EnqueueStatus(HttpStatusCode status) => _statuses.Enqueue(status);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            int active = Interlocked.Increment(ref _active);
            if (active > MaximumActive) MaximumActive = active;
            try
            {
                LastUserAgent = request.Headers.UserAgent.ToString();
                if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
                if (ThrowNetworkError) throw new HttpRequestException("Synthetic network failure.");
                HttpStatusCode status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
                var response = new HttpResponseMessage(status) { RequestMessage = request };
                if (status == (HttpStatusCode)429) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                if (status == HttpStatusCode.OK)
                {
                    if (!_responses.TryGetValue(request.RequestUri!.AbsoluteUri, out byte[]? bytes)) bytes = Encoding.UTF8.GetBytes("missing fixture");
                    response.Content = new ByteArrayContent(bytes);
                    if (WrongContentLength) response.Content.Headers.ContentLength = bytes.LongLength + 1;
                }
                else response.Content = new ByteArrayContent(Array.Empty<byte>());
                return response;
            }
            finally { Interlocked.Decrement(ref _active); }
        }
    }

    internal sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "deploysharp-modelfactory-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
