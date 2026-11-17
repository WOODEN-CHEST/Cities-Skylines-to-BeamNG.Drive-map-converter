using System;
using System.Collections.Generic;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// The world model: everything the converter knows about a city, in CS1's own coordinates and units. It holds the
/// dump's manifest and the data of every section that was loaded (docs/architecture.md, Converter stages).
/// </summary>
/// <remarks>
/// <see cref="Loading.WorldLoader"/> builds it from a dump. Later stages read it and never change it.
/// </remarks>
public sealed class CityWorld
{
    private readonly ISectionData[] _sections;

    /// <summary>Creates a world model.</summary>
    /// <param name="dumpFolder">The dump folder it came from.</param>
    /// <param name="manifest">The dump's manifest. Treat it as read-only.</param>
    /// <param name="sections">The data of the loaded sections, in the order they were loaded.</param>
    public CityWorld(string dumpFolder, DumpManifest manifest, IEnumerable<ISectionData> sections)
    {
        ArgumentNullException.ThrowIfNull(dumpFolder);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(sections);
        DumpFolder = dumpFolder;
        Manifest = manifest;
        UnlockedTiles = [.. manifest.City.UnlockedTiles];
        _sections = [.. sections];
        if (Array.Exists(_sections, section => section is null))
        {
            throw new ArgumentException("The sections can't contain null.", nameof(sections));
        }
    }

    /// <summary>The dump folder the world was loaded from.</summary>
    public string DumpFolder { get; }

    /// <summary>The dump's manifest: where the dump came from and the city's basic facts.</summary>
    public DumpManifest Manifest { get; }

    /// <summary>The map tiles the city has unlocked, on the dump's 9 × 9 grid; empty if the exporter couldn't read them.</summary>
    public IReadOnlyList<TileCoordinate> UnlockedTiles { get; }

    /// <summary>The data of the loaded sections, in the order they were loaded.</summary>
    public IReadOnlyList<ISectionData> Sections => _sections;

    /// <summary>Returns the first loaded section data of a type, or <see langword="null"/> if none was loaded.</summary>
    /// <typeparam name="T">The section data type, such as the terrain's.</typeparam>
    public T? Find<T>()
        where T : class, ISectionData
    {
        foreach (ISectionData section in _sections)
        {
            if (section is T match)
            {
                return match;
            }
        }

        return null;
    }
}
