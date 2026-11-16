namespace CsToBeamNG.Converter.Discovery;

/// <summary>How a location was determined.</summary>
public enum LocationSource
{
    /// <summary>Given by the user, through a command-line option or an environment variable.</summary>
    Override,

    /// <summary>Read from the Windows registry.</summary>
    Registry,

    /// <summary>Found through Steam: a Steam library and the game's app manifest.</summary>
    SteamLibrary,

    /// <summary>A setting of the game itself, such as <c>UserPath</c> in BeamNG.drive's <c>startup.ini</c>.</summary>
    GameSetting,

    /// <summary>A standard location of the OS or of the game.</summary>
    Default,

    /// <summary>Inside the Proton (Wine) prefix that Steam keeps for a game.</summary>
    ProtonPrefix,

    /// <summary>Derived from another location, such as the mods folder inside the user data folder.</summary>
    Derived,
}
