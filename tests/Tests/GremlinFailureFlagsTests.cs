using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FailureFlags
{
    public class GremlinFailureFlagsTests
    {
        private readonly Mock<ILogger<GremlinFailureFlags>> _loggerMock;
        private readonly GremlinFailureFlags _gremlinFailureFlags;

        public GremlinFailureFlagsTests()
        {
            _loggerMock = new Mock<ILogger<GremlinFailureFlags>>();
            _gremlinFailureFlags = new GremlinFailureFlags();
        }

        [Fact]
        public void Constructor_ShouldInitializeWithLogger()
        {
            // Arrange & Act
            var gremlinFailureFlags = new GremlinFailureFlags(null, _loggerMock.Object);

            // Assert
            Assert.NotNull(gremlinFailureFlags);
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultBehavior()
        {
            // Arrange & Act
            var gremlinFailureFlags = new GremlinFailureFlags(new DelayedException());

            // Assert
            Assert.NotNull(gremlinFailureFlags);
        }

        [Fact]
        public void Invoke_ShouldReturnEmptyArray_WhenFailureFlagsDisabled()
        {
            // Arrange
            var flag = new FailureFlag { Name = "TestFlag", Debug = true };

            // Act
            var result = _gremlinFailureFlags.Invoke(flag);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Invoke_ShouldReturnEmptyArray_WhenFlagIsNull()
        {
            // Act
            var result = _gremlinFailureFlags.Invoke(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Fetch_ShouldReturnEmptyArray_WhenFailureFlagsDisabled()
        {
            // Arrange
            var flag = new FailureFlag { Name = "TestFlag", Debug = true };

            // Act
            var result = _gremlinFailureFlags.Fetch(flag);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Fetch_ShouldReturnEmptyArray_WhenFlagIsNull()
        {
            // Act
            var result = _gremlinFailureFlags.Fetch(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Fetch_ShouldReturnEmptyArray_WhenFlagNameIsEmpty()
        {
            // Arrange
            var flag = new FailureFlag { Name = "", Debug = true };

            // Act
            var result = _gremlinFailureFlags.Fetch(flag);

            // Assert
            Assert.Empty(result);
        }
    }
}
