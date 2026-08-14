using Microsoft.Extensions.Logging;

namespace FailureFlags
{
    /// <summary>
    /// DelayedException processes <code>latency</code> and <code>exception</code> properties in experiment effects. All
    /// latency effects will be applied before any exceptions are thrown.
    ///
    /// Behaviors implement specific effects or symptoms of failures that an application will experience in calls to
    /// FailureFlags.invoke(...). When processing multiple experiments, delays should be applied before other failure types
    /// and those failure types that can be processed without changing flow should be applied first. If multiple experiments
    /// result in changing control flow (like exceptions, shutdowns, panics, etc.) then the behavior chain may not realize
    /// some effects.
    /// </summary>
    public class DelayedException : IBehavior
    {
        private readonly Latency _latency;
        private readonly ExceptionBehavior _exception;

        public DelayedException() : this(null)
        {
        }

        public DelayedException(ILogger? logger)
        {
            _latency = new Latency(logger);
            _exception = new ExceptionBehavior(logger);
        }

        public void ApplyBehavior(Experiment[] experiments)
        {
            _latency.ApplyBehavior(experiments);
            _exception.ApplyBehavior(experiments);
        }
    }
}
