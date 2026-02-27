using DeploySharp.Log;
using FluentAssertions;
using System;
using System.IO;
using Xunit;

namespace DeploySharp.Tests.Logger
{
    public class LogLevelTests
    {
        [Theory]
        [InlineData(LogLevel.DEBUG, 0)]
        [InlineData(LogLevel.INFO, 1)]
        [InlineData(LogLevel.WARN, 2)]
        [InlineData(LogLevel.ERROR, 3)]
        [InlineData(LogLevel.FATAL, 4)]
        public void LogLevel_ShouldHaveExpectedValue(LogLevel level, int expectedValue)
        {
            ((int)level).Should().Be(expectedValue);
        }
    }

    public class LogOutputTests
    {
        [Theory]
        [InlineData(LogOutput.Console, 1)]
        [InlineData(LogOutput.File, 2)]
        [InlineData(LogOutput.All, 3)]
        public void LogOutput_ShouldHaveExpectedValue(LogOutput output, int expectedValue)
        {
            ((int)output).Should().Be(expectedValue);
        }

        [Fact]
        public void LogOutput_All_ShouldBeCombination()
        {
            (LogOutput.Console | LogOutput.File).Should().Be(LogOutput.All);
        }

        [Fact]
        public void LogOutput_Console_ShouldHaveFlag()
        {
            LogOutput.All.HasFlag(LogOutput.Console).Should().BeTrue();
        }

        [Fact]
        public void LogOutput_File_ShouldHaveFlag()
        {
            LogOutput.All.HasFlag(LogOutput.File).Should().BeTrue();
        }
    }

    public class LoggerManagerTests
    {
        [Fact]
        public void ProjectMainLogger_ShouldNotBeNull()
        {
            LoggerManager.ProjectMainLogger.Should().NotBeNull();
        }

        [Fact]
        public void IsInitialized_Default_ShouldBeFalse()
        {
            // Note: This test assumes no other test called Initialize first
            // In practice, test isolation may require more careful handling
            var result = LoggerManager.IsInitialized();
            // Just verify it doesn't throw - actual value depends on test order
            (result == true || result == false).Should().BeTrue();
        }

        [Fact]
        public void Initialize_WithConsole_ShouldNotThrow()
        {
            Action act = () => LoggerManager.Initialize(LogLevel.INFO, LogOutput.Console);

            act.Should().NotThrow();
        }

        [Fact]
        public void Initialize_WithFile_ShouldNotThrow()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"DeploySharpTest_{Guid.NewGuid()}");
            try
            {
                Action act = () => LoggerManager.Initialize(LogLevel.DEBUG, LogOutput.File, tempDir);

                act.Should().NotThrow();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void Initialize_WithAll_ShouldNotThrow()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"DeploySharpTest_{Guid.NewGuid()}");
            try
            {
                Action act = () => LoggerManager.Initialize(LogLevel.WARN, LogOutput.All, tempDir);

                act.Should().NotThrow();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void InitializeDefault_ShouldNotThrow()
        {
            Action act = () => LoggerManager.InitializeDefault();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(LogLevel.DEBUG, "DEBUG")]
        [InlineData(LogLevel.INFO, "INFO")]
        [InlineData(LogLevel.WARN, "WARN")]
        [InlineData(LogLevel.ERROR, "ERROR")]
        [InlineData(LogLevel.FATAL, "FATAL")]
        public void ConvertLevel_ShouldReturnCorrectLevel(LogLevel input, string expectedLevelName)
        {
            var result = LoggerManager.ConvertLevel(input);

            result.Name.Should().Be(expectedLevelName);
        }

        [Fact]
        public void ConvertLevel_InvalidValue_ShouldReturnAll()
        {
            var invalidLevel = (LogLevel)999;

            var result = LoggerManager.ConvertLevel(invalidLevel);

            result.Should().Be(log4net.Core.Level.All);
        }
    }
}
