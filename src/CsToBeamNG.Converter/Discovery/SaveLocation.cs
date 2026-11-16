namespace CsToBeamNG.Converter.Discovery;

/// <summary>Where a Cities: Skylines savegame is stored.</summary>
public enum SaveLocation
{
    /// <summary>The <c>Saves</c> folder in the game's user data folder.</summary>
    Local,

    /// <summary>Steam Cloud's local copy, in Steam's <c>userdata</c> folder.</summary>
    SteamCloud,
}
