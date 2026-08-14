using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace FailureFlags
{
    /// <summary>
    /// Latency calls Thread.sleep for some duration as specified by the "latency" property in an Effect statement for each
    /// experiment in a provided list of experiments. This implementation supports the following statement forms:
    ///
    /// 1. An object form with a required "ms" property and an optional "jitter" property, each in milliseconds.
    /// 2. A string containing a number representing a consistent number of milliseconds to delay.
    /// 3. A number representing a consistent number of milliseconds to delay.
    ///
    /// Anything else is logged and skipped. An effect that cannot be applied must say so somewhere;
    /// an operator watching a green experiment inject nothing has no other way to find out why.
    /// </summary>
    /// <example>
    /// {
    ///  ...
    ///   "latency": {
    ///       "ms": 1000,
    ///       "jitter": 100
    ///   }
    ///  ...
    /// }
    ///
    /// or
    ///
    /// {
    ///  ...
    ///   "latency": 1000
    ///  ...
    /// }
    ///
    /// or
    ///
    /// {
    ///  ...
    ///   "latency": "1000"
    ///  ...
    /// }
    /// </example>
    public class Latency : IBehavior
    {
        private readonly ILogger _logger;

        public Latency() : this(null)
        {
        }

        public Latency(ILogger? logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public void ApplyBehavior(Experiment[] experiments)
        {
            var latencies = experiments
                .Where(experiment => experiment.Effect.ContainsKey("latency"))
                .Select(experiment => experiment.Effect["latency"])
                .Where(latency => latency != null)
                .ToList();

            foreach (var latency in latencies)
            {
                if (latency is Dictionary<string, object> latencyMap)
                {
                    ApplyObjectForm(latencyMap);
                }
                else if (TryScalarMilliseconds(latency, "latency", out int latencyToInject))
                {
                    Timeout(latencyToInject);
                }
            }
        }

        private void ApplyObjectForm(Dictionary<string, object> latencyMap)
        {
            if (!latencyMap.TryGetValue("ms", out var ms))
            {
                _logger.LogWarning(
                    "latency effect has no \"ms\" property, injecting nothing; its properties were: {Properties}",
                    string.Join(", ", latencyMap.Keys));
                return;
            }
            if (!TryScalarMilliseconds(ms, "latency ms", out int latencyToInject))
            {
                return;
            }

            // Jitter is optional. Requiring it means {"latency": {"ms": 500}} injects nothing at all.
            int jitterMs = 0;
            if (latencyMap.TryGetValue("jitter", out var jitter))
            {
                TryScalarMilliseconds(jitter, "latency jitter", out jitterMs);
            }

            Timeout(latencyToInject + (jitterMs <= 0 ? 0 : (int)(Rng.NextDouble() * jitterMs)));
        }

        /// <summary>
        /// Converts a scalar latency value to milliseconds.
        ///
        /// Returns false, having logged, for shapes the SDK does not understand. Throws for a value
        /// that is the right kind of thing but not a number ("invalid", "1e999"), because that is
        /// operator error in an experiment definition and worth surfacing loudly.
        /// </summary>
        private bool TryScalarMilliseconds(object? value, string what, out int milliseconds)
        {
            if (!IsScalar(value))
            {
                milliseconds = 0;
                _logger.LogWarning(
                    "{What} is a {Type}, which is neither a number nor a string; injecting nothing",
                    what,
                    value?.GetType().Name ?? "null");
                return false;
            }
            if (!TryToMilliseconds(value, out milliseconds))
            {
                throw new FailureFlagException("Invalid value for latency passed");
            }
            return true;
        }

        private static bool IsScalar(object? value)
        {
            return value is int || value is long || value is double || value is float || value is string;
        }

        /// <summary>
        /// Pure conversion to whole milliseconds.
        ///
        /// Accepts more than <see cref="int"/> deliberately: EffectConverter maps a JSON number to
        /// int only when TryGetInt32 succeeds and to double otherwise, so {"ms": 1000.0} and
        /// anything above Int32.MaxValue arrive here as a double.
        /// </summary>
        internal static bool TryToMilliseconds(object? value, out int milliseconds)
        {
            switch (value)
            {
                case int i:
                    milliseconds = i;
                    return true;
                case long l:
                    return TryFromDouble(l, out milliseconds);
                case double d:
                    return TryFromDouble(d, out milliseconds);
                case float f:
                    return TryFromDouble(f, out milliseconds);
                case string s:
                    if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        milliseconds = parsed;
                        return true;
                    }
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
                    {
                        return TryFromDouble(parsedDouble, out milliseconds);
                    }
                    milliseconds = 0;
                    return false;
                default:
                    milliseconds = 0;
                    return false;
            }
        }

        private static bool TryFromDouble(double value, out int milliseconds)
        {
            if (double.IsNaN(value) || value < int.MinValue || value > int.MaxValue)
            {
                milliseconds = 0;
                return false;
            }
            milliseconds = (int)value;
            return true;
        }

        protected virtual void Timeout(int ms)
        {
            try
            {
                Thread.Sleep(ms);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new FailureFlagException($"Invalid value for latency passed {e.Message}");
            }
            catch (ThreadInterruptedException)
            {
                Thread.CurrentThread.Interrupt();
            }
        }
    }
}
