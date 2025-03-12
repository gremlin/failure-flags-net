using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FailureFlags
{
    /// <summary>
    /// Latency calls Thread.sleep for some duration as specified by the "latency" property in an Effect statement for each
    /// experiment in a provided list of experiments. This implementation supports the following statement forms:
    ///
    /// 1. An object form with two properties, "ms" and or "jitter" where each is an integer in milliseconds.
    /// 2. A string containing an integer representing a consistent number of milliseconds to delay.
    /// 3. An integer representing a consistent number of milliseconds to delay.
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
        public void ApplyBehavior(Experiment[] experiments)
        {
            var latencies = experiments
                .Where(experiment => experiment.Effect.ContainsKey("latency"))
                .Select(experiment => experiment.Effect["latency"])
                .Where(latency => latency != null)
                .ToList();

            foreach (var latency in latencies)
            {
                if (latency is string || latency is int)
                {
                    try
                    {
                        int latencyToInject = int.Parse(latency?.ToString() ?? throw new FailureFlagException("Latency value is null"));
                        Timeout(latencyToInject);
                    }
                    catch (FormatException)
                    {
                        throw new FailureFlagException("Invalid value for latency passed");
                    }
                }


                if (latency is Dictionary<string, object> latencyMap)
                {
                    if (latencyMap.TryGetValue("ms", out var ms) && latencyMap.TryGetValue("jitter", out var jitter))
                    {
                        if ((ms is string || ms is int) && (jitter is string || jitter is int))
                        {
                            try
                            {
                                int latencyToInject = int.Parse(ms?.ToString() ?? throw new FailureFlagException("Latency ms value is null"));
                                int jitterMs = int.Parse(jitter?.ToString() ?? throw new FailureFlagException("Latency jitter value is null"));
                                Timeout(latencyToInject + (jitterMs == 0 ? 0 : (int)(new Random().NextDouble() * jitterMs)));
                            }
                            catch (FormatException)
                            {
                                throw new FailureFlagException("Invalid value for latency passed");
                            }
                        }
                    }
                }
            }
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


