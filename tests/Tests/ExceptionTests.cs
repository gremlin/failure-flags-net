using System.Collections.Generic;
using Xunit;
namespace FailureFlags
{
    public class ExceptionTests
    {
        [Fact]
        public void ApplyBehavior_ShouldThrowException_WhenExceptionEffectWithMessageIsPresent()
        {
            // Arrange
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "exception", new Dictionary<string, object> { { "message", "TestException" } } }
                        }
                    }
            };
            var exceptionBehavior = new Exception();

            // Act & Assert
            Assert.Throws<FailureFlagException>(() => exceptionBehavior.ApplyBehavior(experiments));
        }

        [Fact]
        public void ApplyBehavior_ShouldThrowException_WhenExceptionEffectIsPresent()
        {
            // Arrange
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "exception", "TestException" }
                        }
                    }
            };
            var exceptionBehavior = new Exception();

            // Act & Assert
            Assert.Equal("Exception of type 'TestException' was thrown.", Assert.ThrowsAny<System.Exception>(() => exceptionBehavior.ApplyBehavior(experiments)).Message);
        }

        [Fact]
        public void ApplyBehavior_ShouldNotThrowException_WhenExceptionEffectIsNotPresent()
        {
            // Arrange
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "latency", 100 }
                        }
                    }
                };
            var exceptionBehavior = new Exception();

            // Act & Assert
            var exception = Record.Exception(() => exceptionBehavior.ApplyBehavior(experiments));
            Assert.Null(exception);
        }
    }
}

