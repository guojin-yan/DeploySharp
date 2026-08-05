using System;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Tensors
{
    [TestClass]
    public sealed class TensorShapeTests
    {
        [TestMethod]
        public void StaticShapeCalculatesElementCount()
        {
            var shape = new TensorShape(1, 3, 224, 224);

            Assert.AreEqual(4, shape.Rank);
            Assert.AreEqual(150528L, shape.GetElementCount());
            Assert.IsFalse(shape.IsDynamic);
            Assert.AreEqual("[1,3,224,224]", shape.ToString());
        }

        [TestMethod]
        public void DynamicShapeRejectsElementCount()
        {
            var shape = new TensorShape(-1, 3, 640, 640);

            Assert.IsTrue(shape.IsDynamic);
            Assert.ThrowsExactly<InvalidOperationException>(() => shape.GetElementCount());
        }

        [TestMethod]
        public void InvalidDimensionIsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TensorShape(1, -2, 3));
        }

        [TestMethod]
        public void ScalarContainsOneElement()
        {
            Assert.AreEqual(0, TensorShape.Scalar.Rank);
            Assert.AreEqual(1L, TensorShape.Scalar.GetElementCount());
        }
    }
}
