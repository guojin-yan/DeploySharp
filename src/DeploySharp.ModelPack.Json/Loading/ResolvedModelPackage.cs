using System;
using System.Collections.Generic;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Represents one verified file resolved below a package root. / 表示一个已在包根目录下解析并验证的文件。</summary>
    public sealed class ResolvedModelFile
    {
        internal ResolvedModelFile(ModelFileDocument document, string fullPath)
        {
            Document = document;
            FullPath = fullPath;
        }

        /// <summary>Gets immutable file metadata. / 获取不可变文件元数据。</summary>
        public ModelFileDocument Document { get; }
        /// <summary>Gets the validated absolute local path. / 获取已验证的本地绝对路径。</summary>
        public string FullPath { get; }
    }

    /// <summary>Represents one verified local model artifact. / 表示一个已验证的本地模型工件。</summary>
    public sealed class ResolvedModelArtifact
    {
        private readonly IReadOnlyList<ResolvedModelFile> _files;
        private readonly ModelId _modelId;

        internal ResolvedModelArtifact(ModelId modelId, ModelArtifactDocument document, string location, IEnumerable<ResolvedModelFile> files)
        {
            _modelId = modelId;
            Document = document;
            Location = location;
            _files = new List<ResolvedModelFile>(files).AsReadOnly();
        }

        /// <summary>Gets immutable artifact metadata. / 获取不可变工件元数据。</summary>
        public ModelArtifactDocument Document { get; }
        /// <summary>Gets the validated absolute file or directory location. / 获取已验证的文件或目录绝对位置。</summary>
        public string Location { get; }
        /// <summary>Gets all verified files. / 获取全部已验证文件。</summary>
        public IReadOnlyList<ResolvedModelFile> Files => _files;

        /// <summary>Converts this resolved artifact to the stable Core model-artifact contract. / 将已解析工件转换为稳定的 Core 模型工件契约。</summary>
        public ModelArtifact ToCoreArtifact()
        {
            BackendId? preferredBackend = null;
            if (Document.CompatibleBackends.Count == 1) preferredBackend = new BackendId(Document.CompatibleBackends[0]);
            string? sha256 = null;
            if (Document.LocationKind == ModelArtifactLocationKind.File)
            {
                for (int index = 0; index < Document.Files.Count; index++)
                {
                    if (string.Equals(Document.Files[index].RelativePath, Document.Entrypoint, StringComparison.OrdinalIgnoreCase))
                    {
                        sha256 = Document.Files[index].Sha256;
                        break;
                    }
                }
            }

            return new ModelArtifact(_modelId, Document.Format!, Location, sha256, preferredBackend);
        }
    }

    /// <summary>Represents a manifest and all artifacts verified below one local package root. / 表示一个清单及其在本地包根目录下完成验证的全部工件。</summary>
    public sealed class LocalModelPackage
    {
        private readonly IReadOnlyList<ResolvedModelArtifact> _artifacts;

        internal LocalModelPackage(string manifestPath, string packageRoot, ValidatedModelPackage manifest, IEnumerable<ResolvedModelArtifact> artifacts)
        {
            ManifestPath = manifestPath;
            PackageRoot = packageRoot;
            Manifest = manifest;
            _artifacts = new List<ResolvedModelArtifact>(artifacts).AsReadOnly();
        }

        /// <summary>Gets the absolute manifest path. / 获取清单绝对路径。</summary>
        public string ManifestPath { get; }
        /// <summary>Gets the absolute package root. / 获取包根目录绝对路径。</summary>
        public string PackageRoot { get; }
        /// <summary>Gets the validated normalized manifest. / 获取已验证的规范化清单。</summary>
        public ValidatedModelPackage Manifest { get; }
        /// <summary>Gets resolved artifacts in manifest order. / 按清单顺序获取已解析工件。</summary>
        public IReadOnlyList<ResolvedModelArtifact> Artifacts => _artifacts;

        /// <summary>Converts all resolved artifacts to stable Core contracts. / 将全部已解析工件转换为稳定 Core 契约。</summary>
        public IReadOnlyList<ModelArtifact> ToCoreArtifacts()
        {
            var values = new List<ModelArtifact>(_artifacts.Count);
            for (int index = 0; index < _artifacts.Count; index++) values.Add(_artifacts[index].ToCoreArtifact());
            return values.AsReadOnly();
        }
    }
}
