using System;

namespace FailureFlags
{
    /// <summary>
    /// Compatibility shim for the name this behavior had before 1.1.0.
    ///
    /// <c>FailureFlags.Exception</c> is an ambiguous reference against <see cref="System.Exception"/>
    /// for any consumer with <c>using FailureFlags;</c>, so every bare <c>catch (Exception e)</c> in
    /// their code stops compiling with CS0104. Use <see cref="ExceptionBehavior"/>.
    ///
    /// Note that keeping this shim keeps that ambiguity alive. It exists only so that existing
    /// source keeps compiling for one release.
    /// </summary>
    [Obsolete("Renamed to ExceptionBehavior, because FailureFlags.Exception is ambiguous with System.Exception. This shim will be removed in the next release.")]
    public class Exception : ExceptionBehavior
    {
    }
}
