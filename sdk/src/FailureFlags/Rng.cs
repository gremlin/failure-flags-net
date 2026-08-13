using System;

namespace FailureFlags
{
    /// <summary>
    /// The SDK's single source of randomness.
    ///
    /// One <see cref="Random"/> per thread, for two reasons. <see cref="Random"/> is not thread
    /// safe, so a single shared instance can have its internal state corrupted by concurrent
    /// callers. And allocating a fresh one per roll is waste on a path that runs per request.
    /// </summary>
    internal static class Rng
    {
        [ThreadStatic]
        private static Random? _random;

        /// <summary>
        /// Returns a random double in [0.0, 1.0).
        /// </summary>
        internal static double NextDouble()
        {
            // Seeded explicitly: on .NET Framework the parameterless constructor is clock seeded,
            // so threads that first roll within the same tick would otherwise share a sequence.
            _random ??= new Random(Guid.NewGuid().GetHashCode());
            return _random.NextDouble();
        }
    }
}
