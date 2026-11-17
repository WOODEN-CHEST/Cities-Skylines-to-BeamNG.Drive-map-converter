namespace CsToBeamNG.Dump.Autorun;

/// <summary>How far the exporter has got with an autorun request, in the order the states occur.</summary>
public enum AutorunState
{
    /// <summary>The exporter has read the request and waits for the game's main menu.</summary>
    Accepted,

    /// <summary>The game is loading the save.</summary>
    LoadingSave,

    /// <summary>The city is loaded and being exported.</summary>
    Exporting,

    /// <summary>Final: the dump is complete.</summary>
    Succeeded,

    /// <summary>Final: the request couldn't be carried out; the status's message says why.</summary>
    Failed,
}
