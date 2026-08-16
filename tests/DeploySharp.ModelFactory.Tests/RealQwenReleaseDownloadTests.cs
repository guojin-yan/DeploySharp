using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class RealQwenReleaseDownloadTests
    {
        [TestMethod]
        public async Task DownloadsAndVerifiesPublicQwenReleaseWhenExplicitlyEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_MODELFACTORY_QWEN_RELEASE"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_MODELFACTORY_QWEN_RELEASE=1 to download and verify the public Qwen Release asset.");
                return;
            }

            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelSelection selection = ModelCatalogQuery.Select(catalog, new ModelQuery(
                modelId: "llm/qwen2.5-0.5b-instruct-q4-k-m",
                backend: "llamasharp",
                format: "gguf",
                includePreview: true)).Single();

            using var directory = new TestDirectory();
            using var factory = new ModelFactoryClient(catalog, new ModelFactoryOptions(directory.Path, requestTimeout: TimeSpan.FromMinutes(15)));
            MaterializedModel materialized = await factory.GetModelAsync(selection);

            string modelPath = Path.Combine(materialized.PackageRoot, "qwen2.5-0.5b-instruct-q4_k_m.gguf");
            Assert.IsTrue(File.Exists(modelPath));
            Assert.AreEqual(491400032L, new FileInfo(modelPath).Length);
            Assert.IsTrue(await factory.VerifyModelCacheAsync(selection));
        }
    }
}
