using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CsToBeamNG.Converter.Development.HeightProbe;

/// <summary>
/// The heights probe file that the exporter writes on request (<c>cstobeamng export --probe heights</c>): the game's
/// own terrain height samplers evaluated at road nodes, road segments, buildings, building lot corners and test
/// points, and counts of where the game's height arrays differ. A developer format for research (plan step 1.2), read
/// only by <see cref="HeightProbeCheck"/> (docs/research/cs1-save-and-world.md, Terrain).
/// </summary>
public sealed class HeightProbeFile
{
    /// <summary>The format name in the file.</summary>
    public const string FormatName = "cstobeamng-heights-probe";

    /// <summary>The format version this reader understands.</summary>
    public const int FormatVersion = 1;

    private HeightProbeFile(string gameVersion, IReadOnlyList<HeightProbeSample> samples, IReadOnlyList<HeightArrayComparison> arrays)
    {
        GameVersion = gameVersion;
        Samples = samples;
        Arrays = arrays;
    }

    /// <summary>The game version that wrote the probe.</summary>
    public string GameVersion { get; }

    /// <summary>The probed positions with the game's heights there.</summary>
    public IReadOnlyList<HeightProbeSample> Samples { get; }

    /// <summary>How the game's height arrays differed from each other, in the file's order.</summary>
    public IReadOnlyList<HeightArrayComparison> Arrays { get; }

    /// <summary>Reads a probe file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="InvalidDataException">The file isn't a heights probe of this version.</exception>
    public static HeightProbeFile Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        string format = root.GetProperty("format").GetString() ?? string.Empty;
        int version = root.GetProperty("version").GetInt32();
        if (format != FormatName || version != FormatVersion)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"'{path}' is a '{format}' file of version {version}; expected {FormatName} version {FormatVersion}."));
        }

        var arrays = new List<HeightArrayComparison>();
        foreach (JsonProperty property in root.GetProperty("arrays").EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("differing", out JsonElement differing))
            {
                arrays.Add(new HeightArrayComparison(
                    property.Name,
                    property.Value.GetProperty("values").GetInt32(),
                    differing.GetInt32(),
                    property.Value.GetProperty("maxAbove").GetDouble(),
                    property.Value.GetProperty("maxBelow").GetDouble()));
            }
        }

        var samples = new List<HeightProbeSample>();
        foreach (JsonElement sample in root.GetProperty("samples").EnumerateArray())
        {
            samples.Add(new HeightProbeSample(
                sample.GetProperty("kind").GetString() ?? string.Empty,
                sample.GetProperty("id").GetInt32(),
                sample.GetProperty("label").GetString() ?? string.Empty,
                uint.Parse(sample.GetProperty("flags").GetString() ?? "0", NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                sample.GetProperty("x").GetDouble(),
                sample.GetProperty("y").GetDouble(),
                sample.GetProperty("z").GetDouble(),
                sample.GetProperty("detail").GetDouble(),
                sample.GetProperty("final").GetDouble(),
                sample.GetProperty("raw").GetDouble(),
                sample.GetProperty("rawSmooth").GetDouble(),
                sample.GetProperty("blockSmooth").GetDouble()));
        }

        return new HeightProbeFile(root.GetProperty("game").GetString() ?? string.Empty, samples, arrays);
    }
}

/// <summary>A position the heights probe sampled, with the game's heights there, in metres.</summary>
/// <param name="Kind">What was sampled: <c>node</c>, <c>segment</c> (its middle), <c>building</c>, <c>lot-corner</c> or <c>point</c>.</param>
/// <param name="Id">The game's id of the object, or a running number for points.</param>
/// <param name="Label">The object's prefab name, or what kind of point it is.</param>
/// <param name="Flags">The object's flags as the game stores them; 0 for points.</param>
/// <param name="X">The world X.</param>
/// <param name="Y">The object's own height; 0 for points.</param>
/// <param name="Z">The world Z.</param>
/// <param name="Detail">The game's <c>SampleDetailHeight</c>: the ground it draws and places objects on.</param>
/// <param name="Final">The game's <c>SampleFinalHeight</c>: the final heights, bilinear.</param>
/// <param name="Raw">The game's <c>SampleRawHeight</c>: the raw heights (its <c>m_rawHeights2</c> copy), bilinear.</param>
/// <param name="RawSmooth">The game's <c>SampleRawHeightSmooth</c>, which roads and buildings are placed by.</param>
/// <param name="BlockSmooth">The game's <c>SampleBlockHeightSmooth</c>: the water simulation's ground.</param>
public sealed record HeightProbeSample(
    string Kind,
    int Id,
    string Label,
    uint Flags,
    double X,
    double Y,
    double Z,
    double Detail,
    double Final,
    double Raw,
    double RawSmooth,
    double BlockSmooth);

/// <summary>Where two of the game's height arrays differed when the probe ran.</summary>
/// <param name="Name">Which arrays, such as <c>finalVsRaw</c>.</param>
/// <param name="Values">The number of values compared.</param>
/// <param name="Differing">The number of values that differ.</param>
/// <param name="MaxAboveMeters">The most the first array is above the second, in metres.</param>
/// <param name="MaxBelowMeters">The most the first array is below the second, in metres (0 or negative).</param>
public sealed record HeightArrayComparison(string Name, int Values, int Differing, double MaxAboveMeters, double MaxBelowMeters);
