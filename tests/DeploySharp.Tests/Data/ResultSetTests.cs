using DeploySharp.Data;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ResultSetTests
    {
        [Fact]
        public void Constructor_WithValidArray_ShouldSetPredictions()
        {
            var results = new[] { new Result { Id = 1 }, new Result { Id = 2 } };

            var resultSet = new ResultSet<Result>(results);

            resultSet.Predictions.Should().BeSameAs(results);
        }

        [Fact]
        public void Constructor_WithNullArray_ShouldThrowArgumentNullException()
        {
            Action act = () => new ResultSet<Result>(null!);

            act.Should().Throw<ArgumentNullException>().WithParameterName("predictions");
        }

        [Fact]
        public void Indexer_WithValidIndex_ShouldReturnItem()
        {
            var results = new[] { new Result { Id = 1 }, new Result { Id = 2 } };
            var resultSet = new ResultSet<Result>(results);

            var item = resultSet[0];

            item.Id.Should().Be(1);
        }

        [Fact]
        public void Indexer_WithDifferentIndex_ShouldReturnCorrectItem()
        {
            var results = new[] { new Result { Id = 1 }, new Result { Id = 2 } };
            var resultSet = new ResultSet<Result>(results);

            var item = resultSet[1];

            item.Id.Should().Be(2);
        }

        [Fact]
        public void Count_ShouldReturnArrayLength()
        {
            var results = new[] { new Result { Id = 1 }, new Result { Id = 2 }, new Result { Id = 3 } };
            var resultSet = new ResultSet<Result>(results);

            resultSet.Count.Should().Be(3);
        }

        [Fact]
        public void Count_WithEmptyArray_ShouldBeZero()
        {
            var results = Array.Empty<Result>();
            var resultSet = new ResultSet<Result>(results);

            resultSet.Count.Should().Be(0);
        }

        [Fact]
        public void GetEnumerator_ShouldIterateAllItems()
        {
            var results = new[] { new Result { Id = 1 }, new Result { Id = 2 }, new Result { Id = 3 } };
            var resultSet = new ResultSet<Result>(results);

            var items = new List<Result>();
            foreach (var item in resultSet)
            {
                items.Add(item);
            }

            items.Should().HaveCount(3);
            items.Select(r => r.Id).Should().Equal(1, 2, 3);
        }

        [Fact]
        public void IEnumerableGetEnumerator_ShouldReturnSameEnumerator()
        {
            var results = new[] { new Result { Id = 1 } };
            var resultSet = new ResultSet<Result>(results);

            var enumerator = ((System.Collections.IEnumerable)resultSet).GetEnumerator();

            enumerator.Should().NotBeNull();
        }

        [Fact]
        public void ToString_ShouldContainTypeName()
        {
            var results = new[] { new Result { Id = 1 } };
            var resultSet = new ResultSet<Result>(results);

            var str = resultSet.ToString();

            str.Should().Contain("Result");
            str.Should().Contain("1 predictions");
        }

        [Fact]
        public void ToString_WithMultipleResults_ShouldContainAllPredictions()
        {
            var results = new[] { new Result { Id = 1, Category = "cat1" }, new Result { Id = 2, Category = "cat2" } };
            var resultSet = new ResultSet<Result>(results);

            var str = resultSet.ToString();

            str.Should().Contain("2 predictions");
        }

        [Fact]
        public void UpdateCategory_WithValidCategories_ShouldUpdateAll()
        {
            var results = new[] { new Result { Id = 0 }, new Result { Id = 1 } };
            var resultSet = new ResultSet<Result>(results);
            var categories = new[] { "class0", "class1" };

            resultSet.UpdateCategory(categories);

            results[0].Category.Should().Be("class0");
            results[1].Category.Should().Be("class1");
        }

        [Fact]
        public void UpdateCategory_WithDetResult_ShouldUpdateAll()
        {
            var results = new[]
            {
                new DetResult { Id = 0, Bounds = new Rect(0, 0, 10, 10) },
                new DetResult { Id = 1, Bounds = new Rect(10, 10, 20, 20) }
            };
            var resultSet = new ResultSet<DetResult>(results);
            var categories = new[] { "person", "car" };

            resultSet.UpdateCategory(categories);

            results[0].Category.Should().Be("person");
            results[1].Category.Should().Be("car");
        }

        [Fact]
        public void Predictions_Setter_ShouldUpdateArray()
        {
            var results1 = new[] { new Result { Id = 1 } };
            var results2 = new[] { new Result { Id = 2 } };
            var resultSet = new ResultSet<Result>(results1);

            resultSet.Predictions = results2;

            resultSet[0].Id.Should().Be(2);
            resultSet.Count.Should().Be(1);
        }
    }
}
