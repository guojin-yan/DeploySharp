using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Tensors
{
    [TestClass]
    public sealed class NamedTensorCollectionTests
    {
        [TestMethod]
        public void PreservesOrderAndSupportsExactNameLookup()
        {
            var first = new Tensor<int>(new TensorShape(1), new[] { 1 });
            var second = new Tensor<int>(new TensorShape(1), new[] { 2 });
            var inputs = new InferenceInputs(new[]
            {
                new NamedTensor("first", first),
                new NamedTensor("second", second)
            });

            Assert.AreEqual("first", inputs[0].Name);
            Assert.AreSame(second, inputs.GetRequired("second"));
            Assert.IsFalse(inputs.TryGet("Second", out _));
        }

        [TestMethod]
        public void DuplicateNamesAreRejected()
        {
            var tensor = new Tensor<int>(new TensorShape(1), new[] { 1 });
            var values = new[]
            {
                new NamedTensor("input", tensor),
                new NamedTensor("input", tensor)
            };

            Assert.ThrowsExactly<ArgumentException>(() => new InferenceInputs(values));
        }

        [TestMethod]
        public void MissingRequiredNameThrowsKeyNotFound()
        {
            var inputs = InferenceInputs.Create(
                "input",
                new Tensor<int>(new TensorShape(1), new[] { 1 }));

            Assert.AreEqual(1, inputs.Count);
            Assert.AreSame(inputs[0].Tensor, inputs.GetRequired("input"));
            Assert.IsTrue(inputs.TryGet("input", out ITensor? found));
            Assert.AreSame(inputs[0].Tensor, found);
            Assert.IsFalse(inputs.TryGet("INPUT", out _));
            Assert.ThrowsExactly<KeyNotFoundException>(() => inputs.GetRequired("missing"));
        }

        [TestMethod]
        public void SingleOutputFactoryPreservesTensorAndName()
        {
            var tensor = new Tensor<float>(new TensorShape(1), new[] { 0.5f });
            var outputs = InferenceOutputs.Create("score", tensor);

            Assert.AreEqual(1, outputs.Count);
            Assert.AreEqual("score", outputs[0].Name);
            Assert.AreSame(tensor, outputs.GetRequired("score"));
        }
    }
}
