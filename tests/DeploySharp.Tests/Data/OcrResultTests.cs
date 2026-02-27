using DeploySharp.Data;
using FluentAssertions;
using System;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class OcrResultTests
    {
        #region Property Tests

        [Fact]
        public void TextAreas_SetGet_ShouldWork()
        {
            var ocr = new OcrResult();
            var areas = new[]
            {
                new ObbResult { Bounds = new RotatedRect(new PointF(10, 10), new SizeF(50, 20), 0), Confidence = 0.95f },
                new ObbResult { Bounds = new RotatedRect(new PointF(100, 100), new SizeF(80, 30), 0), Confidence = 0.88f }
            };

            ocr.TextAreas = areas;

            ocr.TextAreas.Should().BeSameAs(areas);
        }

        [Fact]
        public void TextOrientations_SetGet_ShouldWork()
        {
            var ocr = new OcrResult();
            var orientations = new[]
            {
                new Result { Id = 0, Category = "0", Confidence = 0.99f },
                new Result { Id = 1, Category = "90", Confidence = 0.95f }
            };

            ocr.TextOrientations = orientations;

            ocr.TextOrientations.Should().BeSameAs(orientations);
        }

        [Fact]
        public void TextContents_SetGet_ShouldWork()
        {
            var ocr = new OcrResult();
            var contents = new[]
            {
                new TextRecResult { Text = "Hello", Confidence = 0.92f },
                new TextRecResult { Text = "World", Confidence = 0.88f }
            };

            ocr.TextContents = contents;

            ocr.TextContents.Should().BeSameAs(contents);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WithEmptyData_ShouldContainNoData()
        {
            var ocr = new OcrResult();

            var str = ocr.ToString();

            str.Should().Contain("OCR Recognition Results");
            str.Should().Contain("Total: 0");
        }

        [Fact]
        public void ToString_WithData_ShouldContainAllInfo()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[] { new ObbResult { Bounds = new RotatedRect(new PointF(10, 10), new SizeF(50, 20), 0), Confidence = 0.95f } },
                TextOrientations = new[] { new Result { Id = 0, Category = "0", Confidence = 0.99f } },
                TextContents = new[] { new TextRecResult { Text = "Test", Confidence = 0.92f } }
            };

            var str = ocr.ToString();

            str.Should().Contain("OCR Recognition Results");
            str.Should().Contain("Total: 1");
            str.Should().Contain("Test");
        }

        [Fact]
        public void TextContentsToString_WithData_ShouldContainText()
        {
            var ocr = new OcrResult
            {
                TextContents = new[] { new TextRecResult { Text = "Hello World", Confidence = 0.95f } }
            };

            var str = ocr.TextContentsToString();

            str.Should().Contain("Hello World");
            str.Should().Contain("0.95");
        }

        [Fact]
        public void TextContentsToString_WithNoData_ShouldReturnMessage()
        {
            var ocr = new OcrResult();

            var str = ocr.TextContentsToString();

            str.Should().Contain("OCR Text Recognition Results");
            str.Should().Contain("Total: 0");
        }

        [Fact]
        public void TextAreasToString_WithData_ShouldContainAreaInfo()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[] { new ObbResult { Bounds = new RotatedRect(new PointF(10, 10), new SizeF(50, 20), 0), Confidence = 0.95f } }
            };

            var str = ocr.TextAreasToString();

            str.Should().Contain("Text Area Detection Results");
            str.Should().Contain("0.95");
        }

        [Fact]
        public void TextAreasToString_WithNoData_ShouldReturnMessage()
        {
            var ocr = new OcrResult();

            var str = ocr.TextAreasToString();

            str.Should().Contain("Text Area Detection Results");
            str.Should().Contain("Total: 0");
        }

        [Fact]
        public void TextOrientationsToString_WithData_ShouldContainOrientation()
        {
            var ocr = new OcrResult
            {
                TextOrientations = new[] { new Result { Id = 0, Category = "0", Confidence = 0.99f } }
            };

            var str = ocr.TextOrientationsToString();

            str.Should().Contain("Text Orientation Results");
            str.Should().Contain("0");
            str.Should().Contain("0.99");
        }

        [Fact]
        public void TextOrientationsToString_WithNoData_ShouldReturnMessage()
        {
            var ocr = new OcrResult();

            var str = ocr.TextOrientationsToString();

            str.Should().Contain("Text Orientation Results");
            str.Should().Contain("Total: 0");
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void SortByX_Ascending_ShouldSortByX()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(100, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(10, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 50), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByX(true);

            ocr.TextAreas[0].Bounds.Center.X.Should().Be(10);
            ocr.TextAreas[1].Bounds.Center.X.Should().Be(50);
            ocr.TextAreas[2].Bounds.Center.X.Should().Be(100);
        }

        [Fact]
        public void SortByX_Descending_ShouldSortByXDescending()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(10, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(100, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 50), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByX(false);

            ocr.TextAreas[0].Bounds.Center.X.Should().Be(100);
            ocr.TextAreas[1].Bounds.Center.X.Should().Be(50);
            ocr.TextAreas[2].Bounds.Center.X.Should().Be(10);
        }

        [Fact]
        public void SortByX_WithNullAreas_ShouldNotThrow()
        {
            var ocr = new OcrResult { TextAreas = null };

            Action act = () => ocr.SortByX();

            act.Should().NotThrow();
        }

        [Fact]
        public void SortByY_Ascending_ShouldSortByY()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 100), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 10), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 50), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByY(true);

            ocr.TextAreas[0].Bounds.Center.Y.Should().Be(10);
            ocr.TextAreas[1].Bounds.Center.Y.Should().Be(50);
            ocr.TextAreas[2].Bounds.Center.Y.Should().Be(100);
        }

        [Fact]
        public void SortByY_Descending_ShouldSortByYDescending()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 10), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 100), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 50), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByY(false);

            ocr.TextAreas[0].Bounds.Center.Y.Should().Be(100);
            ocr.TextAreas[1].Bounds.Center.Y.Should().Be(50);
            ocr.TextAreas[2].Bounds.Center.Y.Should().Be(10);
        }

        [Fact]
        public void SortByYThenX_ShouldSortByYThenX()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(100, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(10, 50), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 10), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByYThenX(true, true);

            // First by Y (10, then 50s), then by X within same Y
            ocr.TextAreas[0].Bounds.Center.Y.Should().Be(10);
            ocr.TextAreas[1].Bounds.Center.X.Should().Be(10);
            ocr.TextAreas[2].Bounds.Center.X.Should().Be(100);
        }

        [Fact]
        public void SortByXThenY_ShouldSortByXThenY()
        {
            var ocr = new OcrResult
            {
                TextAreas = new[]
                {
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 100), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(50, 10), new SizeF(50, 20), 0) },
                    new ObbResult { Bounds = new RotatedRect(new PointF(10, 50), new SizeF(50, 20), 0) }
                },
                TextOrientations = new[] { new Result(), new Result(), new Result() },
                TextContents = new[] { new TextRecResult(), new TextRecResult(), new TextRecResult() }
            };

            ocr.SortByXThenY(true, true);

            // First by X (10, then 50s), then by Y within same X
            ocr.TextAreas[0].Bounds.Center.X.Should().Be(10);
            ocr.TextAreas[1].Bounds.Center.X.Should().Be(50);
            ocr.TextAreas[1].Bounds.Center.Y.Should().Be(10);
            ocr.TextAreas[2].Bounds.Center.Y.Should().Be(100);
        }

        #endregion
    }
}
