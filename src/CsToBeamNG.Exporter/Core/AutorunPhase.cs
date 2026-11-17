namespace CsToBeamNG.Exporter.Core;

/// <summary>Where an <see cref="AutorunSequence"/> is.</summary>
internal enum AutorunPhase
{
    /// <summary>Waiting for the main menu to be ready.</summary>
    WaitingForMenu,

    /// <summary>The save is loading.</summary>
    LoadingSave,

    /// <summary>The export is running.</summary>
    Exporting,

    /// <summary>The final status is out and the game was asked to quit.</summary>
    Quitting,

    /// <summary>Nothing more to do: the game keeps running, or it was forced to quit.</summary>
    Done,
}
