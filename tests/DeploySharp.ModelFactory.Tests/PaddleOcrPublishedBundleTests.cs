using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrPublishedBundleTests
    {
        [TestMethod]
        [DataRow("mobile-cls", 3077, "5ce3ab3d3fcf5e1e21c9689b5d61c7c12b7ad93e8a7b1c2aaf77b046e7da0f96")]
        [DataRow("mobile-det", 3064, "21397cbb7207f61d10a332436319a5d3ebb4cd396de27fea20a1815e83435473")]
        [DataRow("mobile-rec", 3324, "37838a112cbcd75c9945d52e51def6260f0a26a40fa4af80dc7d605de92b99d3")]
        [DataRow("server-cls", 3076, "4292c3e01f62426252ad516a3e3897b6c3a5bc51dd3fc77d4593bb6be6cedd85")]
        [DataRow("server-det", 3072, "4442be563e57a9cda3f0ce2d4a974199dd7cf51af41194d45170cbf4914d9cb0")]
        [DataRow("server-rec", 3326, "492032ce76d98fbd8e33f78bf83cc983bb933696d56c23a4b4b58ff62ab7d571")]
        public void CatalogMaterializationUsesTheExactPublishedBundle(string suffix, int size, string sha256)
        {
            string filename = suffix + ".modelpack.json";
            string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "paddleocr-release", filename));
            ModelPackageDocument manifest = ModelPackageJsonSerializer.Deserialize(text).Document;
            ModelCatalogEntry entry = OfficialModelCatalog.Load().Document.Entries.Single(value => value.ModelId == manifest.ModelId);
            ModelCatalogArtifact artifact = entry.Artifacts.Single();
            ModelCatalogAsset manifestAsset = artifact.Assets.Single(value => value.Kind == ModelCatalogAssetKind.Manifest);
            string releaseBase = "https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/";

            // Published bytes are UTF-8/CRLF; Git may check fixtures out as LF.
            byte[] bytes = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            Assert.AreEqual(size, bytes.Length);
            Assert.AreEqual(sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            Assert.AreEqual((long)size, manifestAsset.Size);
            Assert.AreEqual(sha256, manifestAsset.Sha256);
            Assert.AreEqual(releaseBase + filename, manifestAsset.DownloadUri!.AbsoluteUri);
            Assert.AreEqual(manifest.Artifacts.Single().Files.Count + 1, artifact.Assets.Count);

            foreach (ModelFileDocument file in manifest.Artifacts.Single().Files)
            {
                ModelCatalogAsset asset = artifact.Assets.Single(value => value.RelativePath == file.RelativePath);
                Assert.AreEqual(file.Size, asset.Size);
                Assert.AreEqual(file.Sha256, asset.Sha256);
                string name = file.Role == ModelFileRole.Model ? "ppocrv5-" + suffix + ".model.onnx"
                    : file.Role == ModelFileRole.Labels ? "ppocrv5_dict.txt" : "paddleocr.LICENSE.txt";
                Assert.AreEqual(releaseBase + name, asset.DownloadUri!.AbsoluteUri);
            }
        }
    }
}
