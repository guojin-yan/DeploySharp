using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class NextPublicationCandidateTests
    {
        [TestMethod]
        public void CandidateQueueKeepsExternalRowsOutOfOfficialCatalog()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "next-publication-candidates.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            var officialIds = new HashSet<string>(
                OfficialModelCatalog.Load().Document.Entries.Select(entry => entry.ModelId!).Where(value => value != null),
                StringComparer.OrdinalIgnoreCase);
            JsonElement candidates = document.RootElement.GetProperty("candidates");
            Assert.IsTrue(candidates.GetArrayLength() > 0);
            string ocrEvidenceScript = Path.Combine(FindRepositoryRoot(), "eng", "models", "ocr-anomaly-rmbg", "Test-PaddleOcrExternalEvidence.ps1");
            Assert.IsTrue(File.Exists(ocrEvidenceScript), "The reproducible OCR evidence command is missing.");
            foreach (JsonElement candidate in candidates.EnumerateArray())
            {
                string modelId = candidate.GetProperty("modelId").GetString()!;
                string manifest = candidate.GetProperty("manifest").GetString()!;
                string manifestPath = Path.Combine(FindRepositoryRoot(), manifest.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsFalse(officialIds.Contains(modelId), "Candidate was admitted to the official catalog: " + modelId);
                Assert.AreEqual("blocked-external-only", candidate.GetProperty("publicationState").GetString());
                Assert.IsFalse(candidate.GetProperty("redistributionAllowed").GetBoolean());
                Assert.IsTrue(candidate.GetProperty("blockers").GetArrayLength() > 0);
                Assert.IsTrue(File.Exists(manifestPath), "Candidate manifest is missing: " + manifestPath);
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "DeploySharp.sln"))) return directory.FullName;
                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate the repository root.");
        }
    }
}
