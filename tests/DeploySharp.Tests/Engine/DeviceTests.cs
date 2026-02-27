using DeploySharp.Engine;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Engine
{
    public class InferenceBackendTests
    {
        [Theory]
        [InlineData(InferenceBackend.OpenVINO, 0)]
        [InlineData(InferenceBackend.OnnxRuntime, 1)]
        [InlineData(InferenceBackend.TensorRT, 2)]
        public void InferenceBackend_ShouldHaveExpectedValue(InferenceBackend backend, int expectedValue)
        {
            ((int)backend).Should().Be(expectedValue);
        }

        [Theory]
        [InlineData(InferenceBackend.OpenVINO, "OpenVINO")]
        [InlineData(InferenceBackend.OnnxRuntime, "OnnxRuntime")]
        [InlineData(InferenceBackend.TensorRT, "TensorRT")]
        public void GetDisplayName_ShouldReturnCorrectName(InferenceBackend backend, string expectedName)
        {
            backend.GetDisplayName().Should().Be(expectedName);
        }
    }

    public class DeviceTypeTests
    {
        [Theory]
        [InlineData(DeviceType.AUTO, "AUTO")]
        [InlineData(DeviceType.CPU, "CPU")]
        [InlineData(DeviceType.GPU0, "GPU.0")]
        [InlineData(DeviceType.GPU1, "GPU.1")]
        [InlineData(DeviceType.NPU, "NPU")]
        public void GetDisplayName_ShouldReturnCorrectName(DeviceType device, string expectedName)
        {
            device.GetDisplayName().Should().Be(expectedName);
        }
    }

    public class OnnxRuntimeDeviceTypeTests
    {
        [Theory]
        [InlineData(OnnxRuntimeDeviceType.Default, 0)]
        [InlineData(OnnxRuntimeDeviceType.OpenVINO, 1)]
        [InlineData(OnnxRuntimeDeviceType.Dnnl, 2)]
        [InlineData(OnnxRuntimeDeviceType.Cuda, 3)]
        [InlineData(OnnxRuntimeDeviceType.TensorRT, 4)]
        [InlineData(OnnxRuntimeDeviceType.DML, 5)]
        [InlineData(OnnxRuntimeDeviceType.ROCm, 6)]
        [InlineData(OnnxRuntimeDeviceType.MIGraphX, 7)]
        public void OnnxRuntimeDeviceType_ShouldHaveExpectedValue(OnnxRuntimeDeviceType device, int expectedValue)
        {
            ((int)device).Should().Be(expectedValue);
        }
    }

    public class DisplayNameAttributeTests
    {
        [Fact]
        public void Name_ShouldReturnCorrectValue()
        {
            var attr = new DisplayNameAttribute("TestName");

            attr.Name.Should().Be("TestName");
        }
    }
}
