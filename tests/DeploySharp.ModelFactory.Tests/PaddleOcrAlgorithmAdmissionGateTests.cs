using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrAlgorithmAdmissionGateTests
    {
        [TestMethod]
        public void MobileClassifierBindsPreviewReleaseAndKeepsAlgorithmBlockersOpen()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry candidate = catalog.Document.Entries.Single(entry =>
                entry.ModelId == "paddleocr/ppocrv5/mobile-cls");

            Assert.AreEqual(ModelCatalogStatus.Preview, candidate.Status);

            string evidencePath = Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "paddleocr-release-admission.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(evidencePath));
            JsonElement root = document.RootElement;

            JsonElement admission = root.GetProperty("releaseAdmission");
            Assert.AreEqual("preview-algorithm-admission-blocked", admission.GetProperty("state").GetString());
            Assert.IsTrue(admission.GetProperty("catalogRedistributionDeclared").GetBoolean());
            Assert.IsFalse(admission.GetProperty("algorithmAdmissionRedistributionApproved").GetBoolean());
            Assert.AreEqual("closed-public-prerelease", admission.GetProperty("immutableReleaseAsset").GetString());

            string[] openBlockers = root.GetProperty("blockers")
                .EnumerateArray()
                .Where(blocker => blocker.GetProperty("status").GetString() == "open")
                .Select(blocker => blocker.GetProperty("id").GetString()!)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "license-and-redistribution" },
                openBlockers);

            JsonElement algorithmCandidate = root.GetProperty("algorithmCandidate");
            Assert.AreEqual(candidate.ModelId, algorithmCandidate.GetProperty("catalogModelId").GetString());
            Assert.AreEqual(candidate.Release!.Tag, algorithmCandidate.GetProperty("release").GetProperty("tag").GetString());
            Assert.AreEqual(candidate.Release.Commit, algorithmCandidate.GetProperty("release").GetProperty("commit").GetString());
            Assert.AreEqual("5ce3ab3d3fcf5e1e21c9689b5d61c7c12b7ad93e8a7b1c2aaf77b046e7da0f96", algorithmCandidate.GetProperty("release").GetProperty("manifestSha256").GetString());

            JsonElement contract = algorithmCandidate.GetProperty("contract");
            CollectionAssert.AreEqual(new[] { 1, 3, 80, 160 }, contract.GetProperty("inputShape").EnumerateArray().Select(value => value.GetInt32()).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, contract.GetProperty("outputShape").EnumerateArray().Select(value => value.GetInt32()).ToArray());
            CollectionAssert.AreEqual(new[] { "0_degree", "180_degree" }, contract.GetProperty("labelOrder").EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.AreEqual(0.9, contract.GetProperty("rejectionThreshold").GetDouble());

            JsonElement golden = algorithmCandidate.GetProperty("golden");
            Assert.AreEqual("recorded", golden.GetProperty("officialPredictorOutputStatus").GetString());
            Assert.AreEqual("d2820ebee4744ef48a7897cd888c659f5f733ae2b618b638577ad30902181e5d", golden.GetProperty("officialPredictorOutputSha256").GetString());
            Assert.AreEqual("180_degree", golden.GetProperty("label").GetString());
            Assert.AreEqual(0.9986026883125305, golden.GetProperty("confidence").GetDouble());
            Assert.AreEqual(0.00001, golden.GetProperty("confidenceTolerance").GetDouble());

            JsonElement candidateEvidence = root.GetProperty("artifacts")
                .EnumerateArray()
                .Single(artifact => artifact.GetProperty("modelId").GetString() ==
                    "paddleocr/ppocrv5/mobile-cls/external");
            Assert.AreEqual(
                "official-image orientation class and confidence golden; ORT/OpenVINO",
                candidateEvidence.GetProperty("officialSemanticEvidence").GetString());
            Assert.IsFalse(candidateEvidence.GetProperty("redistributionApproved").GetBoolean());

            JsonElement reproduction = root.GetProperty("exportReproduction").GetProperty("results")
                .EnumerateArray()
                .Single(result => result.GetProperty("modelId").GetString() ==
                    "paddleocr/ppocrv5/mobile-cls/external");
            Assert.IsTrue(reproduction.GetProperty("exactCandidateMatch").GetBoolean());
        }

        [TestMethod]
        public void OfficialOnnxArchivesAndDictionaryRemainPinnedOutsideCurrentModelPack()
        {
            string evidencePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "paddleocr-release-admission.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(evidencePath));
            JsonElement root = document.RootElement;

            JsonElement archives = root.GetProperty("officialOnnxInferenceArchives");
            Assert.AreEqual(6, archives.GetArrayLength());
            Assert.IsTrue(archives.EnumerateArray().All(archive =>
                archive.GetProperty("scope").GetString() ==
                    "official-paddle-onnx-inference-archive;not-the-current-DeploySharp-ModelPack" &&
                archive.GetProperty("sourceUrl").GetString()!.StartsWith(
                    "https://paddle-model-ecology.bj.bcebos.com/", StringComparison.Ordinal) &&
                archive.GetProperty("sourceUrl").GetString()!.EndsWith(
                    "_onnx_infer.tar", StringComparison.Ordinal) &&
                archive.GetProperty("archiveEntry").GetString()!.EndsWith(
                    "/inference.onnx", StringComparison.Ordinal)));

            JsonElement mobileClassifier = archives.EnumerateArray().Single(archive =>
                archive.GetProperty("modelId").GetString() == "paddleocr/ppocrv5/mobile-cls/external");
            Assert.AreEqual(1044480L, mobileClassifier.GetProperty("archiveSize").GetInt64());
            Assert.AreEqual("e29f1bffb2cec4db1ef8da9b2369d033d0a16d0a1a8f033b518d6063e6b9a1af", mobileClassifier.GetProperty("archiveSha256").GetString());
            Assert.AreEqual(1019454L, mobileClassifier.GetProperty("onnxSize").GetInt64());
            Assert.AreEqual("94a6a0a0425f2b5f08b5df72086f2d72abe40f1d22f6d12d2cd83674f11f2ff3", mobileClassifier.GetProperty("onnxSha256").GetString());

            JsonElement dictionary = root.GetProperty("sharedArtifacts").GetProperty("dictionary");
            Assert.AreEqual("https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/2661c7c0ef5c613e8f93c6e93b2e052399f0f854/ppocr/utils/dict/ppocrv5_dict.txt", dictionary.GetProperty("sourceUrl").GetString());
            Assert.AreEqual("2661c7c0ef5c613e8f93c6e93b2e052399f0f854", dictionary.GetProperty("sourceRevision").GetString());
            Assert.AreEqual("official-pinned-repository-file", dictionary.GetProperty("sourceStatus").GetString());
            Assert.AreEqual("d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b", dictionary.GetProperty("sha256").GetString());
        }

        [TestMethod]
        public void MobileClassifierEvidenceRejectsIdentityContractGoldenAndBlockerDrift()
        {
            string evidencePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "paddleocr-release-admission.json");
            string json = File.ReadAllText(evidencePath);

            AssertDriftRejected(json, root => root["algorithmCandidate"]!["release"]!["modelSha256"] = new string('0', 64));
            AssertDriftRejected(json, root => root["algorithmCandidate"]!["contract"]!["inputShape"]![3] = 161);
            AssertDriftRejected(json, root => root["algorithmCandidate"]!["contract"]!["labelOrder"]![0] = "180_degree");
            AssertDriftRejected(json, root => root["algorithmCandidate"]!["contract"]!["rejectionThreshold"] = 0.5);
            AssertDriftRejected(json, root => root["algorithmCandidate"]!["golden"]!["label"] = "0_degree");
            AssertDriftRejected(json, root => root["algorithmCandidate"]!["golden"]!["officialPredictorOutputSha256"] = new string('0', 64));
            AssertDriftRejected(json, root => root["blockers"]!.AsArray()
                .Single(value => value!["id"]!.GetValue<string>() == "license-and-redistribution")!["status"] = "closed");
        }

        private static void AssertDriftRejected(string json, Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(json)!.AsObject();
            mutate(root);
            Assert.ThrowsExactly<InvalidDataException>(() => ValidateAdmission(root));
        }

        private static void ValidateAdmission(JsonObject root)
        {
            JsonObject candidate = root["algorithmCandidate"]!.AsObject();
            JsonObject release = candidate["release"]!.AsObject();
            JsonObject contract = candidate["contract"]!.AsObject();
            JsonObject golden = candidate["golden"]!.AsObject();
            string[] openBlockers = root["blockers"]!.AsArray()
                .Select(value => value!.AsObject())
                .Where(value => value["status"]!.GetValue<string>() == "open")
                .Select(value => value["id"]!.GetValue<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            bool valid = release["modelSha256"]!.GetValue<string>() == "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2"
                && contract["inputShape"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(new[] { 1, 3, 80, 160 })
                && contract["labelOrder"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(new[] { "0_degree", "180_degree" })
                && contract["rejectionThreshold"]!.GetValue<double>() == 0.9
                && golden["label"]!.GetValue<string>() == "180_degree"
                && golden["officialPredictorOutputStatus"]!.GetValue<string>() == "recorded"
                && golden["officialPredictorOutputSha256"]!.GetValue<string>() == "d2820ebee4744ef48a7897cd888c659f5f733ae2b618b638577ad30902181e5d"
                && openBlockers.SequenceEqual(new[] { "license-and-redistribution" });
            if (!valid) throw new InvalidDataException("PP-OCRv5 mobile-cls algorithm-admission evidence drifted.");
        }
    }
}
