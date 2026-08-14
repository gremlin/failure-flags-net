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
        public void ApplyBehavior_ShouldInjectLatency_WhenDictionaryHasMsButNoJitter()
        {
            // Jitter is optional. This used to inject nothing at all, silently.
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "latency", new Dictionary<string, object> { { "ms", 100 } } }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = -1;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.Equal(100, capturedLatency);
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectLatency_WhenMsIsADouble()
        {
            // EffectConverter maps a JSON number to int only when TryGetInt32 succeeds, so
            // {"ms": 1000.0} arrives here as a double and used to fail the `is int` guard.
            var experiments = new[]
            {
                    new Experiment
                    {
                        Name = "TestExperiment",
                        Guid = "1234",
                        Rate = 1.0f,
                        Effect = new Dictionary<string, object>
                        {
                            { "latency", new Dictionary<string, object> { { "ms", 1000.0 }, { "jitter", 0 } } }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = -1;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.Equal(1000, capturedLatency);
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectLatency_WhenScalarIsADouble()
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
                            { "latency", 250.0 }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };

            int capturedLatency = -1;
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>())
                .Callback<int>(latency => capturedLatency = latency);

            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));
            Assert.Null(exception);
            Assert.Equal(250, capturedLatency);
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectNothing_WhenDictionaryHasNoMs()
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
                            { "latency", new Dictionary<string, object> { { "jitter", 50 } } }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>());

            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));

            Assert.Null(exception);
            mockLatency.Protected().Verify("Timeout", Times.Never(), ItExpr.IsAny<int>());
        }

        [Fact]
        public void ApplyBehavior_ShouldInjectNothing_WhenLatencyIsAnUnsupportedShape()
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
                            { "latency", true }
                        }
                    }
                };
            var mockLatency = new Mock<Latency> { CallBase = true };
            mockLatency.Protected().Setup("Timeout", ItExpr.IsAny<int>());

            var exception = Record.Exception(() => mockLatency.Object.ApplyBehavior(experiments));

            Assert.Null(exception);
            mockLatency.Protected().Verify("Timeout", Times.Never(), ItExpr.IsAny<int>());
        }

        [Theory]
        [InlineData(100, 100)]
        [InlineData(100L, 100)]
        [InlineData(100.0, 100)]
        [InlineData(100.9, 100)]
        [InlineData("100", 100)]
        [InlineData("100.9", 100)]
        public void TryToMilliseconds_ConvertsEveryNumericShape(object value, int expected)
        {
            Assert.True(Latency.TryToMilliseconds(value, out int milliseconds));
            Assert.Equal(expected, milliseconds);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("")]
        [InlineData(true)]
        [InlineData(null)]
        [InlineData(3000000000.0)] // above Int32.MaxValue
        public void TryToMilliseconds_RejectsWhatItCannotConvert(object value)
        {
            Assert.False(Latency.TryToMilliseconds(value, out int milliseconds));
            Assert.Equal(0, milliseconds);
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
