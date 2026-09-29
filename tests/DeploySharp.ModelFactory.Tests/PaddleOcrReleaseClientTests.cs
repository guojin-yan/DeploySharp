using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrReleaseClientTests
    {
        [TestMethod]
        public async Task DownloadsOnlySelectedCoreModelAndDictionaryFromStableRelease()
        {
            byte[] model = Encoding.UTF8.GetBytes("ppocr-model");
            byte[] dictionary = Encoding.UTF8.GetBytes("blank\na\nb\n");
            string modelHash = Hash(model);
            string dictionaryHash = Hash(dictionary);
            string catalog = "{\"corePaddleOcr\":[{\"modelId\":\"ppocrv5/mobile-rec\",\"assetName\":\"ppocrv5-mobile-rec.onnx\",\"family\":\"pp-ocr\",\"version\":\"v5\",\"task\":\"rec\",\"name\":\"mobile\",\"size\":" + model.Length + ",\"sha256\":\"" + modelHash + "\",\"dictionaryAssetName\":\"ppocrv5_dict.txt\",\"profileFactory\":\"CreateRecognition\"}],\"ppStructure\":[]}";
            string sums = dictionaryHash + "  ppocrv5_dict.txt\n" + modelHash + "  ppocrv5-mobile-rec.onnx\n";
            var handler = new StaticHandler(new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["paddleocr-release-catalog.json"] = Encoding.UTF8.GetBytes(catalog),
                ["SHA256SUMS"] = Encoding.UTF8.GetBytes(sums),
                ["ppocrv5-mobile-rec.onnx"] = model,
                ["ppocrv5_dict.txt"] = dictionary
            });
            using var directory = new TestDirectory();
            using var client = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(directory.Path), new HttpClient(handler));

            PaddleOcrReleaseModelMaterialization result = await client.GetModelAsync("paddleocr/ppocrv5/mobile-rec");
            CollectionAssert.AreEqual(model, File.ReadAllBytes(result.ModelPath));
            CollectionAssert.AreEqual(dictionary, File.ReadAllBytes(result.DictionaryPath!));
            Assert.AreEqual("ppocrv5/mobile-rec", result.Model.ModelId);
            Assert.IsTrue(handler.Requested.Contains("paddleocr-release-catalog.json"));
            Assert.IsTrue(handler.Requested.Contains("ppocrv5-mobile-rec.onnx"));
            Assert.IsTrue(handler.Requested.Contains("ppocrv5_dict.txt"));
        }

        [TestMethod]
        public async Task ResolvesPpStructureCodeAliasAndReusesOfflineCache()
        {
            byte[] model = Encoding.UTF8.GetBytes("layout-model");
            string hash = Hash(model);
            string catalog = "{\"corePaddleOcr\":[],\"ppStructure\":[{\"modelId\":\"pp-doclayout-s\",\"assetName\":\"pp-doclayout-s.onnx\",\"family\":\"pp-structure\",\"module\":\"layout-detection\",\"size\":" + model.Length + ",\"sha256\":\"" + hash + "\"}]}";
            var responses = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["paddleocr-release-catalog.json"] = Encoding.UTF8.GetBytes(catalog),
                ["pp-doclayout-s.onnx"] = model
            };
            using var directory = new TestDirectory();
            using (var client = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(directory.Path), new HttpClient(new StaticHandler(responses))))
            {
                PaddleOcrReleaseModel modelInfo = await client.FindModelAsync("paddle-doc/pp-doclayout-s");
                Assert.AreEqual("pp-doclayout-s", modelInfo.ModelId);
                await client.GetModelAsync("paddle-doc/pp-doclayout-s");
            }

            using (var offline = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(directory.Path, offline: true), new HttpClient(new StaticHandler(new Dictionary<string, byte[]>()))))
            {
                PaddleOcrReleaseModelMaterialization result = await offline.GetModelAsync("paddle-doc/pp-doclayout-s");
                Assert.AreEqual(model.Length, new FileInfo(result.ModelPath).Length);
            }
        }

        [TestMethod]
        public async Task DownloadsOnlyDeclaredChart2TableBundleArtifactsAndReusesVerifiedOfflineCache()
        {
            var artifactRows = new[]
            {
                (Role: "vision-projector", ModelId: "paddle-chart/pp-chart2table/vision-projector", Name: "pp-chart2table-vision-projector.onnx", Format: "onnx", Bytes: Encoding.UTF8.GetBytes("vision")),
                (Role: "token-embedding", ModelId: "paddle-chart/pp-chart2table/token-embedding", Name: "pp-chart2table-token-embedding.onnx", Format: "onnx", Bytes: Encoding.UTF8.GetBytes("embedding")),
                (Role: "text-prefill", ModelId: "paddle-chart/pp-chart2table/text-prefill", Name: "pp-chart2table-text-prefill.onnx", Format: "onnx", Bytes: Encoding.UTF8.GetBytes("prefill")),
                (Role: "text-decode-with-past", ModelId: "paddle-chart/pp-chart2table/text-decode-with-past", Name: "pp-chart2table-text-decode-with-past.onnx", Format: "onnx", Bytes: Encoding.UTF8.GetBytes("decode")),
                (Role: "qwen-tokenizer", ModelId: "paddle-chart/pp-chart2table", Name: "qwen.tiktoken", Format: "tiktoken", Bytes: Encoding.UTF8.GetBytes("tokenizer")),
                (Role: "tokenizer-config", ModelId: "paddle-chart/pp-chart2table", Name: "tokenizer_config.json", Format: "json", Bytes: Encoding.UTF8.GetBytes("{}")),
                (Role: "added-tokens", ModelId: "paddle-chart/pp-chart2table", Name: "added_tokens.json", Format: "json", Bytes: Encoding.UTF8.GetBytes("[]"))
            };
            string rows = string.Join(",", artifactRows.Select(value => "{\"role\":\"" + value.Role + "\",\"modelId\":\"" + value.ModelId + "\",\"assetName\":\"" + value.Name + "\",\"format\":\"" + value.Format + "\",\"size\":" + value.Bytes.Length + ",\"sha256\":\"" + Hash(value.Bytes) + "\"}"));
            string catalog = "{\"corePaddleOcr\":[],\"ppStructure\":[],\"bundles\":[{\"bundleId\":\"paddle-chart/pp-chart2table\",\"modelId\":\"paddle-chart/pp-chart2table\",\"artifacts\":[" + rows + "]}]}";
            var responses = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["paddleocr-release-catalog.json"] = Encoding.UTF8.GetBytes(catalog)
            };
            foreach (var artifact in artifactRows) responses.Add(artifact.Name, artifact.Bytes);
            using var directory = new TestDirectory();
            using (var client = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(directory.Path), new HttpClient(new StaticHandler(responses))))
            {
                PaddleOcrReleaseBundleMaterialization bundle = await client.GetBundleAsync("paddle-chart/pp-chart2table");

                Assert.AreEqual(7, bundle.Bundle.Artifacts.Count);
                Assert.AreEqual("qwen.tiktoken", Path.GetFileName(bundle.GetRequiredPath("qwen-tokenizer")));
                Assert.AreEqual("tokenizer_config.json", Path.GetFileName(bundle.GetRequiredPath("tokenizer-config")));
                Assert.AreEqual("added_tokens.json", Path.GetFileName(bundle.GetRequiredPath("added-tokens")));
                Assert.AreEqual("paddle-chart/pp-chart2table/vision-projector", bundle.CreateOnnxArtifact("vision-projector").ModelId.Value);
                CollectionAssert.AreEqual(artifactRows[0].Bytes, File.ReadAllBytes(bundle.GetRequiredPath("vision-projector")));
            }

            using (var offline = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(directory.Path, offline: true), new HttpClient(new StaticHandler(new Dictionary<string, byte[]>()))))
            {
                PaddleOcrReleaseBundleMaterialization cached = await offline.GetBundleAsync("paddle-chart/pp-chart2table");
                Assert.AreEqual(7, cached.Bundle.Artifacts.Count);
                CollectionAssert.AreEqual(artifactRows[3].Bytes, File.ReadAllBytes(cached.GetRequiredPath("text-decode-with-past")));
            }
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var result = sha.ComputeHash(bytes);
            var builder = new StringBuilder(result.Length * 2);
            foreach (byte value in result) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private sealed class StaticHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<string, byte[]> _responses;
            public StaticHandler(IReadOnlyDictionary<string, byte[]> responses) { _responses = responses; }
            public List<string> Requested { get; } = new List<string>();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string name = Path.GetFileName(request.RequestUri!.AbsolutePath);
                Requested.Add(name);
                if (!_responses.TryGetValue(name, out byte[]? bytes)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(bytes) });
            }
        }
    }
}
