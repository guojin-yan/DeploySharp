using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrLicenseReviewTests
    {
        [TestMethod]
        public void LicenseReviewKeepsModelRedistributionBlockedWhenArchivesCarryNoNotices()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "paddleocr-license-redistribution-review.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;

            Assert.AreEqual("1.0", root.GetProperty("schemaVersion").GetString());
            Assert.IsFalse(root.GetProperty("decision").GetProperty("redistributionApproved").GetBoolean());
            Assert.AreEqual("Apache-2.0", root.GetProperty("upstreamCode").GetProperty("licenseExpression").GetString());
            JsonElement archives = root.GetProperty("modelArchives");
            Assert.AreEqual(6, archives.GetArrayLength());
            Assert.IsTrue(archives.EnumerateArray().All(archive =>
                archive.GetProperty("archiveLicenseEntries").GetInt32() == 0 &&
                archive.GetProperty("archiveNoticeEntries").GetInt32() == 0 &&
                !archive.GetProperty("inferenceMetadataLicenseFields").GetBoolean()));
            Assert.IsTrue(root.GetProperty("openReviewItems").GetArrayLength() >= 3);
        }
    }
}
