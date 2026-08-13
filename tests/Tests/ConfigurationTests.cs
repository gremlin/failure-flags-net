using System;
using Xunit;

namespace FailureFlags
{
    /// <summary>
    /// Covers the pure configuration functions. These are deliberately static and side effect free
    /// so the whole precedence table can be asserted without mutating process environment, which
    /// xunit runs test classes in parallel against.
    /// </summary>
    public class ConfigurationTests
    {
        [Theory]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("True", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("1", true)]
        [InlineData(" true ", true)]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData("no", false)]
        [InlineData("0", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("banana", false)]
        [InlineData(null, false)]
        public void ParseEnabled_OnlyAcceptsDocumentedValues(string raw, bool expected)
        {
            Assert.Equal(expected, GremlinFailureFlags.ParseEnabled(raw));
        }

        [Fact]
        public void ResolveEndpoint_DefaultsToLocalhost5032()
        {
            Assert.Equal("http://localhost:5032/experiment", GremlinFailureFlags.ResolveEndpoint(null, null, null, null));
        }

        [Fact]
        public void ResolveEndpoint_PrefersTheExplicitArgumentOverEverything()
        {
            var endpoint = GremlinFailureFlags.ResolveEndpoint(
                "http://explicit:1/experiment",
                "http://from-endpoint-var:2/experiment",
                "from-host-var",
                "3");

            Assert.Equal("http://explicit:1/experiment", endpoint);
        }

        [Fact]
        public void ResolveEndpoint_PrefersTheEndpointVariableOverHostAndPort()
        {
            var endpoint = GremlinFailureFlags.ResolveEndpoint(
                null,
                "http://from-endpoint-var:2/experiment",
                "from-host-var",
                "3");

            Assert.Equal("http://from-endpoint-var:2/experiment", endpoint);
        }

        [Fact]
        public void ResolveEndpoint_AcceptsHostAlone()
        {
            Assert.Equal("http://sidecar:5032/experiment", GremlinFailureFlags.ResolveEndpoint(null, null, "sidecar", null));
        }

        [Fact]
        public void ResolveEndpoint_AcceptsPortAlone()
        {
            Assert.Equal("http://localhost:9999/experiment", GremlinFailureFlags.ResolveEndpoint(null, null, null, "9999"));
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("65536")]
        [InlineData("")]
        public void ResolveEndpoint_FallsBackToTheDefaultPort_WhenPortIsUnusable(string port)
        {
            Assert.Equal("http://localhost:5032/experiment", GremlinFailureFlags.ResolveEndpoint(null, null, null, port));
        }

        [Fact]
        public void ResolveTimeout_DefaultsTo50Milliseconds()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(50), GremlinFailureFlags.ResolveTimeout(null, null));
        }

        [Fact]
        public void ResolveTimeout_PrefersTheExplicitArgument()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(1234), GremlinFailureFlags.ResolveTimeout(1234, "999"));
        }

        [Fact]
        public void ResolveTimeout_ReadsTheEnvironmentVariable()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(999), GremlinFailureFlags.ResolveTimeout(null, "999"));
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("")]
        public void ResolveTimeout_FallsBackToTheDefault_WhenValueIsUnusable(string timeout)
        {
            Assert.Equal(TimeSpan.FromMilliseconds(50), GremlinFailureFlags.ResolveTimeout(null, timeout));
        }
    }
}
