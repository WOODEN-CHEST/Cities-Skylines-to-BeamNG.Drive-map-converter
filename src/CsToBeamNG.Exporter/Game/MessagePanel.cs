using ColossalFramework.UI;

namespace CsToBeamNG.Exporter.Game;

/// <summary>Shows messages to the user in the game's message panel.</summary>
internal static class MessagePanel
{
    /// <summary>The title of the exporter's messages.</summary>
    public const string Title = "CS to BeamNG";

    /// <summary>
    /// Shows a message in the panel that the game uses for its own errors (<c>ExceptionPanel</c>). Main
    /// thread only.
    /// </summary>
    /// <param name="message">The message. Line breaks are kept.</param>
    /// <param name="isError">Whether it reports a failure, which the panel shows as an error.</param>
    public static void Show(string message, bool isError) =>
        UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(Title, message, isError);
}
