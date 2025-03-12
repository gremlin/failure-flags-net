using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FailureFlags
{
    /// <summary>
    /// Represents a named point in code and specific invocation metadata. A Gremlin user can design
    /// experiments which target specific Failure Flags.
    /// </summary>
    public class FailureFlag
    {
        /// <summary>
        /// Name of the failure flag.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Labels of the failure flag for targeting.
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Whether the FailureFlag is configured for debugging.
        public bool Debug { get; set; } = false;
    }
}
