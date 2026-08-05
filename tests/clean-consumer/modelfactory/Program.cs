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
using JYPPX.DeploySharp.ModelPack.Json.Serialization;

namespace DeploySharp.ModelFactory.CleanConsumer;

internal static class Program
{
    private const string ReleaseTag = "models-20260804.1";

    private static async Task Main()
    {
        byte[] modelBytes = Encoding.UTF8.GetBytes("GGUF-clean-consumer-model");
        byte[] testInputBytes = Encoding.UTF8.GetBytes("Hello DeploySharp");
        byte[] expectedResultBytes = Encoding.UTF8.GetBytes("{\"text\":\"synthetic expected result\"}");
        byte[] manifestBytes = CreateManifest(modelBytes);
        (ValidatedModelCatalog catalog, Dictionary<string, byte[]> responses) = CreateCatalog(manifestBytes, modelBytes, testInputBytes, expectedResultBytes);

        string cacheRoot = Path.Combine(Path.GetTempPath(), "deploysharp-modelfactory-clean-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheRoot);
        try
        {
            using (var httpClient = new HttpClient(new FixtureHandler(responses)))
            using (var factory = new ModelFactoryClient(catalog, new ModelFactoryOptions(cacheRoot), httpClient))
            {
                ModelSelection selection = factory.Select(new ModelQuery(modelId: "clean/gguf", backend: "llama-sharp"));
                MaterializedModel materialized = await factory.GetModelAsync(selection).ConfigureAwait(false);
                MaterializedAsset input = await factory.GetTestInputAsync(selection.Entry, "prompt").ConfigureAwait(false);
                if (materialized.Package.ToCoreArtifacts().Count != 1 || !File.Exists(input.FullPath))
                {
                    throw new InvalidOperationException("Online materialization did not produce the expected verified assets.");
                }
            }

            using (var offlineFactory = new ModelFactoryClient(catalog, new ModelFactoryOptions(cacheRoot, offline: true)))
            {
                ModelSelection selection = offlineFactory.Select(new ModelQuery(modelId: "clean/gguf"));
                MaterializedModel materialized = await offlineFactory.GetModelAsync(selection).ConfigureAwait(false);
                if (!await offlineFactory.VerifyModelCacheAsync(selection).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("Offline cache verification failed.");
                }

                Console.WriteLine("ModelFactory package-only consumer passed: " + materialized.Package.ToCoreArtifacts()[0].Format);
            }
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    private static byte[] CreateManifest(byte[] modelBytes)
    {
        var file = new ModelFileDocument("models/model.gguf", Hash(modelBytes), modelBytes.LongLength, "application/octet-stream", ModelFileRole.Model);
        var artifact = new ModelArtifactDocument("gguf.cpu", "gguf", ModelArtifactLocationKind.File, "models/model.gguf", new[] { "llama-sharp" }, new[] { file }, quantization: "q4_k_m", portable: true);
        var document = new ModelPackageDocument(
            "2.0", "clean/gguf", "Clean GGUF", "llama", "text-generation", "1.0",
            new ModelExporterDocument("llama.cpp", "clean", "0123456789abcdef"),
            new ModelSourceDocument("https://example.com/model", "https://example.com/project", "0123456789abcdef", "DeploySharp", null, "Apache-2.0", null, true),
            DateTimeOffset.Parse("2026-08-04T00:00:00Z"), null,
            Array.Empty<ModelTensorSignatureDocument>(), Array.Empty<ModelTensorSignatureDocument>(), new[] { artifact });
        return Encoding.UTF8.GetBytes(ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(document)));
    }

    private static (ValidatedModelCatalog Catalog, Dictionary<string, byte[]> Responses) CreateCatalog(byte[] manifestBytes, byte[] modelBytes, byte[] testInputBytes, byte[] expectedResultBytes)
    {
        ModelCatalogAsset manifest = Asset("manifest", ModelCatalogAssetKind.Manifest, "manifest.json", manifestBytes);
        ModelCatalogAsset model = Asset("model", ModelCatalogAssetKind.Model, "models/model.gguf", modelBytes);
        ModelCatalogAsset prompt = Asset("prompt", ModelCatalogAssetKind.TestInput, "inputs/prompt.txt", testInputBytes);
        ModelCatalogAsset expected = Asset("expected", ModelCatalogAssetKind.TestExpected, "expected/result.json", expectedResultBytes);
        var artifact = new ModelCatalogArtifact(
            "gguf.cpu", "gguf", new[] { "llama-sharp" }, "fp16", "q4_k_m", true, "manifest",
            new[] { manifest, model },
            new ModelCatalogConversion("llama.cpp", "clean", "0123456789abcdef", "Package-only clean-consumer fixture"));
        var entry = new ModelCatalogEntry(
            "clean/gguf", "Clean GGUF", "llama", "text-generation", "1.0", ModelCatalogStatus.Supported,
            "Package-only clean-consumer fixture",
            new ModelSourceDocument("https://example.com/model", "https://example.com/project", "0123456789abcdef", "DeploySharp", null, "Apache-2.0", null, true),
            new ModelCatalogRelease("guojin-yan", "DeploySharp", ReleaseTag, "0123456789abcdef"),
            new[] { artifact }, new[] { prompt, expected }, expectedResultAssetId: "expected", documentationPath: "models/clean-gguf.md");
        var catalogDocument = new ModelCatalogDocument(
            "1.0", "2026-08-04T00:00:00Z", "clean.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry });
        ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(catalogDocument);
        var responses = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [manifest.DownloadUri!.AbsoluteUri] = manifestBytes,
            [model.DownloadUri!.AbsoluteUri] = modelBytes,
            [prompt.DownloadUri!.AbsoluteUri] = testInputBytes,
            [expected.DownloadUri!.AbsoluteUri] = expectedResultBytes
        };
        return (catalog, responses);
    }

    private static ModelCatalogAsset Asset(string id, ModelCatalogAssetKind kind, string relativePath, byte[] bytes)
    {
        string fileName = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        return new ModelCatalogAsset(
            id, kind, ReleaseTag,
            new Uri("https://github.com/guojin-yan/DeploySharp/releases/download/" + ReleaseTag + "/" + fileName),
            relativePath, bytes.LongLength, Hash(bytes), "application/octet-stream", "Apache-2.0");
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, byte[]> _responses;

        public FixtureHandler(IReadOnlyDictionary<string, byte[]> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri != null && _responses.TryGetValue(request.RequestUri.AbsoluteUri, out byte[]? bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }
}
