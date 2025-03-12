using Moq;
using Moq.Protected;
using System.Collections.Generic;
using Xunit;

namespace FailureFlags
{
    public class LatencyTests
    {
        [Fact]
        public void ApplyBehavior_ShouldInjectLatency_WhenLatencyIsString()
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
                            { "latency", "100" }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = 0;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            // Act & Assert
            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.Equal(100, capturedLatency);
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectLatency_WhenLatencyIsInt()
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
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = 0;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            // Act & Assert
            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.Equal(100, capturedLatency);
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectLatencyWithJitter_WhenLatencyIsDictionary()
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
                            { "latency", new Dictionary<string, object> { { "ms", 100 }, { "jitter", 50 } } }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = 0;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            // Act & Assert
            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.True(capturedLatency >= 100 && capturedLatency <= 150);
        }

        [Fact]
        public void ApplyBehavior_ShouldThrowFailureFlagException_WhenLatencyIsInvalidString()
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
                            { "latency", "invalid" }
                        }
                    }
                };
            var latencyBehavior = new Latency();

            // Act & Assert
            Assert.Throws<FailureFlagException>(() => latencyBehavior.ApplyBehavior(experiments));
        }

        [Fact]
        public void ApplyBehavior_ShouldThrowFailureFlagException_WhenLatencyIsNegative()
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
                            { "latency", -100 }
                        }
                    }
                };
            var latencyBehavior = new Latency();

            // Act & Assert
            Assert.Throws<FailureFlagException>(() => latencyBehavior.ApplyBehavior(experiments));
        }
    }
}
