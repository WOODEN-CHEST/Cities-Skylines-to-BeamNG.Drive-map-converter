using System;

namespace CsToBeamNG.Converter.Automation;

/// <summary>A game process that <see cref="IGameLauncher.Start"/> started.</summary>
public interface IGameProcess : IDisposable
{
    /// <summary>The process id.</summary>
    int Id { get; }

    /// <summary>Whether the process has ended.</summary>
    bool HasExited { get; }

    /// <summary>The exit code once the process has ended, if known.</summary>
    int? ExitCode { get; }

    /// <summary>Ends the process and its children.</summary>
    void Kill();
}
