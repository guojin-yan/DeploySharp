using DeploySharp.Engine;
using FluentAssertions;
using System;
using Xunit;

namespace DeploySharp.Tests.Engine
{
    public class InferEngineFactoryTests
    {
        [Fact(Skip = "Requires OpenVINO native libraries")]
        public void Create_WithOpenVINO_ShouldReturnOpenVinoInferEngine()
        {
            var engine = InferEngineFactory.Create(InferenceBackend.OpenVINO);

            engine.Should().NotBeNull();
            engine.Should().BeOfType<OpenVinoInferEngine>();
        }

        [Fact]
        public void Create_WithOnnxRuntime_ShouldReturnOnnxRuntimeInferEngine()
        {
            var engine = InferEngineFactory.Create(InferenceBackend.OnnxRuntime);

            engine.Should().NotBeNull();
            engine.Should().BeOfType<OnnxRuntimeInferEngine>();
        }

        [Fact(Skip = "Requires TensorRT native libraries")]
        public void Create_WithTensorRT_ShouldReturnTensorRtInferEngine()
        {
            var engine = InferEngineFactory.Create(InferenceBackend.TensorRT);

            engine.Should().NotBeNull();
            engine.Should().BeOfType<TensorRtInferEngine>();
        }

        [Fact]
        public void Create_WithInvalidBackend_ShouldThrowNotSupportedException()
        {
            var invalidBackend = (InferenceBackend)999;

            Action act = () => InferEngineFactory.Create(invalidBackend);

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*Unsupported inference backend*");
        }

        [Fact]
        public void Create_EngineShouldImplementIDisposable()
        {
            var engine = InferEngineFactory.Create(InferenceBackend.OnnxRuntime);

            engine.Should().BeAssignableTo<IDisposable>();
        }
    }
}
