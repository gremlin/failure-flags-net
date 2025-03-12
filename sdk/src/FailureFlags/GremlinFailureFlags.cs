using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FailureFlags
{
    /// <summary>
    /// Full implementation of FailureFlags that integrates with Gremlin sidecars and API.
    /// </summary>
    public class GremlinFailureFlags : IFailureFlags
    {
        private static readonly string VERSION = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        public static readonly JsonSerializerOptions JSON_OPTIONS = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new EffectConverter() }
        };

        /// <summary>
        /// Name of the environment variable to control whether to enable the SDK.
        /// </summary>
        private static readonly string FAILURE_FLAGS_ENABLED = "FAILURE_FLAGS_ENABLED";

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
        /// Constructs a new FailureFlags instance.
        /// <param name="defaultBehavior">The default behavior to apply to experiments if no behavior is specified.</param>
        /// <param name="logger">An instance of ILogger used for logging messages.</param>
        /// <returns>An instance of GremlinFailureFlags</returns>
        public GremlinFailureFlags(IBehavior? defaultBehavior = null, ILogger<GremlinFailureFlags>? logger = null, bool enabled = false)
        {
            _defaultBehavior = defaultBehavior ?? new DelayedException();
            _logger = logger ?? NullLogger<GremlinFailureFlags>.Instance;
            _enabled = enabled;
        }

        /// <summary>
        /// Returns the default behavior for this Failure Flags instance.
        /// </summary>
        /// <returns>Default behaviour</returns>
        public IBehavior GetDefaultBehavior()
        {
            return _defaultBehavior;
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag)
        {
            return Invoke(flag, null);
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag, IBehavior? behavior)
        {
            if (!Environment.GetEnvironmentVariables().Contains(FAILURE_FLAGS_ENABLED) && !this._enabled)
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
            double dice = new Random().NextDouble();
            List<Experiment> filteredExperiments = new(activeExperiments.Length);
            foreach (var e in activeExperiments)
            {
                if (e.Rate > 0 && e.Rate <= 1 && dice < e.Rate)
                {
                    filteredExperiments.Add(e);
                }
            }
            Experiment[] experiments = filteredExperiments.ToArray();

            if (experiments.Length <= 0)
            {
                return Array.Empty<Experiment>();
            }

            if (behavior == null)
            {
                _defaultBehavior.ApplyBehavior(filteredExperiments.ToArray());
            }
            else
            {
                behavior.ApplyBehavior(filteredExperiments.ToArray());
            }
            return activeExperiments;
        }

        /// <inheritdoc />
        public Experiment[] Fetch(FailureFlag flag)
        {
            if (!Environment.GetEnvironmentVariables().Contains(FAILURE_FLAGS_ENABLED) && !this._enabled)
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

            Dictionary<string, string> augmentedLabels = new(flag.Labels ?? new Dictionary<string, string>())
            {
                { "failure-flags-sdk-version", "failure-flags-net-v" + VERSION }
            };

            if (flag.Debug)
            {
                _logger.LogInformation("fetching experiments for: name: {Name}, labels: {Labels}", flag.Name, augmentedLabels);
            }
            flag.Labels = augmentedLabels;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var content = new StringContent(JsonSerializer.Serialize(flag, JSON_OPTIONS), Encoding.UTF8, "application/json");

                HttpResponseMessage response = client.PostAsync("http://localhost:5032/experiment", content).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return Array.Empty<Experiment>();
                }
                else if (response.IsSuccessStatusCode)
                {
                    Experiment[]? experiments = response.Content.ReadFromJsonAsync<Experiment[]>(JSON_OPTIONS).Result;
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
