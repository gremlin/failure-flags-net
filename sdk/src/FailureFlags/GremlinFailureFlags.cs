using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace FailureFlags
{
    /// <summary>
    /// Full implementation of FailureFlags that integrates with Gremlin sidecars and API.
    /// </summary>
    public class GremlinFailureFlags : IFailureFlags
    {
        private static readonly string VERSION = ResolveVersion();
        public static readonly JsonSerializerOptions JSON_OPTIONS = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new EffectConverter() }
        };

        /// <summary>
        /// Name of the environment variable to control whether to enable the SDK.
        /// </summary>
        private const string FAILURE_FLAGS_ENABLED = "FAILURE_FLAGS_ENABLED";

        /// <summary>
        /// Name of the environment variable holding the full sidecar URL. Takes precedence over
        /// <see cref="GREMLIN_SIDECAR_HOST"/> and <see cref="GREMLIN_SIDECAR_PORT"/>.
        /// </summary>
        private const string FAILURE_FLAGS_ENDPOINT = "FAILURE_FLAGS_ENDPOINT";

        /// <summary>
        /// Name of the environment variable holding the fetch timeout, in milliseconds.
        /// </summary>
        private const string FAILURE_FLAGS_TIMEOUT_MS = "FAILURE_FLAGS_TIMEOUT_MS";

        /// <summary>
        /// Name of the environment variable holding the sidecar host, matching the sidecar's own
        /// configuration namespace.
        /// </summary>
        private const string GREMLIN_SIDECAR_HOST = "GREMLIN_SIDECAR_HOST";

        /// <summary>
        /// Name of the environment variable holding the sidecar port.
        /// </summary>
        private const string GREMLIN_SIDECAR_PORT = "GREMLIN_SIDECAR_PORT";

        internal const string DEFAULT_HOST = "localhost";
        internal const int DEFAULT_PORT = 5032;

        /// <summary>
        /// The sidecar is a co-process on loopback. Anything slower than this is not going to
        /// answer, and a Failure Flag must never be the reason a request is slow.
        /// </summary>
        internal const int DEFAULT_TIMEOUT_MS = 50;

        /// <summary>
        /// One client for the process. Constructing an HttpClient per call also constructs a
        /// connection pool per call and leaves the socket in TIME_WAIT on dispose, which exhausts
        /// ephemeral ports on any hot path.
        ///
        /// Its own timeout is infinite on purpose: timeouts are per request, via the
        /// CancellationTokenSource in <see cref="Fetch"/>, because the deadline is per instance and
        /// this client is shared by all of them.
        /// </summary>
        private static readonly HttpClient _http = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        /// <summary>
        /// Default behavior to apply to experiments if no behavior is specified.
        /// </summary>
        private readonly IBehavior _defaultBehavior;

        /// <summary>
        /// Logger instance used for logging messages.
        /// </summary>
        private readonly ILogger<GremlinFailureFlags> _logger;

        /// <summary>
        /// Overrides the environment variable FAILURE_FLAGS_ENABLED to enable or disable the SDK for testing purposes.
        /// If set to true, regardless of whether the FAILURE_FLAGS_ENABLED environment variable is set, the SDK will be enabled.
        /// </summary>
        private readonly bool _enabled;

        /// <summary>
        /// Resolved sidecar URL. See <see cref="ResolveEndpoint"/> for the precedence rules.
        /// </summary>
        private readonly string _endpoint;

        /// <summary>
        /// Resolved per-request deadline. See <see cref="ResolveTimeout"/>.
        /// </summary>
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Constructs a new FailureFlags instance.
        /// </summary>
        /// <param name="defaultBehavior">The default behavior to apply to experiments if no behavior is specified.</param>
        /// <param name="logger">An instance of ILogger used for logging messages.</param>
        /// <param name="enabled">Enables the SDK regardless of the FAILURE_FLAGS_ENABLED environment variable.</param>
        /// <param name="endpoint">Sidecar URL. Overrides every environment variable when set.</param>
        /// <param name="timeoutMs">Per-request deadline in milliseconds. Overrides FAILURE_FLAGS_TIMEOUT_MS when set.</param>
        /// <returns>An instance of GremlinFailureFlags</returns>
        public GremlinFailureFlags(
            IBehavior? defaultBehavior = null,
            ILogger<GremlinFailureFlags>? logger = null,
            bool enabled = false,
            string? endpoint = null,
            int? timeoutMs = null)
        {
            _logger = logger ?? NullLogger<GremlinFailureFlags>.Instance;
            _defaultBehavior = defaultBehavior ?? new DelayedException(_logger);
            _enabled = enabled;
            _endpoint = ResolveEndpoint(
                endpoint,
                Environment.GetEnvironmentVariable(FAILURE_FLAGS_ENDPOINT),
                Environment.GetEnvironmentVariable(GREMLIN_SIDECAR_HOST),
                Environment.GetEnvironmentVariable(GREMLIN_SIDECAR_PORT));
            _timeout = ResolveTimeout(timeoutMs, Environment.GetEnvironmentVariable(FAILURE_FLAGS_TIMEOUT_MS));
            _logger.LogDebug("failure flags will fetch from {Endpoint} with a {TimeoutMs}ms deadline", _endpoint, _timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Returns the default behavior for this Failure Flags instance.
        /// </summary>
        /// <returns>Default behaviour</returns>
        public IBehavior GetDefaultBehavior()
        {
            return _defaultBehavior;
        }

        /// <summary>
        /// Parses the value of FAILURE_FLAGS_ENABLED. Absent, empty, or anything other than the
        /// documented "true", "yes", or "1" means disabled.
        ///
        /// Testing for the presence of the key rather than its value would mean
        /// FAILURE_FLAGS_ENABLED=false enables fault injection, which is the wrong direction for a
        /// kill switch and bites every config system that renders booleans rather than omitting
        /// keys.
        /// </summary>
        internal static bool ParseEnabled(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            string value = raw!.Trim();
            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves the sidecar URL, most specific source first: the constructor argument, then
        /// FAILURE_FLAGS_ENDPOINT, then GREMLIN_SIDECAR_HOST and GREMLIN_SIDECAR_PORT (either may
        /// be set alone), then http://localhost:5032/experiment.
        ///
        /// A port that is not a number in 1..65535 falls back to the default rather than throwing.
        /// This is a fail-safe library; bad configuration must not take the application with it.
        /// </summary>
        internal static string ResolveEndpoint(string? explicitEndpoint, string? endpointVariable, string? hostVariable, string? portVariable)
        {
            if (!string.IsNullOrWhiteSpace(explicitEndpoint))
            {
                return explicitEndpoint!.Trim();
            }
            if (!string.IsNullOrWhiteSpace(endpointVariable))
            {
                return endpointVariable!.Trim();
            }

            string host = string.IsNullOrWhiteSpace(hostVariable) ? DEFAULT_HOST : hostVariable!.Trim();
            int port = DEFAULT_PORT;
            if (!string.IsNullOrWhiteSpace(portVariable)
                && int.TryParse(portVariable!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort)
                && parsedPort > 0
                && parsedPort <= 65535)
            {
                port = parsedPort;
            }
            return $"http://{host}:{port}/experiment";
        }

        /// <summary>
        /// Resolves the per-request deadline: the constructor argument, then FAILURE_FLAGS_TIMEOUT_MS,
        /// then <see cref="DEFAULT_TIMEOUT_MS"/>. Non-positive and unparsable values fall back.
        /// </summary>
        internal static TimeSpan ResolveTimeout(int? explicitTimeoutMs, string? timeoutVariable)
        {
            if (explicitTimeoutMs.HasValue && explicitTimeoutMs.Value > 0)
            {
                return TimeSpan.FromMilliseconds(explicitTimeoutMs.Value);
            }
            if (!string.IsNullOrWhiteSpace(timeoutVariable)
                && int.TryParse(timeoutVariable!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTimeout)
                && parsedTimeout > 0)
            {
                return TimeSpan.FromMilliseconds(parsedTimeout);
            }
            return TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        }

        /// <summary>
        /// Reads the version the SDK reports about itself in labels. AssemblyVersion is always four
        /// parts, so it cannot represent the three-part version in the VERSION file; the
        /// informational version can.
        /// </summary>
        private static string ResolveVersion()
        {
            Assembly assembly = typeof(GremlinFailureFlags).Assembly;
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
            {
                return assembly.GetName().Version?.ToString() ?? "unknown";
            }
            // Strip any "+<source revision>" build metadata.
            int metadata = informational!.IndexOf('+');
            return metadata < 0 ? informational : informational.Substring(0, metadata);
        }

        private bool IsEnabled()
        {
            return _enabled || ParseEnabled(Environment.GetEnvironmentVariable(FAILURE_FLAGS_ENABLED));
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag)
        {
            return Invoke(flag, null);
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag, IBehavior? behavior)
        {
            if (!IsEnabled())
            {
                return Array.Empty<Experiment>();
            }
            if (flag == null)
            {
                return Array.Empty<Experiment>();
            }
            if (flag.Debug)
            {
                _logger.LogInformation("ifExperimentActive: name: {Name}, labels: {Labels}", flag.Name, flag.Labels);
            }

            Experiment[] activeExperiments;
            try
            {
                activeExperiments = Fetch(flag);
            }
            catch (System.Exception e)
            {
                if (flag.Debug)
                {
                    _logger.LogInformation($"unable to fetch experiments {e.Message}");
                }
                return Array.Empty<Experiment>();
            }

            if (activeExperiments == null)
            {
                if (flag.Debug)
                {
                    _logger.LogInformation("no experiment for name: {Name}, labels: {Labels}", flag.Name, flag.Labels);
                }
                return Array.Empty<Experiment>();
            }

            if (flag.Debug)
            {
                _logger.LogInformation("{Count} fetched experiments", activeExperiments.Length);
            }

            // One roll per experiment. A single roll compared against every rate makes experiments
            // that are supposed to be statistically independent perfectly correlated.
            List<Experiment> filteredExperiments = new(activeExperiments.Length);
            foreach (var e in activeExperiments)
            {
                if (e.Rate > 0 && e.Rate <= 1 && Rng.NextDouble() < e.Rate)
                {
                    filteredExperiments.Add(e);
                }
            }

            if (filteredExperiments.Count <= 0)
            {
                return Array.Empty<Experiment>();
            }

            Experiment[] experiments = filteredExperiments.ToArray();
            (behavior ?? _defaultBehavior).ApplyBehavior(experiments);
            return experiments;
        }

        /// <inheritdoc />
        public Experiment[] Fetch(FailureFlag flag)
        {
            if (!IsEnabled())
            {
                return Array.Empty<Experiment>();
            }
            if (flag == null)
            {
                return Array.Empty<Experiment>();
            }
            if (string.IsNullOrEmpty(flag.Name))
            {
                _logger.LogInformation("Invalid failure flag name {Name}", flag.Name);
                return Array.Empty<Experiment>();
            }

            // Send a copy. Callers reasonably hold a FailureFlag as a reusable template, and a
            // method called Fetch has no business rewriting its argument.
            FailureFlag payload = new()
            {
                Name = flag.Name,
                Debug = flag.Debug,
                Labels = new Dictionary<string, string>(flag.Labels ?? new Dictionary<string, string>())
                {
                    { "failure-flags-sdk-version", "failure-flags-net-v" + VERSION }
                }
            };

            if (flag.Debug)
            {
                _logger.LogInformation("fetching experiments for: name: {Name}, labels: {Labels}", payload.Name, payload.Labels);
            }

            try
            {
                using var cancellation = new CancellationTokenSource(_timeout);
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload, JSON_OPTIONS), Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using HttpResponseMessage response = _http.SendAsync(request, cancellation.Token).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return Array.Empty<Experiment>();
                }
                else if (response.IsSuccessStatusCode)
                {
                    Experiment[]? experiments = response.Content.ReadFromJsonAsync<Experiment[]>(JSON_OPTIONS, cancellation.Token).Result;
                    return experiments ?? Array.Empty<Experiment>();
                }
            }
            catch (JsonException e)
            {
                _logger.LogError($"Unable to serialize or deserialize: {e.Message}");
            }
            catch (IOException e)
            {
                _logger.LogError($"IOException during HTTP call to Gremlin co - process: {e.Message}");
            }
            catch (System.Exception e)
            {
                _logger.LogError($"Something went wrong when sending request: {e.Message}");
            }
            return Array.Empty<Experiment>();
        }
    }
}
