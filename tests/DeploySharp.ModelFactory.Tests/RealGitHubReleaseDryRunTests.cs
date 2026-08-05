using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class RealGitHubReleaseDryRunTests
    {
        [TestMethod]
        public async Task ReadsRealReleaseMetadataOnlyWhenExplicitlyConfigured()
        {
            string? repository = Environment.GetEnvironmentVariable("DEPLOYSHARP_MODELFACTORY_REPOSITORY");
            string? tag = Environment.GetEnvironmentVariable("DEPLOYSHARP_MODELFACTORY_TAG");
            if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(tag))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_MODELFACTORY_REPOSITORY and DEPLOYSHARP_MODELFACTORY_TAG to enable the read-only GitHub Release dry run.");
                return;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeploySharp-ModelFactory-Integration/2.0");
            string? token = Environment.GetEnvironmentVariable("DEPLOYSHARP_MODELFACTORY_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string url = "https://api.github.com/repos/" + repository.Trim('/') + "/releases/tags/" + Uri.EscapeDataString(tag);
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(tag, document.RootElement.GetProperty("tag_name").GetString());
            Assert.IsTrue(document.RootElement.TryGetProperty("assets", out JsonElement assets));
            Assert.AreEqual(JsonValueKind.Array, assets.ValueKind);
        }
    }
}
