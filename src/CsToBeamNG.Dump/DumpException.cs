using System;

namespace CsToBeamNG.Dump;

/// <summary>
/// A dump or one of its files can't be read. The message is meant for the user: it names the file, what
/// is wrong and, where possible, what to do.
/// </summary>
public sealed class DumpException : Exception
{
    /// <summary>Creates an exception.</summary>
    /// <param name="kind">Why the dump can't be read.</param>
    /// <param name="message">A message for the user.</param>
    public DumpException(DumpErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Creates an exception with the error that caused it.</summary>
    /// <param name="kind">Why the dump can't be read.</param>
    /// <param name="message">A message for the user.</param>
    /// <param name="innerException">The error that caused it.</param>
    public DumpException(DumpErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Why the dump can't be read.</summary>
    public DumpErrorKind Kind { get; }
}
