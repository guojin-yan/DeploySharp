using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.TensorRtSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Builds caller-owned TensorRT engines from validated ONNX artifacts.</summary>
    public sealed class TensorRtOnnxEngineBuilder
    {
        /// <summary>Computes the managed build-input hash used before a device/runtime identity is appended to a complete engine cache key.</summary>
        public static string GetBuildInputsSha256(string onnxSha256, TensorRtOnnxEngineBuildOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(onnxSha256) || onnxSha256.Length != 64 || onnxSha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException("The ONNX SHA256 must contain exactly 64 hexadecimal characters.", nameof(onnxSha256));
            }
            options ??= TensorRtOnnxEngineBuildOptions.Default;
            return ComputeBuildInputsSha256(onnxSha256.ToLowerInvariant(), options);
        }

        /// <summary>Builds and atomically writes one External/local-cache .engine or .plan file.</summary>
        public TensorRtOnnxEngineBuildResult Build(
            ModelArtifact onnxArtifact,
            string enginePath,
            TensorRtOnnxEngineBuildOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (onnxArtifact == null) throw new ArgumentNullException(nameof(onnxArtifact));
            options ??= TensorRtOnnxEngineBuildOptions.Default;
            TensorRtOnnxModelArtifactValidator.ReadResult source = TensorRtOnnxModelArtifactValidator.ReadValidated(onnxArtifact, options.MaximumOnnxBytes);
            string outputPath = ValidateOutputPath(onnxArtifact, enginePath, options.Overwrite);
            cancellationToken.ThrowIfCancellationRequested();

            string temporaryPath = Path.Combine(
                Path.GetDirectoryName(outputPath)!,
                "." + Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            TensorRtLogger? logger = null;
            TensorRtBuilder? builder = null;
            TensorRtNetworkDefinition? network = null;
            TensorRtBuilderConfig? config = null;
            TensorRtOnnxParser? parser = null;
            TensorRtHostMemory? serialized = null;
            var profiles = new List<TensorRtOptimizationProfile>();
            try
            {
                logger = new TensorRtLogger(TensorRtApiLineMapper.Map(options.ApiVersion));
                builder = new TensorRtBuilder(logger);
                network = builder.CreateNetwork(options.StronglyTypedNetwork);
                config = builder.CreateBuilderConfig();
                ConfigureBuilder(config, options);
                parser = new TensorRtOnnxParser(logger, network);
                if (ShouldAttachParserBuilderConfig(options.ApiVersion) && !parser.SetBuilderConfig(config))
                {
                    throw BuildFailure(onnxArtifact, TensorRtErrorCodes.EngineBuildFailed, "TensorRT rejected the ONNX parser builder configuration.");
                }

                if (!parser.TryParse(source.Bytes, out IReadOnlyList<TensorRtOnnxParserDiagnostic> diagnostics))
                {
                    throw BuildFailure(onnxArtifact, TensorRtErrorCodes.OnnxParseFailed, "TensorRT could not parse the ONNX model.", FormatDiagnostics(diagnostics));
                }

                ValidatePrecisionPolicy(onnxArtifact, network, options.Precision);
                ApplyInputProfiles(onnxArtifact, builder, network, config, options.InputProfiles, profiles);
                cancellationToken.ThrowIfCancellationRequested();
                serialized = builder.BuildSerializedNetwork(network, config);
                ulong serializedBytes = serialized.SizeInBytes;
                if (serializedBytes < 8 || serializedBytes > (ulong)options.MaximumEngineBytes)
                {
                    throw BuildFailure(onnxArtifact, TensorRtErrorCodes.EngineBuildFailed, "The serialized TensorRT engine is empty, truncated, or exceeds the configured output size limit.", "bytes=" + serializedBytes);
                }

                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan))
                {
                    serialized.CopyTo(stream);
                    stream.Flush(true);
                }

                long actualBytes = new FileInfo(temporaryPath).Length;
                if (actualBytes != checked((long)serializedBytes))
                {
                    throw BuildFailure(onnxArtifact, TensorRtErrorCodes.EngineBuildFailed, "The serialized TensorRT engine length changed while it was written.", "expected=" + serializedBytes + ";actual=" + actualBytes);
                }
                string engineSha256 = ComputeFileSha256(temporaryPath);
                File.Move(temporaryPath, outputPath, options.Overwrite);
                return new TensorRtOnnxEngineBuildResult(
                    source.Path,
                    outputPath,
                    source.Bytes.LongLength,
                    actualBytes,
                    source.Sha256,
                    engineSha256,
                    GetBuildInputsSha256(source.Sha256, options),
                    options.ApiVersion,
                    options.Precision,
                    profiles.Count);
            }
            catch (TensorRtBackendException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (IOException exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.EngineOutputInvalid,
                    "The caller-owned TensorRT engine output could not be written.",
                    exception,
                    onnxArtifact.ModelId,
                    operation: "write-engine",
                    technicalDetails: outputPath);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.EngineOutputInvalid,
                    "The caller-owned TensorRT engine output could not be written.",
                    exception,
                    onnxArtifact.ModelId,
                    operation: "write-engine",
                    technicalDetails: outputPath);
            }
            catch (BridgeProbeException exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.NativeRuntimeUnavailable,
                    "ONNX-to-engine build requires the consumer-owned native bridge, TensorRT, CUDA runtime, driver, and a compatible GPU.",
                    exception,
                    onnxArtifact.ModelId,
                    operation: "build-engine",
                    technicalDetails: exception.GetType().FullName);
            }
            catch (TensorRtException exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.EngineBuildFailed,
                    "TensorRT could not build the serialized engine from the parsed ONNX network.",
                    exception,
                    onnxArtifact.ModelId,
                    operation: "build-engine",
                    technicalDetails: exception.GetType().FullName);
            }
            catch (Exception exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.NativeRuntimeUnavailable,
                    "ONNX-to-engine build requires the consumer-owned native bridge, TensorRT, CUDA runtime, driver, and a compatible GPU.",
                    exception,
                    onnxArtifact.ModelId,
                    operation: "build-engine",
                    technicalDetails: exception.GetType().FullName);
            }
            finally
            {
                TryDelete(temporaryPath);
                DisposeQuietly(serialized);
                DisposeQuietly(parser);
                DisposeQuietly(config);
                for (int index = profiles.Count - 1; index >= 0; index--) DisposeQuietly(profiles[index]);
                DisposeQuietly(network);
                DisposeQuietly(builder);
                DisposeQuietly(logger);
            }
        }

        private static void ConfigureBuilder(TensorRtBuilderConfig config, TensorRtOnnxEngineBuildOptions options)
        {
            config.SetMemoryPoolLimit(TensorRtMemoryPoolType.Workspace, options.WorkspaceBytes);
            if (options.OptimizationLevel >= 0) config.SetOptimizationLevel(options.OptimizationLevel);
            switch (options.Precision)
            {
                case TensorRtOnnxEnginePrecision.RuntimeDefault:
                    break;
                case TensorRtOnnxEnginePrecision.Float32:
                    config.SetFlag(TensorRtBuilderFlag.Tf32, false);
                    break;
                case TensorRtOnnxEnginePrecision.Float16:
                    config.SetFlag(TensorRtBuilderFlag.Fp16, true);
                    break;
                case TensorRtOnnxEnginePrecision.Int8ExplicitQuantization:
                    // Explicit Q/DQ quantization is encoded in the ONNX graph; legacy INT8 calibration stays disabled.
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(options));
            }
        }

        internal static bool ShouldAttachParserBuilderConfig(TensorRtApiVersion apiVersion)
        {
            if (!Enum.IsDefined(typeof(TensorRtApiVersion), apiVersion)) throw new ArgumentOutOfRangeException(nameof(apiVersion));
            return apiVersion == TensorRtApiVersion.TensorRt11;
        }

        private static void ApplyInputProfiles(
            ModelArtifact artifact,
            TensorRtBuilder builder,
            TensorRtNetworkDefinition network,
            TensorRtBuilderConfig config,
            IReadOnlyList<TensorRtOnnxInputProfile> requestedProfiles,
            List<TensorRtOptimizationProfile> ownedProfiles)
        {
            Dictionary<string, TensorRtOnnxInputProfile> requested = requestedProfiles.ToDictionary(profile => profile.InputName, StringComparer.Ordinal);
            TensorRtOptimizationProfile? profile = null;
            for (int inputIndex = 0; inputIndex < network.InputCount; inputIndex++)
            {
                using TensorRtTensor input = network.GetInput(inputIndex);
                TensorRtDims networkShape = input.Shape;
                bool isDynamic = networkShape.Values.Any(value => value < 0);
                bool hasRequested = requested.TryGetValue(input.Name, out TensorRtOnnxInputProfile? requestedProfile);

                if (input.IsShapeTensor)
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "Shape-tensor ONNX inputs are not supported by the initial managed builder contract.", "input=" + input.Name);
                }
                if (isDynamic && !hasRequested)
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "A min/opt/max profile is required for every dynamic ONNX input.", "input=" + input.Name + ";shape=" + networkShape);
                }
                if (!isDynamic && hasRequested)
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "An optimization profile was supplied for a static ONNX input.", "input=" + input.Name + ";shape=" + networkShape);
                }
                if (!hasRequested) continue;

                ValidateProfileAgainstNetwork(artifact, input.Name, networkShape, requestedProfile!);
                if (profile == null)
                {
                    profile = builder.CreateOptimizationProfile();
                    ownedProfiles.Add(profile);
                }
                profile.SetShape(
                    input.Name,
                    ToDims(requestedProfile!.Minimum),
                    ToDims(requestedProfile.Optimum),
                    ToDims(requestedProfile.Maximum));
                requested.Remove(input.Name);
            }

            if (requested.Count != 0)
            {
                throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "An optimization profile references an unknown ONNX network input.", "inputs=" + string.Join(",", requested.Keys.OrderBy(value => value, StringComparer.Ordinal)));
            }
            if (profile == null) return;
            if (!profile.IsValid)
            {
                throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "TensorRT rejected the completed dynamic input profile.");
            }
            int profileIndex = config.AddOptimizationProfile(profile);
            if (profileIndex < 0)
            {
                throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "TensorRT could not attach the dynamic input profile.");
            }
        }

        private static void ValidatePrecisionPolicy(
            ModelArtifact artifact,
            TensorRtNetworkDefinition network,
            TensorRtOnnxEnginePrecision precision)
        {
            if (precision != TensorRtOnnxEnginePrecision.Int8ExplicitQuantization) return;
            bool hasQuantize = false;
            bool hasDequantize = false;
            for (int layerIndex = 0; layerIndex < network.LayerCount; layerIndex++)
            {
                using TensorRtLayer layer = network.GetLayer(layerIndex);
                switch (layer.Type)
                {
                    case TensorRtLayerType.QuantizeTrt8:
                    case TensorRtLayerType.QuantizeTrt10:
                    case TensorRtLayerType.DynamicQuantize:
                        hasQuantize = true;
                        break;
                    case TensorRtLayerType.DequantizeTrt8:
                    case TensorRtLayerType.DequantizeTrt10:
                        hasDequantize = true;
                        break;
                }
            }
            if (!hasQuantize || !hasDequantize)
            {
                throw BuildFailure(
                    artifact,
                    TensorRtErrorCodes.ConfigurationInvalid,
                    "Explicit INT8 requires TensorRT to parse both Quantize and Dequantize layers from the ONNX graph.",
                    "quantize=" + hasQuantize + ";dequantize=" + hasDequantize);
            }
        }

        private static void ValidateProfileAgainstNetwork(ModelArtifact artifact, string inputName, TensorRtDims networkShape, TensorRtOnnxInputProfile profile)
        {
            if (profile.Minimum.Rank != networkShape.Rank)
            {
                throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "The ONNX input profile rank does not match the network input rank.", "input=" + inputName + ";networkRank=" + networkShape.Rank + ";profileRank=" + profile.Minimum.Rank);
            }
            for (int index = 0; index < networkShape.Rank; index++)
            {
                int declared = networkShape.Values[index];
                if (declared > 0 && (profile.Minimum[index] != declared || profile.Optimum[index] != declared || profile.Maximum[index] != declared))
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.ConfigurationInvalid, "A static ONNX input dimension must remain fixed in every optimization profile selector.", "input=" + inputName + ";dimension=" + index + ";declared=" + declared);
                }
            }
        }

        private static TensorRtDims ToDims(JYPPX.DeploySharp.Tensors.TensorShape shape)
        {
            long[] values = shape.ToArray();
            var dimensions = new int[values.Length];
            for (int index = 0; index < values.Length; index++) dimensions[index] = checked((int)values[index]);
            return new TensorRtDims(dimensions);
        }

        private static string ValidateOutputPath(ModelArtifact artifact, string enginePath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(enginePath)) throw new ArgumentException("A caller-owned engine output path is required.", nameof(enginePath));
            try
            {
                string fullPath = Path.GetFullPath(enginePath);
                string extension = Path.GetExtension(fullPath);
                if (!string.Equals(extension, ".engine", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".plan", StringComparison.OrdinalIgnoreCase))
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The TensorRT engine output must use the .engine or .plan extension.", fullPath);
                }
                string? directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The caller-owned TensorRT engine output directory does not exist.", fullPath);
                }
                FileAttributes directoryAttributes = File.GetAttributes(directory);
                if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The TensorRT engine output directory cannot be a reparse point.", directory);
                }
                if (Directory.Exists(fullPath))
                {
                    throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The TensorRT engine output path cannot be a directory.", fullPath);
                }
                if (File.Exists(fullPath))
                {
                    FileAttributes fileAttributes = File.GetAttributes(fullPath);
                    if ((fileAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    {
                        throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The existing TensorRT engine output must be a regular file.", fullPath);
                    }
                    if (!overwrite) throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The TensorRT engine output already exists and overwrite is disabled.", fullPath);
                }
                return fullPath;
            }
            catch (TensorRtBackendException) { throw; }
            catch (Exception exception)
            {
                throw BuildFailure(artifact, TensorRtErrorCodes.EngineOutputInvalid, "The TensorRT engine output path could not be validated.", exception.GetType().FullName);
            }
        }

        private static string FormatDiagnostics(IReadOnlyList<TensorRtOnnxParserDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0) return "diagnostics=none";
            return string.Join(" | ", diagnostics.Take(8).Select(diagnostic => diagnostic.ToString()));
        }

        private static string ComputeFileSha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
            using SHA256 algorithm = SHA256.Create();
            return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
        }

        private static string ComputeBuildInputsSha256(string onnxSha256, TensorRtOnnxEngineBuildOptions options)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("contract", "deploysharp-tensorrt-onnx-build-v1");
                writer.WriteString("dependency", "JYPPX.TensorRT.CSharp.API/4.0.0");
                writer.WriteString("dependencyContentHash", "jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==");
                writer.WriteString("onnxSha256", onnxSha256);
                writer.WriteNumber("apiVersion", (int)options.ApiVersion);
                writer.WriteNumber("precision", (int)options.Precision);
                writer.WriteNumber("workspaceBytes", options.WorkspaceBytes);
                writer.WriteNumber("optimizationLevel", options.OptimizationLevel);
                writer.WriteBoolean("stronglyTypedNetwork", options.StronglyTypedNetwork);
                writer.WriteStartArray("inputProfiles");
                foreach (TensorRtOnnxInputProfile profile in options.InputProfiles.OrderBy(item => item.InputName, StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("inputName", profile.InputName);
                    WriteShape(writer, "minimum", profile.Minimum);
                    WriteShape(writer, "optimum", profile.Optimum);
                    WriteShape(writer, "maximum", profile.Maximum);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return TensorRtOnnxModelArtifactValidator.ComputeSha256(stream.ToArray());
        }

        private static void WriteShape(Utf8JsonWriter writer, string propertyName, JYPPX.DeploySharp.Tensors.TensorShape shape)
        {
            writer.WriteStartArray(propertyName);
            foreach (long dimension in shape.ToArray()) writer.WriteNumberValue(dimension);
            writer.WriteEndArray();
        }

        private static TensorRtBackendException BuildFailure(ModelArtifact artifact, string errorCode, string message, string? details = null)
        {
            return new TensorRtBackendException(errorCode, message, modelId: artifact.ModelId, operation: "build-engine", technicalDetails: details);
        }

        private static void DisposeQuietly(IDisposable? resource)
        {
            try { resource?.Dispose(); }
            catch { }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
