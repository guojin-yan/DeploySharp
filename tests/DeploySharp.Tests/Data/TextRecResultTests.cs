using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class TextRecResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToTextRecResult()
        {
            var result = new TextRecResult();

            result.Type.Should().Be(ResultType.TextRecResult);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void TextRecResult_ShouldInheritFromResult()
        {
            var result = new TextRecResult();

            result.Should().BeAssignableTo<Result>();
        }

        [Fact]
        public void Text_SetGet_ShouldWork()
        {
            var result = new TextRecResult();
            result.Text = "Hello World";

            result.Text.Should().Be("Hello World");
        }

        [Fact]
        public void Text_WithEmptyString_ShouldWork()
        {
            var result = new TextRecResult();
            result.Text = "";

            result.Text.Should().BeEmpty();
        }

        [Fact]
        public void Text_DefaultValue_ShouldBeEmptyString()
        {
            var result = new TextRecResult();

            result.Text.Should().BeEmpty();
        }

        #endregion

        #region Inherited Property Tests

        [Fact]
        public void Id_SetGet_ShouldWork()
        {
            var result = new TextRecResult { Id = 5 };

            result.Id.Should().Be(5);
        }

        [Fact]
        public void Confidence_SetGet_ShouldWork()
        {
            var result = new TextRecResult { Confidence = 0.88f };

            result.Confidence.Should().Be(0.88f);
        }

        [Fact]
        public void Type_SetGet_ShouldWork()
        {
            var result = new TextRecResult { Type = ResultType.Detection };

            result.Type.Should().Be(ResultType.Detection);
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateNewInstance()
        {
            var result = new TextRecResult
            {
                Id = 3,
                Text = "Recognized Text",
                Confidence = 0.92f
            };

            var cloned = result.Clone();

            cloned.Should().NotBeNull();
            cloned!.Id.Should().Be(3);
            cloned.Text.Should().Be("Recognized Text");
            cloned.Confidence.Should().Be(0.92f);
        }

        [Fact]
        public void Clone_ShouldNotBeSameReference()
        {
            var result = new TextRecResult { Text = "Test" };

            var cloned = result.Clone();

            cloned.Should().NotBeSameAs(result);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new TextRecResult
            {
                Text = "Original",
                Confidence = 0.9f
            };

            var cloned = original.Clone();
            cloned.Text = "Modified";
            cloned.Confidence = 0.5f;

            original.Text.Should().Be("Original");
            original.Confidence.Should().Be(0.9f);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldContainText()
        {
            var result = new TextRecResult { Text = "Hello" };

            result.ToString().Should().Contain("Hello");
        }

        [Fact]
        public void ToString_ShouldContainTextLabel()
        {
            var result = new TextRecResult { Text = "Test" };

            result.ToString().Should().Contain("Text:");
        }

        [Fact]
        public void ToString_WithEmptyText_ShouldContainEmptyQuotes()
        {
            var result = new TextRecResult { Text = "" };

            result.ToString().Should().Contain("Text: \"\"");
        }

        #endregion
    }
}
