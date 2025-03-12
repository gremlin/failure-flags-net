using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FailureFlags
{
    /// <summary>
    /// Represents an active experiment defined by a Gremlin user.
    /// </summary>
    /// <example>
    /// Example of an experiment with a delay effect:
    /// <code>
    /// var experiment = new Experiment
    /// {
    ///     Name = "DelayExperiment",
    ///     Guid = "1234",
    ///     Rate = 1.0f,
    ///     Effect = new Dictionary<string, object>
    ///     {
    ///         { "latency", 1000 }
    ///     }
    /// };
    /// </code>
    /// Example of an experiment with an exception effect:
    /// <code>
    /// var experiment = new Experiment
    /// {
    ///     Name = "ExceptionExperiment",
    ///     Guid = "5678",
    ///     Rate = 1.0f,
    ///     Effect = new Dictionary<string, object></string>
    ///     {
    ///         { "exception", "TestException" }
    ///     }
    /// };
    /// </code>
    /// </example>
    public class Experiment
    {
        /// <summary>
        /// Name of the experiment.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// GUID of the experiment.
        /// </summary>
        [JsonPropertyName("guid")]
        public string Guid { get; set; }

        /// <summary>
        /// Rate of the experiment.
        /// </summary>
        [JsonPropertyName("rate")]
        public float Rate { get; set; }

        /// <summary>
        /// Effects of the experiment.
        /// </summary>
        [JsonPropertyName("effect")]
        [JsonConverter(typeof(EffectConverter))]
        public Dictionary<string, object> Effect { get; set; } = new Dictionary<string, object>();
    }
}
