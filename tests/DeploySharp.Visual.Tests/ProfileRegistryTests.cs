using System;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class ProfileRegistryTests
    {
        [TestMethod]
        public void RegistryFreezesAndReturnsDeterministicSnapshot()
        {
            var registry = new VisualProfileRegistry();
            VisualModelProfile second = VisualTestData.ClassificationProfile();
            var first = new VisualModelProfile("tests/a", second.ModelId, second.Task, "1", "fake", second.Input, second.Outputs, second.Labels, second.Decoder);
            registry.Register(second);
            registry.Register(first);
            registry.Freeze();
            Assert.IsTrue(registry.IsFrozen);
            CollectionAssert.AreEqual(new[] { "tests/a", "tests/classification.v1" }, registry.GetProfiles().Select(value => value.ProfileId).ToArray());
            Assert.ThrowsExactly<VisualException>(() => registry.Register(first));
        }

        [TestMethod]
        public void DuplicateLabelsOutputsAndMismatchedDecoderAreRejected()
        {
            Assert.ThrowsExactly<VisualException>(() => new VisualModelProfile("tests/bad", VisualTestData.ClassificationModelId, VisualTaskId.ImageClassification, "1", "fake", new VisualInputBinding("x", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("out", TensorElementType.Float32, new TensorShape(1, 2)), new VisualOutputBinding("out", TensorElementType.Float32, new TensorShape(1, 2)) }, new[] { new VisualLabel(0, "same"), new VisualLabel(1, "same") }, new ClassificationDecoder("out")));
            Assert.ThrowsExactly<VisualException>(() => new VisualModelProfile("tests/bad-task", VisualTestData.ClassificationModelId, VisualTaskId.ObjectDetection, "1", "fake", new VisualInputBinding("x", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("out", TensorElementType.Float32, new TensorShape(1, 2)) }, Array.Empty<VisualLabel>(), new ClassificationDecoder("out")));
        }

        [TestMethod]
        public void SelectionReusesCoreBackendDescriptorAndHonorsPreferredBackend()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            var provider = new FakeVisualBackendProvider(VisualTestData.Metadata(profile, new TensorShape(1, 3)), _ => JYPPX.DeploySharp.Tensors.InferenceOutputs.Create("scores", new JYPPX.DeploySharp.Tensors.Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            using var backends = new BackendRegistry();
            backends.Register(provider);
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var artifact = new ModelArtifact(profile.ModelId, "fake", "fixture", preferredBackend: VisualTestData.BackendId);
            VisualProfileSelection selection = profiles.Select(artifact, backends, new BackendRequest(BackendCapabilities.TensorInference), VisualTaskId.ImageClassification);
            Assert.AreEqual(VisualTestData.BackendId, selection.Backend.Id);
            Assert.AreEqual(profile.ProfileId, selection.Profile.ProfileId);
            Assert.ThrowsExactly<VisualException>(() => profiles.Select(new ModelArtifact(new ModelId("missing"), "fake", "fixture"), backends, new BackendRequest(BackendCapabilities.TensorInference)));
        }
    }
}
