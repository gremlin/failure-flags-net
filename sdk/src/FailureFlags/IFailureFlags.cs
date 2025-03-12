namespace FailureFlags
{
    /// <summary>
    /// Interace exposing the core functionality of the FailureFlags system.
    /// GremlinFailureFlags.cs is the default implementation.
    /// </summary>
    public interface IFailureFlags
    {
        /// <summary>
        /// Retrieves active experiments targeting the provided Failure Flag.
        /// </summary>
        /// <param name="flag">Failure Flag to invoke</param>
        /// <returns>Array of active experiments. Null if there are no active experiments targeting the provided Failure Flag.</returns>
        Experiment[] Fetch(FailureFlag flag);

        /// <summary>
        /// Fetches and applies default behaviors for any experiments targeting the provided Failure Flag.
        /// </summary>
        /// <param name="flag">Failure Flag to invoke</param>
        /// <returns>Array of active experiments. Null if there are no active experiments targeting the provided Failure Flag.</returns>
        Experiment[] Invoke(FailureFlag flag);

        /// <summary>
        /// Fetches and applies the provided behavior for any experiments targeting the provided Failure Flag.
        /// </summary>
        /// <param name="flag">Failure Flag to invoke</param>
        /// <param name="behavior">Behavior to use for any active experiments</param>
        /// <returns>Array of active experiments. Null if there are no active experiments targeting the provided Failure Flag.</returns>
        Experiment[] Invoke(FailureFlag flag, IBehavior behavior);
    }
}
