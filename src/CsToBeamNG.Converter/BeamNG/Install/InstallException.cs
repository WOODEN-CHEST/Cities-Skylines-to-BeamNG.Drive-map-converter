using System;

namespace CsToBeamNG.Converter.BeamNG.Install;

/// <summary>An install was refused because the target isn't safe to replace.</summary>
public sealed class InstallException : Exception
{
    /// <summary>Creates an exception without a message.</summary>
    public InstallException()
    {
    }

    /// <summary>Creates an exception.</summary>
    /// <param name="message">What is wrong, and what the user can do.</param>
    public InstallException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a cause.</summary>
    /// <param name="message">What is wrong, and what the user can do.</param>
    /// <param name="innerException">The cause.</param>
    public InstallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
