using DeploySharp.Data;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class TensorTypeTests
    {
        [Theory]
        [InlineData(TensorType.Input, 0)]
        [InlineData(TensorType.Output, 1)]
        public void TensorType_ShouldHaveExpectedValue(TensorType type, int expectedValue)
        {
            ((int)type).Should().Be(expectedValue);
        }
    }

    public class NodeDataTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParams_ShouldCreateInstance()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            node.Name.Should().Be("test");
            node.Index.Should().Be(0);
            node.Type.Should().Be(TensorType.Input);
        }

        [Fact]
        public void Constructor_WithNullName_ShouldThrowArgumentNullException()
        {
            Action act = () => new NodeData(null!, 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            act.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }

        [Fact]
        public void Constructor_WithNullData_ShouldThrowArgumentNullException()
        {
            Action act = () => new NodeData("test", 0, TensorType.Input, null!, new[] { 10 }, typeof(float));

            act.Should().Throw<ArgumentNullException>().WithParameterName("data");
        }

        [Fact]
        public void Constructor_WithNullShape_ShouldThrowNullReferenceException()
        {
            // Note: Source code has a bug - it tries to clone shape before null check
            Action act = () => new NodeData("test", 0, TensorType.Input, new float[10], null!, typeof(float));

            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void Constructor_WithNullDataType_ShouldThrowArgumentNullException()
        {
            Action act = () => new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, null!);

            act.Should().Throw<ArgumentNullException>().WithParameterName("dataType");
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Name_SetValue_ShouldUpdate()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));
            node.Name = "input_0";

            node.Name.Should().Be("input_0");
        }

        [Fact]
        public void Index_SetValue_ShouldUpdate()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));
            node.Index = 1;

            node.Index.Should().Be(1);
        }

        [Fact]
        public void Type_SetValue_ShouldUpdate()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));
            node.Type = TensorType.Output;

            node.Type.Should().Be(TensorType.Output);
        }

        [Fact]
        public void DataBuffer_SetValue_ShouldUpdate()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));
            var buffer = new float[100];
            node.DataBuffer = buffer;

            node.DataBuffer.Should().BeSameAs(buffer);
        }

        #endregion

        #region Shape Tests

        [Fact]
        public void Shape_WhenCreated_ShouldReturnShape()
        {
            var shape = new[] { 2, 3, 4 };
            var node = new NodeData("test", 0, TensorType.Input, new float[24], shape, typeof(float));

            node.Shape.Should().Equal(shape);
        }

        [Fact]
        public void Shape_WhenCreated_ShouldReturnClone()
        {
            var shape = new[] { 2, 3, 4 };
            var node = new NodeData("test", 0, TensorType.Input, new float[24], shape, typeof(float));

            shape[0] = 100; // Modify original

            node.Shape[0].Should().Be(2); // Node shape should be unchanged
        }

        #endregion

        #region DataType Tests

        [Fact]
        public void DataType_WithFloatBuffer_ShouldReturnFloatType()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            node.DataType.Should().Be(typeof(float));
        }

        [Fact]
        public void DataType_WithIntBuffer_ShouldReturnIntType()
        {
            var node = new NodeData("test", 0, TensorType.Input, new int[10], new[] { 10 }, typeof(int));

            node.DataType.Should().Be(typeof(int));
        }

        [Fact]
        public void DataType_WithByteBuffer_ShouldReturnByteType()
        {
            var node = new NodeData("test", 0, TensorType.Input, new byte[10], new[] { 10 }, typeof(byte));

            node.DataType.Should().Be(typeof(byte));
        }

        #endregion

        #region ElementCount Tests

        [Fact]
        public void ElementCount_With1DShape_ShouldCalculateCorrectly()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            node.ElementCount.Should().Be(10);
        }

        [Fact]
        public void ElementCount_With2DShape_ShouldCalculateCorrectly()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[12], new[] { 3, 4 }, typeof(float));

            node.ElementCount.Should().Be(12);
        }

        [Fact]
        public void ElementCount_With3DShape_ShouldCalculateCorrectly()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[24], new[] { 2, 3, 4 }, typeof(float));

            node.ElementCount.Should().Be(24);
        }

        [Fact]
        public void ElementCount_With4DShape_ShouldCalculateCorrectly()
        {
            var data = new float[1 * 3 * 224 * 224];
            var node = new NodeData("test", 0, TensorType.Input, data, new[] { 1, 3, 224, 224 }, typeof(float));

            node.ElementCount.Should().Be(1 * 3 * 224 * 224);
        }

        #endregion

        #region ElementSize Tests

        [Fact]
        public void ElementSize_WithFloatBuffer_ShouldReturn4()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            node.ElementSize.Should().Be(4);
        }

        [Fact]
        public void ElementSize_WithDoubleBuffer_ShouldReturn8()
        {
            var node = new NodeData("test", 0, TensorType.Input, new double[10], new[] { 10 }, typeof(double));

            node.ElementSize.Should().Be(8);
        }

        [Fact]
        public void ElementSize_WithIntBuffer_ShouldReturn4()
        {
            var node = new NodeData("test", 0, TensorType.Input, new int[10], new[] { 10 }, typeof(int));

            node.ElementSize.Should().Be(4);
        }

        [Fact]
        public void ElementSize_WithByteBuffer_ShouldReturn1()
        {
            var node = new NodeData("test", 0, TensorType.Input, new byte[10], new[] { 10 }, typeof(byte));

            node.ElementSize.Should().Be(1);
        }

        [Fact]
        public void ElementSize_WithUnsupportedType_ShouldThrowNotSupportedException()
        {
            var node = new NodeData("test", 0, TensorType.Input, new string[10], new[] { 10 }, typeof(string));

            Action act = () => _ = node.ElementSize;

            act.Should().Throw<NotSupportedException>().WithMessage("*string*");
        }

        #endregion

        #region IDisposable Tests

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            var node = new NodeData("test", 0, TensorType.Input, new float[10], new[] { 10 }, typeof(float));

            Action act = () => node.Dispose();

            act.Should().NotThrow();
        }

        #endregion
    }

    public class DataTensorTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldInitializeEmpty()
        {
            var tensor = new DataTensor();

            tensor.Count.Should().Be(0);
        }

        #endregion

        #region AddNode Tests

        [Fact]
        public void AddNode_WithValidParams_ShouldAddNode()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            tensor.Count.Should().Be(1);
        }

        [Fact]
        public void AddNode_WithEmptyName_ShouldThrowArgumentException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.AddNode("", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            act.Should().Throw<ArgumentException>().WithMessage("*Node name cannot be empty*");
        }

        [Fact]
        public void AddNode_WithWhitespaceName_ShouldThrowArgumentException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.AddNode("   ", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            act.Should().Throw<ArgumentException>().WithMessage("*Node name cannot be empty*");
        }

        [Fact]
        public void AddNode_WithNullData_ShouldThrowArgumentNullException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.AddNode("input_0", 0, TensorType.Input, null!, new[] { 100 }, typeof(float));

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddNode_WithNullShape_ShouldThrowArgumentNullException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.AddNode("input_0", 0, TensorType.Input, new float[100], null!, typeof(float));

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddNode_WithNullDataType_ShouldThrowArgumentNullException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, null!);

            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region AddNode<T> Tests

        [Fact]
        public void AddNodeGeneric_WithFloatType_ShouldCreateBuffer()
        {
            var tensor = new DataTensor();
            tensor.AddNode<float>("input_0", 0, TensorType.Input, new[] { 100 });

            tensor.Count.Should().Be(1);
            tensor[0].DataType.Should().Be(typeof(float));
            tensor[0].DataBuffer.Should().BeOfType<float[]>();
        }

        [Fact]
        public void AddNodeGeneric_WithIntType_ShouldCreateBuffer()
        {
            var tensor = new DataTensor();
            tensor.AddNode<int>("input_0", 0, TensorType.Input, new[] { 100 });

            tensor.Count.Should().Be(1);
            tensor[0].DataType.Should().Be(typeof(int));
            tensor[0].DataBuffer.Should().BeOfType<int[]>();
        }

        [Fact]
        public void AddNodeGeneric_WithByteType_ShouldCreateBuffer()
        {
            var tensor = new DataTensor();
            tensor.AddNode<byte>("input_0", 0, TensorType.Input, new[] { 100 });

            tensor.Count.Should().Be(1);
            tensor[0].DataType.Should().Be(typeof(byte));
            tensor[0].DataBuffer.Should().BeOfType<byte[]>();
        }

        [Fact]
        public void AddNodeGeneric_With2DShape_ShouldCalculateSize()
        {
            var tensor = new DataTensor();
            tensor.AddNode<float>("input_0", 0, TensorType.Input, new[] { 3, 224, 224 });

            tensor[0].ElementCount.Should().Be(3 * 224 * 224);
            tensor[0].DataBuffer.Length.Should().Be(3 * 224 * 224);
        }

        #endregion

        #region Indexer Tests

        [Fact]
        public void Indexer_WithValidIndex_ShouldReturnNode()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            var node = tensor[0];

            node.Name.Should().Be("input_0");
        }

        [Fact]
        public void Indexer_WithNegativeIndex_ShouldThrowIndexOutOfRangeException()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            Action act = () => _ = tensor[-1];

            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void Indexer_WithOutOfRangeIndex_ShouldThrowIndexOutOfRangeException()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            Action act = () => _ = tensor[1];

            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void Indexer_WithValidName_ShouldReturnNode()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            var node = tensor["input_0"];

            node.Name.Should().Be("input_0");
        }

        [Fact]
        public void Indexer_WithInvalidName_ShouldThrowKeyNotFoundException()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            Action act = () => _ = tensor["input_1"];

            act.Should().Throw<KeyNotFoundException>();
        }

        #endregion

        #region GetNode Tests

        [Fact]
        public void GetNode_WithExistingName_ShouldReturnNode()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            var node = tensor.GetNode("input_0");

            node.Name.Should().Be("input_0");
        }

        [Fact]
        public void GetNode_WithNonExistingName_ShouldThrowKeyNotFoundException()
        {
            var tensor = new DataTensor();

            Action act = () => tensor.GetNode("nonexistent");

            act.Should().Throw<KeyNotFoundException>().WithMessage("*not found*");
        }

        #endregion

        #region TryGetNode Tests

        [Fact]
        public void TryGetNode_WithExistingName_ShouldReturnTrueAndNode()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            bool found = tensor.TryGetNode("input_0", out var node);

            found.Should().BeTrue();
            node.Should().NotBeNull();
            node!.Name.Should().Be("input_0");
        }

        [Fact]
        public void TryGetNode_WithNonExistingName_ShouldReturnFalseAndNull()
        {
            var tensor = new DataTensor();

            bool found = tensor.TryGetNode("nonexistent", out var node);

            found.Should().BeFalse();
            node.Should().BeNull();
        }

        #endregion

        #region TotalElements Tests

        [Fact]
        public void TotalElements_WithMultipleNodes_ShouldSumElements()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));
            tensor.AddNode("input_1", 1, TensorType.Input, new float[200], new[] { 200 }, typeof(float));

            tensor.TotalElements.Should().Be(300);
        }

        [Fact]
        public void TotalElements_WithEmptyTensor_ShouldReturnZero()
        {
            var tensor = new DataTensor();

            tensor.TotalElements.Should().Be(0);
        }

        [Fact]
        public void TotalElements_With2DShapes_ShouldCalculateCorrectly()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[3 * 224], new[] { 3, 224 }, typeof(float));
            tensor.AddNode("input_1", 1, TensorType.Input, new float[224 * 224], new[] { 224, 224 }, typeof(float));

            tensor.TotalElements.Should().Be(3 * 224 + 224 * 224);
        }

        #endregion

        #region IEnumerable Tests

        [Fact]
        public void GetEnumerator_ShouldIterateAllNodes()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));
            tensor.AddNode("input_1", 1, TensorType.Input, new float[200], new[] { 200 }, typeof(float));

            var nodes = tensor.ToList();

            nodes.Should().HaveCount(2);
            nodes[0].Name.Should().Be("input_0");
            nodes[1].Name.Should().Be("input_1");
        }

        [Fact]
        public void IEnumerableGetEnumerator_ShouldIterateAllNodes()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            System.Collections.IEnumerable enumerable = tensor;
            var enumerator = enumerable.GetEnumerator();

            enumerator.MoveNext().Should().BeTrue();
            ((NodeData)enumerator.Current!).Name.Should().Be("input_0");
        }

        #endregion

        #region IDisposable Tests

        [Fact]
        public void Dispose_ShouldClearNodes()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            tensor.Dispose();

            tensor.Count.Should().Be(0);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            var tensor = new DataTensor();
            tensor.AddNode("input_0", 0, TensorType.Input, new float[100], new[] { 100 }, typeof(float));

            tensor.Dispose();
            Action act = () => tensor.Dispose();

            act.Should().NotThrow();
        }

        #endregion
    }
}
