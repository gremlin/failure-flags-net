using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace FailureFlags
{
    public class GremlinFailureFlagsIntegTests : IDisposable
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new EffectConverter() }
        };

        private readonly Mock<ILogger<GremlinFailureFlags>> _loggerMock;
        private readonly GremlinFailureFlags _gremlinFailureFlags;
        private readonly WireMockServer _wireMockServer;

        public GremlinFailureFlagsIntegTests()
        {
            _loggerMock = new Mock<ILogger<GremlinFailureFlags>>();
            _gremlinFailureFlags = new GremlinFailureFlags(null, _loggerMock.Object, true);
            _wireMockServer = WireMockServer.Start(new WireMockServerSettings
            {
                Port = 5032
            });
        }

        public void Dispose()
        {
            _wireMockServer.Stop();
        }

        [Fact]
        public void Invoke_DoesNothing_WhenNoExperimentReturned()
        {
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json"));

            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };

            Experiment[] experiments = _gremlinFailureFlags.Invoke(failureFlag);
            Assert.Empty(experiments);
        }

        [Fact]
        public void Invoke_DoesNothing_WhenNoExperimentReturnedWhenBehaviorPassed()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var experiment = new Experiment { Effect = effect, Rate = 0f };

            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));

            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };

            Experiment[] experiments = _gremlinFailureFlags.Invoke(failureFlag);
            Assert.Empty(experiments);
        }

        [Fact]
        public void Invoke_IntroducesLatency_WhenExperimentReturnedAndLatencyInEffect()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _gremlinFailureFlags.Invoke(failureFlag);
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 700);
        }

        [Fact]
        public void Invoke_BehaviorCalled_WhenExperiment100PercentProbable()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var behaviorMock = new Mock<IBehavior>();
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };
            _gremlinFailureFlags.Invoke(failureFlag, behaviorMock.Object);
            behaviorMock.Verify(l => l.ApplyBehavior(It.IsAny<Experiment[]>()), Times.Once);
        }

        [Fact]
        public void Invoke_BehaviorNotCalled_WhenExperimentZeroPercentProbable()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var experiment = new Experiment { Effect = effect, Rate = 0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var behaviorMock = new Mock<IBehavior>();
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };
            _gremlinFailureFlags.Invoke(failureFlag, behaviorMock.Object);
            behaviorMock.Verify(l => l.ApplyBehavior(It.IsAny<Experiment[]>()), Times.Never);
        }

        [Fact]
        public void Invoke_BehaviorNotCalledWhenDisabled()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var behaviorMock = new Mock<IBehavior>();
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };
            var gremlinFailureFlags = new GremlinFailureFlags(null, _loggerMock.Object, false);
            gremlinFailureFlags.Invoke(failureFlag, behaviorMock.Object);
            behaviorMock.Verify(l => l.ApplyBehavior(It.IsAny<Experiment[]>()), Times.Never);
        }

        [Fact]
        public void Invoke_IntroducesLatency_WhenExperimentReturnedAndLatencyInEffectAndLatencyBehaviorPassed()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _gremlinFailureFlags.Invoke(failureFlag, new Latency());
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 700);
        }

        [Fact]
        public void Invoke_IntroducesTwoLatency_WhenTwoExperimentsReturnedAndLatencyInEffectAndLatencyBehaviorPassed()
        {
            var effect = new Dictionary<string, object> { { "latency", 500 } };
            var exp1 = new Experiment { Effect = effect, Rate = 1.0f };
            var exp2 = new Experiment { Effect = effect, Rate = 1.0f };
            var experiments = new List<Experiment> { exp1, exp2 };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(experiments, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _gremlinFailureFlags.Invoke(failureFlag, new Latency());
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds > 900);
        }

        [Fact]
        public void Invoke_IntroducesLatency_WhenExperimentReturnedAndLatencyInEffectInObject()
        {
            var latencyEffect = new Dictionary<string, object> { { "ms", 500 }, { "jitter", 100 } };
            var effect = new Dictionary<string, object> { { "latency", latencyEffect } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _gremlinFailureFlags.Invoke(failureFlag);
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds > 500 && stopwatch.ElapsedMilliseconds < 800);
        }

        [Fact]
        public void Invoke_IntroducesLatency_WhenExperimentReturnedAndLatencyAndExceptionInEffectInObject()
        {
            var latencyEffect = new Dictionary<string, object> { { "ms", 500 }, { "jitter", 100 } };
            var effect = new Dictionary<string, object> { { "latency", latencyEffect }, { "exception", new Dictionary<string, object> { { "message", "TestException" } } } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var exception = Assert.Throws<FailureFlagException>(() => _gremlinFailureFlags.Invoke(failureFlag));
            stopwatch.Stop();
            var actualMessage = exception.Message;
            var expectedMessage = "Exception injected by failure flag: TestException";
            Assert.Equal(expectedMessage, actualMessage);
            Assert.True(stopwatch.ElapsedMilliseconds > 500 && stopwatch.ElapsedMilliseconds < 800);
        }

        [Fact]
        public void Invoke_IntroducesLatency_WhenExperimentReturnedAndLatencyAndExceptionInEffectInObjectAndBehaviorPassed()
        {
            var latencyEffect = new Dictionary<string, object> { { "ms", 500 }, { "jitter", 100 } };
            var effect = new Dictionary<string, object> { { "latency", latencyEffect }, { "exception", new Dictionary<string, object> { { "message", "TestException" } } } };
            var experiment = new Experiment { Effect = effect, Rate = 1.0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new Experiment[] { experiment }, jsonOptions)));
            var labels = new Dictionary<string, string> { { "key", "value" } };
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = labels,
                Debug = true
            };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var exception = Assert.Throws<FailureFlagException>(() => _gremlinFailureFlags.Invoke(failureFlag, new DelayedException()));
            stopwatch.Stop();
            var actualMessage = exception.Message;
            var expectedMessage = "Exception injected by failure flag: TestException";
            Assert.Equal(expectedMessage, actualMessage);
            Assert.True(stopwatch.ElapsedMilliseconds > 500 && stopwatch.ElapsedMilliseconds < 800);
        }
    }
}