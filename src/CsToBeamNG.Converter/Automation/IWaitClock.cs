using System;
using System.Threading;

namespace CsToBeamNG.Converter.Automation;

/// <summary>The time and a way to wait, so tests can run waits instantly.</summary>
public interface IWaitClock
{
    /// <summary>The current time in UTC.</summary>
    DateTime UtcNow { get; }

    /// <summary>Waits.</summary>
    /// <param name="duration">How long.</param>
    void Wait(TimeSpan duration);
}

/// <summary>The real clock.</summary>
public sealed class SystemWaitClock : IWaitClock
{
    /// <summary>The instance.</summary>
    public static readonly SystemWaitClock Instance = new();

    private SystemWaitClock()
    {
    }

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc/>
    public void Wait(TimeSpan duration) => Thread.Sleep(duration);
}
