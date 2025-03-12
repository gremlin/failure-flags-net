using System.Collections.Generic;
using Xunit;

namespace FailureFlags
{
    public class DelayedExceptionTests
    {
        [Fact]
        public void ApplyBehavior_ShouldApplyLatencyAndExceptionBehaviors()
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
                            { "latency", 100 },
                            { "exception", new Dictionary<string, object> { { "message", "TestException" } } }
                        }
                    }
                };

            // Act & Assert
            var expectedException = new FailureFlagException("Exception injected by failure flag: TestException");
            var actualException = Assert.Throws<FailureFlagException>(() => new DelayedException().ApplyBehavior(experiments));
            Assert.Equal(expectedException.Message, actualException.Message);
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

            // Act & Assert
            var exception = Record.Exception(() => new DelayedException().ApplyBehavior(experiments));
            Assert.Null(exception);
        }
    }
}

