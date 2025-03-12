namespace FailureFlags
{
    /// <summary>
    /// Behaviors implement specific effects or symptoms of failures that an application will experience in calls to
    /// FailureFlags.invoke(...). When processing multiple experiments, delays should be applied before other failure types
    /// and those failure types that can be processed without changing flow should be applied first. If multiple experiments
    /// result in changing control flow (like exceptions, shutdowns, panics, etc.) then the behavior chain may not realize
    /// some effects.
    /// </summary>
    /// <example>
    /// See Latency.cs, Exception and DelayedException.cs for examples
    /// </example>
    public interface IBehavior
    {
        /// <summary>
        /// Applies any behavior described by the effect statements in each experiment in the provided array.
        /// </summary>
        /// <param name="experiments">An ordered array of active experiments to apply</param>
        void ApplyBehavior(Experiment[] experiments);
    }
}
