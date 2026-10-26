using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrReleaseLiveTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task PublishedPaddleOcrVlBundleDownloadsAndReopensOffline()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_RELEASE_RUN"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_RELEASE_RUN=1 to download the published five-graph Bundle.");
            string? cacheRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_RELEASE_CACHE");
            if (string.IsNullOrWhiteSpace(cacheRoot))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_RELEASE_CACHE to a dedicated directory with at least 5 GB free.");

            using (var online = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(cacheRoot, requestTimeout: TimeSpan.FromHours(2))))
            {
                PaddleOcrReleaseBundleMaterialization bundle = await online.GetBundleAsync("paddleocr-vl/1.6");
                Assert.AreEqual(10, bundle.Bundle.Artifacts.Count);
                foreach (PaddleOcrReleaseBundleArtifact artifact in bundle.Bundle.Artifacts)
                    AssertAsset(bundle.GetRequiredPath(artifact.Role), artifact.Size, artifact.Sha256);
            }
            using (var offline = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(cacheRoot, offline: true)))
            {
                PaddleOcrReleaseBundleMaterialization bundle = await offline.GetBundleAsync("paddleocr-vl/1.6");
                Assert.AreEqual(5, bundle.Bundle.Artifacts.Count(value => string.Equals(value.Format, "onnx", StringComparison.Ordinal)));
                foreach (PaddleOcrReleaseBundleArtifact artifact in bundle.Bundle.Artifacts)
                    AssertAsset(bundle.GetRequiredPath(artifact.Role), artifact.Size, artifact.Sha256);
            }
        }

        private static void AssertAsset(string path, long size, string expectedSha256)
        {
            Assert.IsTrue(File.Exists(path), path);
            Assert.AreEqual(size, new FileInfo(path).Length, path);
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            Assert.AreEqual(expectedSha256, Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(), path);
        }
    }
}
