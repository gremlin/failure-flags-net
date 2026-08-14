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
            var exceptionBehavior = new ExceptionBehavior();

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
            var exceptionBehavior = new ExceptionBehavior();

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
            var exceptionBehavior = new ExceptionBehavior();

            // Act & Assert
            var exception = Record.Exception(() => exceptionBehavior.ApplyBehavior(experiments));
            Assert.Null(exception);
        }

        [Fact]
        public void ApplyBehavior_ShouldNotThrowException_WhenExceptionEffectHasNoMessage()
        {
            // Malformed effect. It injects nothing, matching the Go SDK's `len(message) > 0` guard,
            // but it now logs rather than vanishing.
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "exception", new Dictionary<string, object> { { "class", "TestException" } } }
                        }
                    }
            };
            var exceptionBehavior = new ExceptionBehavior();

            // Act & Assert
            var exception = Record.Exception(() => exceptionBehavior.ApplyBehavior(experiments));
            Assert.Null(exception);
        }

        [Fact]
        public void ObsoleteExceptionShim_StillBehavesLikeExceptionBehavior()
        {
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
#pragma warning disable CS0618 // exercising the compatibility shim on purpose
            IBehavior exceptionBehavior = new Exception();
#pragma warning restore CS0618

            Assert.Throws<FailureFlagException>(() => exceptionBehavior.ApplyBehavior(experiments));
        }
    }
}

