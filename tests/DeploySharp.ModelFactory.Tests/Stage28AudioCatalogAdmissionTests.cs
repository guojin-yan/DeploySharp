using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage28AudioCatalogAdmissionTests
    {
        [TestMethod]
        public void AudioQuerySelectsExactBackendRateChannelTimestampAndFeatureIdentity()
        {
            Identity identity = Exact();
            ValidatedModelCatalog catalog = Catalog(identity, family: "wav2vec2", task: "automatic-speech-recognition", includeSecond: false);
            ModelSelection selected = ModelCatalogQuery.Select(catalog, new ModelQuery(task: "automatic-speech-recognition", family: "wav2vec2", modelVersion: Version, format: "onnx", backend: "onnxruntime", precision: "fp32", includePreview: true, capability: "ctc-transcription", sampleRate: 16000, channelCount: 1, language: "en", timestampMode: "ctc-frame-stride-320", speakerMode: "none", audioFeatureId: identity.Feature, vadId: identity.Vad, speakerId: identity.Speaker)).Single();
            Assert.AreEqual("ctc-encoder-head", selected.Artifact.BundleRole);
            ModelCatalogArtifact roundTrip = ModelCatalogJsonSerializer.Deserialize(ModelCatalogJsonSerializer.Serialize(catalog)).Document.Entries.Single().Artifacts.Single();
            Assert.AreEqual(identity.Vad, roundTrip.VadId);
            Assert.AreEqual(identity.Speaker, roundTrip.SpeakerId);
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void AudioBundleRejectsMixedProcessorTokenizerVadSpeakerAndFeatureIdentities()
        {
            AssertMixed(value => value with { Processor = "other-processor" });
            AssertMixed(value => value with { Tokenizer = "other-tokenizer" });
            AssertMixed(value => value with { Vad = "other-vad" });
            AssertMixed(value => value with { Speaker = "other-speaker" });
            AssertMixed(value => value with { Feature = "other-feature" });
        }

        private static void AssertMixed(Func<Identity, Identity> mutate)
        {
            ModelBundleQuery query = new ModelBundleQuery(task: "speaker-diarization", family: "pyannote-speaker-diarization", format: "onnx", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "ctc-encoder-head", "ctc-sidecar" });
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog(Exact(), mutate(Exact())), query));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, exception.Diagnostics.Single().Code);
        }

        private static ValidatedModelCatalog Catalog(Identity second, Identity? first = null, string family = "pyannote-speaker-diarization", string task = "speaker-diarization", bool includeSecond = true)
        {
            Identity left = first ?? second;
            var source = new ModelSourceDocument("https://huggingface.co/pyannote/speaker-diarization-3.1", "https://github.com/pyannote/pyannote-audio", "84fd25912480287da0247647c3d2b4853cb3ee5d", "pyannote.audio", null, "MIT", null, false);
            IEnumerable<ModelCatalogArtifact> artifacts = includeSecond ? new[] { Artifact("ctc-encoder-head", left), Artifact("ctc-sidecar", second) } : new[] { Artifact("ctc-encoder-head", second) };
            var entry = new ModelCatalogEntry("external/audio/pyannote-speaker-diarization-3.1", "audio external bundle", family, task, Version, ModelCatalogStatus.External, "External audio contract.", source, null, artifacts, Array.Empty<ModelCatalogAsset>());
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-10T00:00:00Z", "stage28.audio.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, Identity identity)
        {
            var conversion = new ModelCatalogConversion("official-source-contract", "blocked", "84fd25912480287da0247647c3d2b4853cb3ee5d", "No executable artifact; gated source evidence only.");
            return new ModelCatalogArtifact(role + ".blocked", "onnx", new[] { "onnxruntime" }, "fp32", "none", false, null, Array.Empty<ModelCatalogAsset>(), conversion, role, Version, new[] { "speaker-diarization", "speaker-embedding", "ctc-transcription" }, tokenizerId: identity.Tokenizer, vocabularyMode: "none", language: "en", processorId: identity.Processor, sampleRate: 16000, channelCount: 1, timestampMode: "ctc-frame-stride-320", speakerMode: "none", audioFeatureId: identity.Feature, vadId: identity.Vad, speakerId: identity.Speaker);
        }

        private static Identity Exact() => new Identity("pyannote.no-tokenizer", "pyannote-speaker-diarization-3.1-model-card", "pyannote-segmentation-3.0-gated", "pyannote-embedding-gated", "pyannote-segmentation-embedding-clustering-gated-v1");
        private const string Version = "pyannote-speaker-diarization-3.1-84fd259-blocked";
        private sealed record Identity(string Tokenizer, string Processor, string Vad, string Speaker, string Feature);
    }
}
