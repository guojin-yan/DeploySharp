using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    internal static class OnnxRuntimeTestData
    {
        public static readonly BackendId BackendId = OnnxRuntimeBackendProvider.BackendId;

        public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

        public static ModelArtifact Artifact(string name, string? hash = null)
        {
            string id = "tests/onnxruntime-" + Path.GetFileNameWithoutExtension(name);
            return new ModelArtifact(new ModelId(id), "onnx", Fixture(name), hash, BackendId);
        }

        public static IInferenceSession Open(string name, SessionOptions? sessionOptions = null, OnnxRuntimeOptions? backendOptions = null)
        {
            var provider = new OnnxRuntimeBackendProvider(backendOptions);
            try
            {
                return provider.CreateSession(Artifact(name), new BackendRequest(BackendCapabilities.TensorInference, BackendId, "cpu"), sessionOptions ?? SessionOptions.Default);
            }
            finally { provider.Dispose(); }
        }

        public static InferenceInputs ClassificationInputs()
        {
            return InferenceInputs.Create("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new[]
            {
                1f, 1f, 1f, 1f,
                2f, 2f, 2f, 2f,
                3f, 3f, 3f, 3f
            }));
        }

        public static InferenceInputs LongRunningInputs()
        {
            var values = new float[128 * 128];
            for (int index = 0; index < values.Length; index++) values[index] = index % 17;
            return InferenceInputs.Create("state", new Tensor<float>(new TensorShape(128, 128), values));
        }

        public static InferenceInputs LoopInputs(long tripCount)
        {
            return new InferenceInputs(new[]
            {
                new NamedTensor("state", new Tensor<float>(TensorShape.Scalar, new[] { 0f })),
                new NamedTensor("trip_count", new Tensor<long>(TensorShape.Scalar, new[] { tripCount }))
            });
        }

        public static string Sha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var builder = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(stream)) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        public static InferenceInputs NumericInputs()
        {
            return new InferenceInputs(new List<NamedTensor>
            {
                new NamedTensor("bool_in", new Tensor<bool>(new TensorShape(2), new[] { true, false })),
                new NamedTensor("int8_in", new Tensor<sbyte>(new TensorShape(2), new sbyte[] { -2, 3 })),
                new NamedTensor("uint8_in", new Tensor<byte>(new TensorShape(2), new byte[] { 2, 3 })),
                new NamedTensor("int16_in", new Tensor<short>(new TensorShape(2), new short[] { -20, 30 })),
                new NamedTensor("uint16_in", new Tensor<ushort>(new TensorShape(2), new ushort[] { 20, 30 })),
                new NamedTensor("int32_in", new Tensor<int>(new TensorShape(2), new[] { -200, 300 })),
                new NamedTensor("uint32_in", new Tensor<uint>(new TensorShape(2), new uint[] { 200, 300 })),
                new NamedTensor("int64_in", new Tensor<long>(new TensorShape(2), new long[] { -2000, 3000 })),
                new NamedTensor("uint64_in", new Tensor<ulong>(new TensorShape(2), new ulong[] { 2000, 3000 })),
                new NamedTensor("float32_in", new Tensor<float>(new TensorShape(2), new[] { 1.25f, -2.5f })),
                new NamedTensor("float64_in", new Tensor<double>(new TensorShape(2), new[] { 1.25d, -2.5d }))
            });
        }
    }
}
