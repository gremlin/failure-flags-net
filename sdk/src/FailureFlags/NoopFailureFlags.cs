using System;

namespace FailureFlags
{
    /// <summary>
    /// Stub implementation of FailureFlags.
    /// </summary>
    public class NoopFailureFlags : IFailureFlags
    {
        /// <inheritdoc />
        public Experiment[] Fetch(FailureFlag flag)
        {
            return Array.Empty<Experiment>();
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag)
        {
            return Array.Empty<Experiment>();
        }

        /// <inheritdoc />
        public Experiment[] Invoke(FailureFlag flag, IBehavior behavior)
        {
            return Array.Empty<Experiment>();
        }
    }
}
