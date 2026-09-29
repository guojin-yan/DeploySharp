using System;
using System.Collections.Generic;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Metadata for one independently downloadable converted PP-Structure ONNX asset. / 一个可独立下载的已转换 PP-Structure ONNX 资产元数据。</summary>
    public sealed class PaddleDocumentReleaseArtifact
    {
        internal PaddleDocumentReleaseArtifact(string modelId, string assetName, PaddleDocumentModule module, long size, string sha256, int opset)
        {
            ModelId = modelId; AssetName = assetName; Module = module; Size = size; Sha256 = sha256; Opset = opset;
        }
        public string ModelId { get; }
        public string ReleaseTag => "models-paddleocr";
        public string AssetName { get; }
        public PaddleDocumentModule Module { get; }
        public long Size { get; }
        public string Sha256 { get; }
        public int Opset { get; }
        public Uri DownloadUri => new Uri("https://github.com/guojin-yan/DeploySharp/releases/download/" + ReleaseTag + "/" + Uri.EscapeDataString(AssetName), UriKind.Absolute);
    }

    /// <summary>Maps document model contracts to the converted assets in models-paddleocr. Unsupported upstream archives are intentionally absent. / 将文档模型合同映射到 models-paddleocr 中已转换的资产；上游不兼容的归档有意不映射。</summary>
    public static class PaddleDocumentReleaseArtifacts
    {
        private static readonly IReadOnlyDictionary<string, PaddleDocumentReleaseArtifact> Entries = Create();

        public static PaddleDocumentReleaseArtifact Get(string modelId)
        {
            if (!TryGet(modelId, out PaddleDocumentReleaseArtifact? artifact)) throw new KeyNotFoundException("No models-paddleocr Release asset is registered for: " + modelId);
            return artifact!;
        }

        public static bool TryGet(string modelId, out PaddleDocumentReleaseArtifact? artifact)
        {
            artifact = null;
            if (string.IsNullOrWhiteSpace(modelId)) return false;
            string normalized = modelId.Trim();
            if (!Entries.TryGetValue(normalized, out artifact))
            {
                string[] prefixes = { "paddle-doc/", "paddle-table/", "paddle-formula/", "paddle-seal/" };
                foreach (string prefix in prefixes)
                {
                    if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Entries.TryGetValue(normalized.Substring(prefix.Length), out artifact)) break;
                }
                if (artifact == null && string.Equals(normalized, "paddle-seal/ppocrv4-mobile", StringComparison.OrdinalIgnoreCase)) Entries.TryGetValue("ppocrv4-mobile-seal-det", out artifact);
                if (artifact == null && string.Equals(normalized, "paddle-seal/ppocrv4-server", StringComparison.OrdinalIgnoreCase)) Entries.TryGetValue("ppocrv4-server-seal-det", out artifact);
            }
            return artifact != null;
        }

        private static IReadOnlyDictionary<string, PaddleDocumentReleaseArtifact> Create()
        {
            var values = new[]
            {
                A("pp-lcnet-x1-0-doc-ori", "pp-lcnet-x1-0-doc-ori.onnx", PaddleDocumentModule.DocumentOrientation, 6783057, "96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0"),
                A("uvdoc", "uvdoc.onnx", PaddleDocumentModule.TextImageUnwarping, 31684150, "10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338"),
                A("pp-doclayout-plus-l", "pp-doclayout-plus-l.onnx", PaddleDocumentModule.LayoutDetection, 129694577, "d67689f6325ec0d4cc812308dbd1c84bc801e76731c0f183297013d6b59e0048"),
                A("pp-doclayout-l", "pp-doclayout-l.onnx", PaddleDocumentModule.LayoutDetection, 129356945, "d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9"),
                A("pp-doclayout-m", "pp-doclayout-m.onnx", PaddleDocumentModule.LayoutDetection, 23484680, "a65002c871af5c3d4415524a813f55cda4e8c5d59fe8cebd5af16a84b48e80f3"),
                A("pp-doclayout-s", "pp-doclayout-s.onnx", PaddleDocumentModule.LayoutDetection, 4905136, "5254cb338d5ffb0b54c6f95b527e6e7f0c8a0cb35919c4d6e420dcb913ba592f"),
                A("pp-docblocklayout", "pp-docblocklayout.onnx", PaddleDocumentModule.LayoutDetection, 129311709, "abbf5febf79a35c9f329b4f591d440c7a4b3ae90cbb042bc8835900b49582218"),
                A("picodet-layout-1x", "picodet-layout-1x.onnx", PaddleDocumentModule.LayoutDetection, 7510754, "61efac8c8d3eeee09d7d16db249eb919c1432e22d8235bd408ddffa3477cb8fd"),
                A("picodet-layout-1x-table", "picodet-layout-1x-table.onnx", PaddleDocumentModule.LayoutDetection, 7502497, "7cf5b8c3f7e00353bdb231fec73dd73b66ea0cbf598daaf1f113bf7e6dc4af31"),
                A("picodet-s-layout-3cls", "picodet-s-layout-3cls.onnx", PaddleDocumentModule.LayoutDetection, 4874092, "f2b2c55af08bad9dab4030f86d34336f99bc7e39df9999676ce29db7e60b2fb1"),
                A("picodet-l-layout-3cls", "picodet-l-layout-3cls.onnx", PaddleDocumentModule.LayoutDetection, 23433157, "34d873d7ec7a77448b85ae41db5785bfae0c5dda483c03c177189ebd086b1ce4"),
                A("rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", PaddleDocumentModule.LayoutDetection, 492005465, "e003528247ea0324eba25c94127a1ff5ef48f8a83ae725a8f42f8f0a1d551d8b"),
                A("picodet-s-layout-17cls", "picodet-s-layout-17cls.onnx", PaddleDocumentModule.LayoutDetection, 4895821, "f8a43b8588f1cee80acea3646738a2704c7c7f4cb73f63bc3d325016195fd496"),
                A("picodet-l-layout-17cls", "picodet-l-layout-17cls.onnx", PaddleDocumentModule.LayoutDetection, 23469221, "b3e1b8deaa9538a76418083274df9acf07232cccb30ba6ab957a77a2d73cecb3"),
                A("rt-detr-h-layout-17cls", "rt-detr-h-layout-17cls.onnx", PaddleDocumentModule.LayoutDetection, 492034253, "556ba39ce91419069135e8446e155edda3d5cd475632d6ea5cec0f4dc072b41a"),
                A("slanext-wired", "slanext-wired.onnx", PaddleDocumentModule.TableStructureRecognition, 367743373, "0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f"),
                A("slanext-wireless", "slanext-wireless.onnx", PaddleDocumentModule.TableStructureRecognition, 367743373, "5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97"),
                A("pp-lcnet-x1-0-table-cls", "pp-lcnet-x1-0-table-cls.onnx", PaddleDocumentModule.TableClassification, 6772807, "04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45"),
                A("rt-detr-l-wired-cell-det", "rt-detr-l-wired-cell-det.onnx", PaddleDocumentModule.TableCellDetection, 129311709, "c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6"),
                A("rt-detr-l-wireless-cell-det", "rt-detr-l-wireless-cell-det.onnx", PaddleDocumentModule.TableCellDetection, 129311709, "e141c8aa947ef0aea165c45d51caf6d2cccbca5f381ed9c8854f402570b296bf"),
                A("pp-formulanet-plus-s", "pp-formulanet-plus-s.onnx", PaddleDocumentModule.FormulaRecognition, 231878904, "e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d"),
                A("pp-formulanet-plus-m", "pp-formulanet-plus-m.onnx", PaddleDocumentModule.FormulaRecognition, 592372919, "10d5b4645ec75b441e83e0a543c07e80d3d1704e66eeba9cb605374828465a19"),
                A("pp-formulanet-plus-l", "pp-formulanet-plus-l.onnx", PaddleDocumentModule.FormulaRecognition, 733525676, "ba9ee741c8e95b1f9a026494a2bec9185a710e10298efb857fae6d375c2da2e6"),
                A("pp-formulanet-s", "pp-formulanet-s.onnx", PaddleDocumentModule.FormulaRecognition, 231878904, "9fbcf2d7b5d7534e4c596e241f5c60be482a4d24d6a9625f2e79e140c7522877"),
                A("pp-formulanet-l", "pp-formulanet-l.onnx", PaddleDocumentModule.FormulaRecognition, 730379948, "acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3"),
                A("unimernet", "unimernet.onnx", PaddleDocumentModule.FormulaRecognition, 1842024100, "1d64fafa0161f153dafe40823e97c4b05103030509dd6b286d7c8d4a11b068ab"),
                A("ppocrv4-mobile-seal-det", "ppocrv4-mobile-seal-det.onnx", PaddleDocumentModule.SealTextDetection, 4819576, "e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199"),
                A("ppocrv4-server-seal-det", "ppocrv4-server-seal-det.onnx", PaddleDocumentModule.SealTextDetection, 113443410, "f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7"),
                A("paddleocr/ppocrv5/mobile-rec", "ppocrv5-mobile-rec.onnx", PaddleDocumentModule.TextRecognition, 16560873, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9")
            };
            var result = new Dictionary<string, PaddleDocumentReleaseArtifact>(StringComparer.OrdinalIgnoreCase);
            foreach (PaddleDocumentReleaseArtifact value in values) result.Add(value.ModelId, value);
            return result;
        }

        private static PaddleDocumentReleaseArtifact A(string id, string asset, PaddleDocumentModule module, long size, string sha256)
            => new PaddleDocumentReleaseArtifact(id, asset, module, size, sha256, 17);
    }
}

#pragma warning restore CS1591
