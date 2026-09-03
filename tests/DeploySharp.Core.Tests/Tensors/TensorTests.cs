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

        [TestMethod]
        public void SequenceArgMaxResultOwnsCompactTraceAndChecksBounds()
        {
            int[] classes = { 1, 2, 3, 0 };
            float[] confidences = { .9f, .8f, .7f, .6f };
            int[] invalid = { -1, 5 };
            var result = new SequenceArgMaxResult(2, 2, 4, classes, confidences, invalid);

            classes[0] = 99;
            confidences[0] = 0;
            invalid[0] = 0;

            Assert.AreEqual(1, result.GetClassIndex(0, 0));
            Assert.AreEqual(.9f, result.GetConfidence(0, 0));
            Assert.AreEqual(-1, result.GetInvalidOffset(0));
            Assert.AreEqual(5, result.GetInvalidOffset(1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => result.GetClassIndex(2, 0));
            Assert.ThrowsExactly<ArgumentException>(() => new SequenceArgMaxResult(2, 2, 4, new int[3], new float[4], new int[2]));
        }
    }
}
