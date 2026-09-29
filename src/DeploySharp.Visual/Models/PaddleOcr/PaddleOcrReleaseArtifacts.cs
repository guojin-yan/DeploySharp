using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Immutable metadata for one independently downloadable core PP-OCR asset in the models-paddleocr Release. / models-paddleocr Release 中一个可独立下载的核心 PP-OCR 资产元数据。</summary>
    public sealed class PaddleOcrReleaseArtifact
    {
        internal PaddleOcrReleaseArtifact(ModelId modelId, string assetName, long size, string sha256, int opset, string? dictionaryAssetName)
        {
            ModelId = modelId; AssetName = assetName; Size = size; Sha256 = sha256; Opset = opset; DictionaryAssetName = dictionaryAssetName;
        }
        public ModelId ModelId { get; }
        public string ReleaseTag => "models-paddleocr";
        public string AssetName { get; }
        public long Size { get; }
        public string Sha256 { get; }
        public int Opset { get; }
        public string? DictionaryAssetName { get; }
        public Uri DownloadUri => new Uri("https://github.com/guojin-yan/DeploySharp/releases/download/" + ReleaseTag + "/" + Uri.EscapeDataString(AssetName), UriKind.Absolute);
    }

    /// <summary>Maps the Visual core catalog to the independently downloadable Release assets. / 将 Visual 核心目录映射到可独立下载的 Release 资产。</summary>
    public static class PaddleOcrReleaseArtifacts
    {
        private static readonly IReadOnlyDictionary<string, PaddleOcrReleaseArtifact> Entries = Create();

        public static PaddleOcrReleaseArtifact Get(ModelId modelId)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            if (!Entries.TryGetValue(modelId.Value, out PaddleOcrReleaseArtifact? artifact)) throw new KeyNotFoundException("No models-paddleocr Release asset is registered for: " + modelId.Value);
            return artifact;
        }

        public static bool TryGet(ModelId modelId, out PaddleOcrReleaseArtifact? artifact)
        {
            if (modelId.IsEmpty) { artifact = null; return false; }
            return Entries.TryGetValue(modelId.Value, out artifact);
        }

        private static IReadOnlyDictionary<string, PaddleOcrReleaseArtifact> Create()
        {
            var values = new[]
            {
                A("paddleocr/ppocrv4/mobile-det", "ppocrv4-mobile-det.onnx", 4745517, "63e5e65c450d1fc85bf3670916cfc0041ad5acc3b8e4773de59337d413ae27eb", 11, null),
                A("paddleocr/ppocrv4/server-det", "ppocrv4-server-det.onnx", 113442336, "1d6b24b3038d814ba5243691d4d5c2af2d3451f909f12617748aba508345f6b9", 11, null),
                A("paddleocr/ppocrv4/mobile-rec", "ppocrv4-mobile-rec.onnx", 10831559, "bfaa9bded94d704a110e2a7630be42f8a796341bb45df227121250e703ee4bc7", 11, "ppocrv4_keys.txt"),
                A("paddleocr/ppocrv4/server-rec", "ppocrv4-server-rec.onnx", 90520234, "b089d3cebf235c57ee1cf1621da452c0bf25acb8f2eead7f5fb65a843f17057f", 7, "ppocrv4_keys.txt"),
                A("paddleocr/ppocrv4/legacy-cls", "ppocrv4-legacy-cls.onnx", 582663, "f4bb53707100c5f3d59ba834eb05bb400369f20aed35d4b26807b1bfadd2a70e", 11, null),
                A("paddleocr/ppocrv5/mobile-det", "ppocrv5-mobile-det.onnx", 4826518, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", 11, null),
                A("paddleocr/ppocrv5/server-det", "ppocrv5-server-det.onnx", 88116836, "0c4ff76f78feb3e4b4e9b3030df350234f570027e1ec9cfe5bbb7a596cb57f47", 11, null),
                A("paddleocr/ppocrv5/mobile-rec", "ppocrv5-mobile-rec.onnx", 16560873, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", 7, "ppocrv5_dict.txt"),
                A("paddleocr/ppocrv5/server-rec", "ppocrv5-server-rec.onnx", 84502992, "12ec4f2b7266afcca07063786238e1538c8f19656cb2b6bbc0f4b9c492c2667a", 10, "ppocrv5_dict.txt"),
                A("paddleocr/ppocrv5/mobile-cls", "ppocrv5-mobile-cls.onnx", 1018940, "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", 7, null),
                A("paddleocr/ppocrv5/server-cls", "ppocrv5-server-cls.onnx", 6777816, "38aa97cd4be591e0ad304e659f07ba30d946f27a63315433f6659c69c8778345", 7, null),
                A("paddleocr/ppocrv6/tiny-det", "ppocrv6-tiny-det.onnx", 1780590, "193bab7a04fca699a6c82e6abb5b81bdb28177f0abd4062552b04908dafb19f8", 14, null),
                A("paddleocr/ppocrv6/tiny-rec", "ppocrv6-tiny-rec.onnx", 4462639, "9ef676d6ed3c88256a2d92c640c44f25b0c40947e111b14b8be8f594091563e6", 11, "PP-OCRv6_tiny_rec_dict.txt"),
                A("paddleocr/ppocrv6/small-det", "ppocrv6-small-det.onnx", 9880512, "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e", 14, null),
                A("paddleocr/ppocrv6/small-rec", "ppocrv6-small-rec.onnx", 21159378, "5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634", 11, "PP-OCRv6_small_rec_dict.txt"),
                A("paddleocr/ppocrv6/medium-det", "ppocrv6-medium-det.onnx", 62032837, "eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1", 14, null),
                A("paddleocr/ppocrv6/medium-rec", "ppocrv6-medium-rec.onnx", 76554979, "9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba", 11, "PP-OCRv6_medium_rec_dict.txt")
            };
            var result = new Dictionary<string, PaddleOcrReleaseArtifact>(StringComparer.OrdinalIgnoreCase);
            foreach (PaddleOcrReleaseArtifact value in values) result.Add(value.ModelId.Value, value);
            return result;
        }

        private static PaddleOcrReleaseArtifact A(string modelId, string assetName, long size, string sha256, int opset, string? dictionary)
            => new PaddleOcrReleaseArtifact(new ModelId(modelId), assetName, size, sha256, opset, dictionary);
    }
}

#pragma warning restore CS1591
