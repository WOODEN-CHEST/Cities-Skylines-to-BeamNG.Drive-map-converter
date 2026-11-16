using System.Collections.Generic;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// Where Cities: Skylines and its folders are. The names follow the MSBuild properties of the build
/// (<c>CitiesSkylinesDir</c>, <c>CitiesSkylinesManagedDir</c>, ...); see <c>agents/environment.md</c>.
/// </summary>
public sealed record CitiesSkylinesInfo
{
    /// <summary>The install folder (contains <c>Cities_Data</c>), or <see langword="null"/> if not found.</summary>
    public FoundPath? InstallDir { get; init; }

    /// <summary>The Steam build id from the app manifest, if the game was found through Steam.</summary>
    public string? SteamBuildId { get; init; }

    /// <summary>The game's managed assemblies, <c>Cities_Data/Managed</c>.</summary>
    public FoundPath? ManagedDir { get; init; }

    /// <summary>The user data folder (contains <c>Addons</c> and <c>Saves</c>).</summary>
    public FoundPath? UserDir { get; init; }

    /// <summary>The local mods folder, <c>Addons/Mods</c> in the user data folder.</summary>
    public FoundPath? ModsDir { get; init; }

    /// <summary>The folder of the CsToBeamNG exporter mod in the mods folder.</summary>
    public FoundPath? ExporterModDir { get; init; }

    /// <summary>The assembly version of the deployed exporter mod, if it is there.</summary>
    public string? ExporterModVersion { get; init; }

    /// <summary>The folder where the exporter writes city dumps.</summary>
    public FoundPath? DumpRoot { get; init; }

    /// <summary>The folder with the subscribed Steam Workshop items.</summary>
    public FoundPath? WorkshopDir { get; init; }

    /// <summary>The number of items in <see cref="WorkshopDir"/>.</summary>
    public int WorkshopItemCount { get; init; }

    /// <summary>The game's log file (Unity's player log).</summary>
    public FoundPath? LogFile { get; init; }

    /// <summary>The savegames found, local ones first, then Steam Cloud ones, each sorted by name.</summary>
    public IReadOnlyList<SaveGame> Saves { get; init; } = [];
}
