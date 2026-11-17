using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Converter.Tests.Fixtures;

/// <summary>
/// A made-up section type for tests: points in game coordinates, one 12-byte record each (x, y, z as f32). It
/// exercises the loader registry, the world model, the area of interest and render layers the way the real
/// sections of steps 1.2–1.4 will.
/// </summary>
internal static class TestPoints
{
    public const string SectionName = "test-points";
    public const string FileName = "points.bin";
    public const string RecordType = "testPoint";
    public const int RecordSize = 12;

    public static DumpVersion Version { get; } = new(0, 1);
}

/// <summary>Loads <see cref="TestPoints"/> sections; skips points that aren't finite, with a warning.</summary>
internal sealed class TestPointsLoader : ISectionLoader
{
    public string SectionName { get; init; } = TestPoints.SectionName;

    public DumpVersion Version { get; init; } = TestPoints.Version;

    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        var points = new List<Vector3>();
        using RecordFileReader reader = section.OpenRecordFile(TestPoints.FileName, TestPoints.RecordType, TestPoints.RecordSize);
        while (reader.MoveNext())
        {
            var point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (!(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z)))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Point {reader.RecordIndex} isn't finite, so it is skipped."));
                continue;
            }

            points.Add(point);
        }

        return new TestPointsData(points);
    }
}

/// <summary>The points of a <see cref="TestPoints"/> section.</summary>
internal sealed class TestPointsData : ISectionData
{
    public TestPointsData(IReadOnlyList<Vector3> points)
    {
        Points = points;
        foreach (Vector3 point in points)
        {
            var bounds = new MapRect(point.X, point.Z, point.X, point.Z);
            AreaOfInterestBounds = AreaOfInterestBounds?.Union(bounds) ?? bounds;
        }
    }

    public IReadOnlyList<Vector3> Points { get; }

    public IReadOnlyList<SectionStatistic> Statistics =>
    [
        new("points", Points.Count.ToString(CultureInfo.InvariantCulture)),
        new("bounds", AreaOfInterestBounds?.ToString() ?? "none"),
    ];

    public MapRect? AreaOfInterestBounds { get; }
}

/// <summary>Draws the test points as a magenta polyline.</summary>
internal sealed class TestPointsLayer : IRenderLayer
{
    public static Rgb Color { get; } = new(200, 0, 200);

    public string Name => "points";

    public string Description => "test points joined in magenta";

    public void Draw(RenderContext context)
    {
        if (context.World.Find<TestPointsData>() is not { } data)
        {
            return;
        }

        for (int i = 1; i < data.Points.Count; i++)
        {
            Vector3 from = data.Points[i - 1];
            Vector3 to = data.Points[i];
            context.Canvas.DrawLine(from.X, from.Z, to.X, to.Z, Color);
        }
    }
}
