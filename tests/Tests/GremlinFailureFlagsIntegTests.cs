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
            // The 50ms production default is deliberately too short for WireMock startup plus a
            // cold loopback connection. A timeout here is swallowed into an empty result, so it
            // would show up as "the experiment didn't fire" rather than as a failure. The timeout
            // path has its own test below.
            _gremlinFailureFlags = new GremlinFailureFlags(null, _loggerMock.Object, true, timeoutMs: 5000);
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

        [Fact]
        public void Fetch_GivesUpQuickly_WhenTheSidecarIsSlow()
        {
            // Without an explicit timeout HttpClient's 100 second default governs, and the measured
            // cost of the fail-safe path was 4.3 seconds even when the OS refused the connection
            // outright. Assert on elapsed time, not on "it returned empty" -- it returns empty
            // whether it worked perfectly or failed completely.
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithDelay(TimeSpan.FromSeconds(2))
                        .WithBody(JsonSerializer.Serialize(
                            new[] { new Experiment { Effect = new Dictionary<string, object> { { "latency", 500 } }, Rate = 1.0f } },
                            jsonOptions)));

            var impatient = new GremlinFailureFlags(null, _loggerMock.Object, true, timeoutMs: 50);
            var failureFlag = new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            };

            var stopwatch = Stopwatch.StartNew();
            Experiment[] experiments = impatient.Fetch(failureFlag);
            stopwatch.Stop();

            Assert.Empty(experiments);

            // A band, not an upper bound. Empty-and-fast is also what "never got off the ground"
            // looks like, so the lower bound is what proves the request was actually issued and
            // then abandoned at the deadline rather than failing instantly.
            Assert.True(
                stopwatch.ElapsedMilliseconds >= 40 && stopwatch.ElapsedMilliseconds < 1000,
                $"expected the fetch to be abandoned at its ~50ms deadline, took {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void Fetch_UsesTheConfiguredEndpoint_WhenOneIsPassed()
        {
            using var elsewhere = WireMockServer.Start();
            elsewhere
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(
                            new[] { new Experiment { Name = "from-elsewhere", Effect = new Dictionary<string, object>(), Rate = 1.0f } },
                            jsonOptions)));

            var redirected = new GremlinFailureFlags(
                null,
                _loggerMock.Object,
                true,
                endpoint: $"{elsewhere.Urls[0]}/experiment",
                timeoutMs: 5000);

            Experiment[] experiments = redirected.Fetch(new FailureFlag
            {
                Name = "test-1",
                Labels = new Dictionary<string, string>(),
                Debug = true
            });

            Assert.NotEmpty(elsewhere.LogEntries);
            Assert.Equal("from-elsewhere", Assert.Single(experiments).Name);
        }

        [Fact]
        public void Invoke_ReturnsOnlyTheExperimentsThatFired()
        {
            var certain = new Experiment { Name = "certain", Effect = new Dictionary<string, object>(), Rate = 1.0f };
            var impossible = new Experiment { Name = "impossible", Effect = new Dictionary<string, object>(), Rate = 0f };
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(JsonSerializer.Serialize(new[] { certain, impossible }, jsonOptions)));

            Experiment[] experiments = _gremlinFailureFlags.Invoke(
                new FailureFlag { Name = "test-1", Labels = new Dictionary<string, string>() },
                new Mock<IBehavior>().Object);

            Assert.Equal("certain", Assert.Single(experiments).Name);
        }

        [Fact]
        public void Fetch_DoesNotMutateTheFlagItWasGiven()
        {
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(204));

            // The kind of thing a caller would hold as a reusable template.
            var labels = new Dictionary<string, string> { { "method", "GET" } };
            var failureFlag = new FailureFlag { Name = "test-1", Labels = labels };

            _gremlinFailureFlags.Fetch(failureFlag);

            Assert.Same(labels, failureFlag.Labels);
            Assert.DoesNotContain("failure-flags-sdk-version", failureFlag.Labels.Keys);
        }

        [Fact]
        public void Fetch_LabelsTheRequestWithAThreePartSdkVersion()
        {
            _wireMockServer
                .Given(
                    Request.Create()
                        .WithPath("/experiment")
                        .UsingPost())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(204));

            _gremlinFailureFlags.Fetch(new FailureFlag { Name = "test-1", Labels = new Dictionary<string, string>() });

            var body = Assert.Single(_wireMockServer.LogEntries).RequestMessage.Body;

            // Asserting the shape, not the number, so this survives every version bump. Three parts
            // rules out AssemblyVersion, which is always four and cannot express the VERSION file's
            // "1.1.0"; the anchored quote rules out a "+<sha>" source revision suffix.
            Assert.Matches(@"""failure-flags-sdk-version"":""failure-flags-net-v\d+\.\d+\.\d+""", body);
        }
    }
}