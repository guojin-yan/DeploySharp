using System;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Tensors
{
    [TestClass]
    public sealed class TensorTests
    {
        [TestMethod]
        public void DefaultOwnershipCopiesCallerBuffer()
        {
            float[] values = { 1.0f, 2.0f };
            var tensor = new Tensor<float>(new TensorShape(1, 2), values);

            values[0] = 99.0f;

            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f }, tensor.ToArray());
            Assert.AreEqual(TensorBufferOwnership.Copy, tensor.Ownership);
            Assert.AreEqual(TensorElementType.Float32, tensor.ElementType);
        }

        [TestMethod]
        public void BorrowOwnershipUsesCallerBuffer()
        {
            int[] values = { 1, 2 };
            var tensor = new Tensor<int>(new TensorShape(2), values, TensorBufferOwnership.Borrow);

            values[0] = 7;

            CollectionAssert.AreEqual(new[] { 7, 2 }, tensor.ToArray());
        }

        [TestMethod]
        public void BufferLengthMustMatchShape()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new Tensor<float>(new TensorShape(1, 3), new[] { 1.0f, 2.0f }));
        }

        [TestMethod]
        public void RuntimeTensorRejectsDynamicShape()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new Tensor<float>(new TensorShape(-1), new[] { 1.0f }));
        }
    }
}
