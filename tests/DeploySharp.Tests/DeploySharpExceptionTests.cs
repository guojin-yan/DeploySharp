using DeploySharp.Common;
using FluentAssertions;
using System;
using Xunit;

namespace DeploySharp.Tests
{
    public class DeploySharpExceptionTests
    {
        #region Constructor Tests - Basic

        [Fact]
        public void Constructor_WithMessage_ShouldSetMessage()
        {
            var ex = new DeploySharpException("Test error message");

            ex.Message.Should().Be("Test error message");
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
        {
            var innerEx = new InvalidOperationException("Inner exception");
            var ex = new DeploySharpException("Outer exception", innerEx);

            ex.Message.Should().Be("Outer exception");
            ex.InnerException.Should().Be(innerEx);
            ex.InnerException!.Message.Should().Be("Inner exception");
        }

        [Fact]
        public void Constructor_WithNullMessage_ShouldWork()
        {
            var ex = new DeploySharpException((string?)null);

            ex.Message.Should().Be("Exception of type 'DeploySharp.Common.DeploySharpException' was thrown.");
        }

        [Fact]
        public void Constructor_WithEmptyMessage_ShouldWork()
        {
            var ex = new DeploySharpException("");

            ex.Message.Should().BeEmpty();
        }

        #endregion

        #region Constructor Tests - With ErrorCode

        [Fact]
        public void Constructor_WithErrorCodeAndMessage_ShouldSetProperties()
        {
            var ex = new DeploySharpException("DEPLOY_001", "Configuration error");

            ex.ErrorCode.Should().Be("DEPLOY_001");
            ex.Message.Should().Be("Configuration error");
        }

        [Fact]
        public void Constructor_WithErrorCodeMessageAndTechnicalDetails_ShouldSetAll()
        {
            var ex = new DeploySharpException("DEPLOY_002", "Connection failed", "Timeout after 30s");

            ex.ErrorCode.Should().Be("DEPLOY_002");
            ex.Message.Should().Be("Connection failed");
            ex.TechnicalDetails.Should().Be("Timeout after 30s");
        }

        [Fact]
        public void Constructor_WithErrorCodeAndInnerException_ShouldSetAll()
        {
            var innerEx = new TimeoutException("Connection timeout");
            var ex = new DeploySharpException("DEPLOY_003", "Request failed", innerEx);

            ex.ErrorCode.Should().Be("DEPLOY_003");
            ex.Message.Should().Be("Request failed");
            ex.InnerException.Should().Be(innerEx);
        }

        [Fact]
        public void Constructor_FullParams_ShouldSetAllProperties()
        {
            var innerEx = new IOException("Disk error");
            var ex = new DeploySharpException("DEPLOY_004", "Save failed", innerEx, "Sector 7 bad");

            ex.ErrorCode.Should().Be("DEPLOY_004");
            ex.Message.Should().Be("Save failed");
            ex.InnerException.Should().Be(innerEx);
            ex.TechnicalDetails.Should().Be("Sector 7 bad");
        }

        #endregion

        #region Property Tests

        [Fact]
        public void ErrorCode_Default_ShouldBeNull()
        {
            var ex = new DeploySharpException("Simple message");

            ex.ErrorCode.Should().BeNull();
        }

        [Fact]
        public void TechnicalDetails_Default_ShouldBeNull()
        {
            var ex = new DeploySharpException("Simple message");

            ex.TechnicalDetails.Should().BeNull();
        }

        [Fact]
        public void TechnicalDetails_WithNullOptionalParam_ShouldBeNull()
        {
            var ex = new DeploySharpException("ERR_001", "Error");

            ex.TechnicalDetails.Should().BeNull();
        }

        #endregion

        #region Exception Hierarchy Tests

        [Fact]
        public void ShouldInheritFromException()
        {
            var ex = new DeploySharpException("Test");

            ex.Should().BeAssignableTo<Exception>();
        }

        [Fact]
        public void ShouldBeCatchableAsException()
        {
            try
            {
                throw new DeploySharpException("Test");
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<DeploySharpException>();
            }
        }

        #endregion

        #region InnerException Tests

        [Fact]
        public void InnerException_WithNull_ShouldBeNull()
        {
            var ex = new DeploySharpException("Test");

            ex.InnerException.Should().BeNull();
        }

        [Fact]
        public void InnerException_WithArgumentException_ShouldPreserveType()
        {
            var inner = new ArgumentException("Invalid argument");
            var ex = new DeploySharpException("Error", inner);

            ex.InnerException.Should().BeOfType<ArgumentException>();
        }

        [Fact]
        public void InnerException_WithNestedException_ShouldChainCorrectly()
        {
            var level3 = new Exception("Level 3");
            var level2 = new DeploySharpException("Level 2", level3);
            var level1 = new DeploySharpException("Level 1", level2);

            level1.InnerException.Should().Be(level2);
            level1.InnerException!.InnerException.Should().Be(level3);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WithErrorCode_ShouldContainErrorCode()
        {
            var ex = new DeploySharpException("DEPLOY_001", "Test error");

            ex.ToString().Should().Contain("Error Code: DEPLOY_001");
        }

        [Fact]
        public void ToString_WithTechnicalDetails_ShouldContainTechnicalDetails()
        {
            var ex = new DeploySharpException("DEPLOY_001", "Test error", "More info");

            ex.ToString().Should().Contain("Technical Details: More info");
        }

        [Fact]
        public void ToString_WithBoth_ShouldContainBoth()
        {
            var ex = new DeploySharpException("DEPLOY_001", "Test error", "Tech details");

            var str = ex.ToString();
            str.Should().Contain("Error Code: DEPLOY_001");
            str.Should().Contain("Technical Details: Tech details");
        }

        [Fact]
        public void ToString_WithoutExtras_ShouldNotContainErrorCodeLabel()
        {
            var ex = new DeploySharpException("Simple message");

            ex.ToString().Should().NotContain("Error Code:");
        }

        #endregion
    }
}
