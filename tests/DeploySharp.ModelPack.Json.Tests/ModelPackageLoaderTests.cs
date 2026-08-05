using System;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class ModelPackageLoaderTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "deploysharp-modelpack-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestMethod]
        public void LoadsOnnxExternalDataAndDirectoryArtifactsAndConvertsToCore()
        {
            byte[] onnx = new byte[] { 1, 2, 3 };
            byte[] external = new byte[] { 4, 5 };
            byte[] openvinoXml = new byte[] { 6, 7 };
            byte[] openvinoWeights = new byte[] { 8, 9, 10 };
            ModelArtifactDocument onnxArtifact = new ModelArtifactDocument("onnx.cpu", "onnx", ModelArtifactLocationKind.File, "model.onnx", new[] { "onnxruntime" }, new[]
            {
                new ModelFileDocument("model.onnx", ModelPackageTestFactory.Hash(onnx), onnx.LongLength, "application/octet-stream", ModelFileRole.Model),
                new ModelFileDocument("model.data", ModelPackageTestFactory.Hash(external), external.LongLength, "application/octet-stream", ModelFileRole.ExternalData)
            }, portable: true);
            ModelArtifactDocument openvinoArtifact = new ModelArtifactDocument("openvino.cpu", "openvino", ModelArtifactLocationKind.Directory, "openvino", new[] { "openvino" }, new[]
            {
                new ModelFileDocument("openvino/model.xml", ModelPackageTestFactory.Hash(openvinoXml), openvinoXml.LongLength, "application/xml", ModelFileRole.Model),
                new ModelFileDocument("openvino/model.bin", ModelPackageTestFactory.Hash(openvinoWeights), openvinoWeights.LongLength, "application/octet-stream", ModelFileRole.Weights)
            }, portable: true);
            ModelPackageTestFactory.CreatePackage(_root, Tuple.Create("model.onnx", onnx), Tuple.Create("model.data", external), Tuple.Create("openvino/model.xml", openvinoXml), Tuple.Create("openvino/model.bin", openvinoWeights));
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(onnxArtifact, openvinoArtifact))));

            LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
            Assert.AreEqual(2, package.Artifacts.Count);
            Assert.AreEqual(Path.Combine(_root, "model.onnx"), package.Artifacts[0].Location);
            Assert.AreEqual("onnx", package.ToCoreArtifacts()[0].Format);
            Assert.AreEqual("onnxruntime", package.ToCoreArtifacts()[0].PreferredBackend!.Value.Value);
            Assert.AreEqual(4, package.Artifacts[0].Files.Count + package.Artifacts[1].Files.Count);
        }

        [TestMethod]
        public void ReportsIntegrityMismatchAndMissingFilesWithStructuredDiagnostics()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            ModelPackageTestFactory.CreatePackage(_root, Tuple.Create("model.onnx", new byte[] { 9, 9, 9 }));
            ModelPackageDocument document = ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes));
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(document)));

            ModelPackageValidationException exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.IntegrityMismatch));

            File.Delete(Path.Combine(_root, "model.onnx"));
            exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.FileNotFound));
        }

        [TestMethod]
        public void AsyncLoaderHonorsCancellationAndDeclaredSizeLimit()
        {
            byte[] bytes = new byte[] { 1, 2, 3, 4 };
            ModelPackageTestFactory.CreatePackage(_root, Tuple.Create("model.onnx", bytes));
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes)))));
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => ModelPackageLoader.LoadAsync(manifestPath, cancellationToken: cancelled.Token).GetAwaiter().GetResult());
            var options = new ModelPackageLoadOptions(maximumTotalFileBytes: 1);
            ModelPackageValidationException exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath, options));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.LimitExceeded));
        }

        [TestMethod]
        public void RejectsSymlinkOutsidePackageWhenPlatformAllowsCreatingOne()
        {
            byte[] bytes = new byte[] { 8, 8 };
            string outside = Path.Combine(Path.GetDirectoryName(_root)!, "deploysharp-modelpack-outside-" + Guid.NewGuid().ToString("N") + ".onnx");
            File.WriteAllBytes(outside, bytes);
            string link = Path.Combine(_root, "model.onnx");
            try
            {
                File.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException)
            {
                File.Delete(outside);
                Assert.Inconclusive("Symbolic-link creation is unavailable in this test environment: " + exception.Message);
                return;
            }

            try
            {
                string manifestPath = Path.Combine(_root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes)))));
                ModelPackageValidationException exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
                Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.LinkBoundary));
            }
            finally
            {
                File.Delete(link);
                File.Delete(outside);
            }
        }

        [TestMethod]
        public void AcceptsSymlinkInsidePackageWhenPlatformAllowsCreatingOne()
        {
            byte[] bytes = new byte[] { 3, 3, 3 };
            string target = Path.Combine(_root, "target.onnx");
            string link = Path.Combine(_root, "model.onnx");
            File.WriteAllBytes(target, bytes);
            try
            {
                File.CreateSymbolicLink(link, target);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException)
            {
                Assert.Inconclusive("Symbolic-link creation is unavailable in this test environment: " + exception.Message);
                return;
            }

            try
            {
                string manifestPath = Path.Combine(_root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes)))));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Assert.AreEqual(Path.GetFullPath(link), package.Artifacts[0].Location);
            }
            finally
            {
                File.Delete(link);
                File.Delete(target);
            }
        }
    }
}
