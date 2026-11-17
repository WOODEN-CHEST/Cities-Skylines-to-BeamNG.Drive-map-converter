namespace CsToBeamNG.Converter.Automation;

/// <summary>Starts Cities: Skylines and checks what is running. <see cref="CitiesSkylinesLauncher"/> is the real one.</summary>
public interface IGameLauncher
{
    /// <summary>Whether Cities: Skylines is already running.</summary>
    bool IsGameRunning();

    /// <summary>Whether the Steam client is running, which the game needs.</summary>
    bool IsSteamRunning();

    /// <summary>Starts the game. Throws if it can't be started.</summary>
    IGameProcess Start();
}
