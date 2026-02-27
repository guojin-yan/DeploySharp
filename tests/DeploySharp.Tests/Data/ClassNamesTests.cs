using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ClassNamesTests
    {
        #region COCO Class Names Tests

        [Fact]
        public void CocoClassNames_ShouldHave80Classes()
        {
            ClassNames.CocoClassNames.Should().HaveCount(80);
        }

        [Theory]
        [InlineData(0, "person")]
        [InlineData(1, "bicycle")]
        [InlineData(2, "car")]
        [InlineData(3, "motorcycle")]
        [InlineData(4, "airplane")]
        [InlineData(15, "cat")]
        [InlineData(16, "dog")]
        [InlineData(79, "toothbrush")]
        public void CocoClassNames_ShouldContainExpectedClasses(int classId, string expectedName)
        {
            ClassNames.CocoClassNames.Should().ContainKey(classId);
            ClassNames.CocoClassNames[classId].Should().Be(expectedName);
        }

        #endregion

        #region PASCAL VOC Class Names Tests

        [Fact]
        public void VocClassNames_ShouldHave20Classes()
        {
            ClassNames.VocClassNames.Should().HaveCount(20);
        }

        [Theory]
        [InlineData(1, "aeroplane")]
        [InlineData(2, "bicycle")]
        [InlineData(6, "bus")]
        [InlineData(7, "car")]
        [InlineData(15, "person")]
        [InlineData(20, "tvmonitor")]
        public void VocClassNames_ShouldContainExpectedClasses(int classId, string expectedName)
        {
            ClassNames.VocClassNames.Should().ContainKey(classId);
            ClassNames.VocClassNames[classId].Should().Be(expectedName);
        }

        #endregion

        #region CIFAR-10 Class Names Tests

        [Fact]
        public void Cifar10ClassNames_ShouldHave10Classes()
        {
            ClassNames.Cifar10ClassNames.Should().HaveCount(10);
        }

        [Theory]
        [InlineData(0, "airplane")]
        [InlineData(1, "automobile")]
        [InlineData(2, "bird")]
        [InlineData(3, "cat")]
        [InlineData(4, "deer")]
        [InlineData(5, "dog")]
        [InlineData(9, "truck")]
        public void Cifar10ClassNames_ShouldContainExpectedClasses(int classId, string expectedName)
        {
            ClassNames.Cifar10ClassNames.Should().ContainKey(classId);
            ClassNames.Cifar10ClassNames[classId].Should().Be(expectedName);
        }

        #endregion

        #region CIFAR-100 Class Names Tests

        [Fact]
        public void Cifar100ClassNames_ShouldHave100Classes()
        {
            ClassNames.Cifar100ClassNames.Should().HaveCount(100);
        }

        [Theory]
        [InlineData(0, "apple")]
        [InlineData(1, "aquarium_fish")]
        [InlineData(50, "mountain")]
        [InlineData(99, "woman")]
        public void Cifar100ClassNames_ShouldContainExpectedClasses(int classId, string expectedName)
        {
            ClassNames.Cifar100ClassNames.Should().ContainKey(classId);
            ClassNames.Cifar100ClassNames[classId].Should().Be(expectedName);
        }

        #endregion

        #region ImageNet Class Names Tests

        [Fact]
        public void ImageNetClassNames_ShouldHaveManyClasses()
        {
            // Actual count may vary, but should have a substantial number
            ClassNames.ImageNetClassNames.Count.Should().BeGreaterThan(900);
        }

        [Theory]
        [InlineData(0, "tench, Tinca tinca")]
        [InlineData(1, "goldfish, Carassius auratus")]
        public void ImageNetClassNames_ShouldContainExpectedClasses(int classId, string expectedName)
        {
            ClassNames.ImageNetClassNames.Should().ContainKey(classId);
            ClassNames.ImageNetClassNames[classId].Should().Be(expectedName);
        }

        [Fact]
        public void ImageNetClassNames_First10Entries_ShouldBeAnimals()
        {
            // First 10 classes are fish types
            ClassNames.ImageNetClassNames[0].Should().Contain("tench");
            ClassNames.ImageNetClassNames[1].Should().Contain("goldfish");
            ClassNames.ImageNetClassNames[2].Should().Contain("shark");
        }

        [Fact]
        public void ImageNetClassNames_DogBreeds_ShouldBePresent()
        {
            // Dog breeds start around index 151
            ClassNames.ImageNetClassNames[151].Should().Contain("Chihuahua");
            ClassNames.ImageNetClassNames[152].Should().Contain("Japanese spaniel");
        }

        #endregion
    }
}
