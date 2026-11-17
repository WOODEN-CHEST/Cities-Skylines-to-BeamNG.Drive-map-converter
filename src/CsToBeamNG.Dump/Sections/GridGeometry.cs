using System;
using System.Globalization;
using CsToBeamNG.Dump.Json;
#if NET
using System.Text.Json;
#endif

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// Where the points of a square grid of samples lie, such as the terrain's heights (docs/dump-format.md,
/// <c>terrain</c>). Point (i, j) is at world X = <see cref="OriginX"/> + <see cref="Spacing"/> · i and
/// Z = <see cref="OriginZ"/> + <see cref="Spacing"/> · j, in the game's coordinates and metres. Grid files store
/// the points row by row: record j · <see cref="Resolution"/> + i.
/// </summary>
public sealed class GridGeometry
{
    /// <summary>The largest resolution, so that the number of points fits into an <see cref="int"/>.</summary>
    public const int MaxResolution = 32768;

    /// <summary>Creates a grid geometry.</summary>
    /// <param name="originX">The X of point (0, 0), in metres.</param>
    /// <param name="originZ">The Z of point (0, 0), in metres.</param>
    /// <param name="spacing">The distance between neighbouring points, in metres; more than 0.</param>
    /// <param name="resolution">The number of points along each side, 2 to <see cref="MaxResolution"/>.</param>
    /// <exception cref="ArgumentException">A value is out of range or not finite.</exception>
    public GridGeometry(float originX, float originZ, float spacing, int resolution)
    {
        string? problem = Check(originX, originZ, spacing, resolution);
        if (problem is not null)
        {
            throw new ArgumentException("Invalid grid: " + problem + ".");
        }

        OriginX = originX;
        OriginZ = originZ;
        Spacing = spacing;
        Resolution = resolution;
    }

    /// <summary>The X of point (0, 0), in metres.</summary>
    public float OriginX { get; }

    /// <summary>The Z of point (0, 0), in metres.</summary>
    public float OriginZ { get; }

    /// <summary>The distance between neighbouring points, in metres.</summary>
    public float Spacing { get; }

    /// <summary>The number of points along each side.</summary>
    public int Resolution { get; }

    /// <summary>The number of points: <see cref="Resolution"/> squared.</summary>
    public int PointCount => Resolution * Resolution;

    /// <summary>Returns <c>1081 × 1081 points, 16 m apart, from (-8640, -8640)</c>.</summary>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} × {0} points, {1} m apart, from ({2}, {3})",
        Resolution,
        JsonNumberFormat.Format(Spacing),
        JsonNumberFormat.Format(OriginX),
        JsonNumberFormat.Format(OriginZ));

    /// <summary>Writes the geometry as a JSON object property.</summary>
    internal void Write(JsonWriter json, string propertyName)
    {
        json.WriteStartObject(propertyName);
        json.WriteNumber("originX", OriginX);
        json.WriteNumber("originZ", OriginZ);
        json.WriteNumber("spacing", Spacing);
        json.WriteNumber("resolution", Resolution);
        json.WriteEndObject();
    }

#if NET
    /// <summary>Reads a geometry written by <see cref="Write"/>.</summary>
    /// <exception cref="DumpException">The object is missing or invalid.</exception>
    internal static GridGeometry Read(JsonFieldReader reader, JsonElement parent, string name, string parentPath)
    {
        JsonElement element = reader.RequiredObject(parent, name, parentPath);
        string path = JsonFieldReader.Child(parentPath, name);
        float originX = reader.RequiredSingle(element, "originX", path);
        float originZ = reader.RequiredSingle(element, "originZ", path);
        float spacing = reader.RequiredSingle(element, "spacing", path);
        int resolution = reader.RequiredInt32(element, "resolution", path, 2, MaxResolution);
        string? problem = Check(originX, originZ, spacing, resolution);
        return problem is null ? new GridGeometry(originX, originZ, spacing, resolution) : throw reader.Error(path, problem);
    }
#endif

    private static string? Check(float originX, float originZ, float spacing, int resolution)
    {
        if (!IsFinite(originX) || !IsFinite(originZ))
        {
            return "the origin must be finite";
        }

        if (!IsFinite(spacing) || spacing <= 0)
        {
            return "the spacing must be a finite number of metres above 0";
        }

        return resolution is < 2 or > MaxResolution
            ? string.Format(CultureInfo.InvariantCulture, "the resolution must be from 2 to {0} points", MaxResolution)
            : null;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
