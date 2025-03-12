namespace FailureFlags
{
    /// <summary>
    /// Represents an exception thrown while processing experiments with an
    /// <code>exception</code> property on its effect statement.
    /// <example>
    /// Given the effect statement:
    /// {
    ///     "exception": { "message": "TestException" }
    /// }
    /// Throw a FailureFlagException with the message "TestException"
    /// </example>
    /// 
    /// </summary>
    public class FailureFlagException : System.Exception
    {
        public FailureFlagException(string message) : base(message)
        { }
    }
}
