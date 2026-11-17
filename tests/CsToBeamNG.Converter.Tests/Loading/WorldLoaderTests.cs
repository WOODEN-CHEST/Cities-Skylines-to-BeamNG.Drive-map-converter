using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Loading;

public sealed class WorldLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LoadsAKnownSectionIntoTheWorld()
    {
        string folder = new SyntheticDump().WithUnlockedTiles(new TileCoordinate(4, 4)).WithPoints(new(1, 2, 3), new(-4, 5, 6)).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader());

        SectionLoadResult section = Assert.Single(result.Sections);
        Assert.Equal(SectionLoadStatus.Loaded, section.Status);
        Assert.Equal(TestPoints.Version, section.LoaderVersion);
        Assert.Empty(result.Messages);
        Assert.False(result.HasErrors);
        TestPointsData? data = result.World.Find<TestPointsData>();
        Assert.NotNull(data);
        Assert.Same(data, section.Data);
        Assert.Equal([new Vector3(1, 2, 3), new Vector3(-4, 5, 6)], data.Points);
        Assert.Equal(folder, result.World.DumpFolder);
        Assert.Equal("Synthetic City", result.World.Manifest.City.Name);
        Assert.Equal([new TileCoordinate(4, 4)], result.World.UnlockedTiles);
    }

    [Fact]
    public void SkipsAnUnknownSectionWithANote()
    {
        string folder = new SyntheticDump().WithOpaqueSection("mystery", new DumpVersion(0, 3)).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader());

        SectionLoadResult section = Assert.Single(result.Sections);
        Assert.Equal(SectionLoadStatus.Unknown, section.Status);
        Assert.Null(section.LoaderVersion);
        Assert.Null(section.Data);
        LoadMessage message = Assert.Single(result.Messages);
        Assert.Equal(new LoadMessage(LoadMessageSeverity.Info, "Section 'mystery' (version 0.3) is unknown to this version of CsToBeamNG, so it is skipped.", "mystery"), message);
        Assert.Empty(result.World.Sections);
        Assert.False(result.HasErrors);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, "Update CsToBeamNG to convert it.")]
    [InlineData(0, 4, 1, 0, "Export the city again with the current exporter.")]
    public void SkipsASectionOfAnotherMajorVersionWithAWarning(int sectionMajor, int sectionMinor, int loaderMajor, int loaderMinor, string remedy)
    {
        string folder = new SyntheticDump()
            .WithPoints(TestPoints.SectionName, new DumpVersion(sectionMajor, sectionMinor), TestPoints.RecordType, Vector3.One)
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader { Version = new DumpVersion(loaderMajor, loaderMinor) });

        SectionLoadResult section = Assert.Single(result.Sections);
        Assert.Equal(SectionLoadStatus.Incompatible, section.Status);
        Assert.Null(section.Data);
        LoadMessage message = Assert.Single(result.Messages);
        Assert.Equal(LoadMessageSeverity.Warning, message.Severity);
        Assert.Equal(
            $"Section 'test-points' has version {sectionMajor}.{sectionMinor}, but this version of CsToBeamNG reads only versions {loaderMajor}.x, so it is skipped. {remedy}",
            message.Message);
        Assert.Null(result.World.Find<TestPointsData>());
    }

    [Fact]
    public void ReadsANewerMinorVersionWithANote()
    {
        string folder = new SyntheticDump()
            .WithPoints(TestPoints.SectionName, new DumpVersion(0, 5), TestPoints.RecordType, Vector3.One)
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader());

        Assert.Equal(SectionLoadStatus.Loaded, Assert.Single(result.Sections).Status);
        LoadMessage message = Assert.Single(result.Messages);
        Assert.Equal(LoadMessageSeverity.Info, message.Severity);
        Assert.Equal("Section 'test-points' has version 0.5, newer than the 0.1 that this version of CsToBeamNG knows. Data added since is ignored.", message.Message);
        Assert.Equal("test-points", message.SectionName);
    }

    [Fact]
    public void ReadsAnOlderMinorVersionQuietly()
    {
        string folder = new SyntheticDump()
            .WithPoints(TestPoints.SectionName, new DumpVersion(0, 1), TestPoints.RecordType, Vector3.One)
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader { Version = new DumpVersion(0, 3) });

        Assert.Equal(SectionLoadStatus.Loaded, Assert.Single(result.Sections).Status);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void SkipsADamagedSectionWithAnErrorAndGoesOn()
    {
        string folder = new SyntheticDump()
            .WithPoints(TestPoints.SectionName, TestPoints.Version, "otherPoint", Vector3.One)
            .WithOpaqueSection("mystery", new DumpVersion(0, 1))
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader());

        Assert.Equal([SectionLoadStatus.Failed, SectionLoadStatus.Unknown], result.Sections.Select(s => s.Status));
        Assert.True(result.HasErrors);
        LoadMessage error = result.Messages[0];
        Assert.Equal(LoadMessageSeverity.Error, error.Severity);
        Assert.StartsWith("Section 'test-points' couldn't be read, so it is skipped: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("otherPoint", error.Message, StringComparison.Ordinal);
        Assert.Equal(LoadMessageSeverity.Info, result.Messages[1].Severity);
        Assert.Empty(result.World.Sections);
    }

    [Fact]
    public void KeepsWhatTheLoaderReported()
    {
        string folder = new SyntheticDump()
            .WithPoints(new Vector3(1, 1, 1), new Vector3(float.NaN, 0, 0), new Vector3(2, 2, 2))
            .WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader());

        Assert.Equal(2, result.World.Find<TestPointsData>()?.Points.Count);
        Assert.Equal([new LoadMessage(LoadMessageSeverity.Warning, "Point 1 isn't finite, so it is skipped.", "test-points")], result.Messages);
        Assert.Equal(result.Messages, Assert.Single(result.Sections).Messages);
    }

    [Fact]
    public void ListsTheSectionsTheDumpLacks()
    {
        string folder = new SyntheticDump().WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = Load(folder, new TestPointsLoader(), new TestPointsLoader { SectionName = "more-points" });

        Assert.Equal(["test-points", "more-points"], result.MissingSections);
        Assert.Empty(result.Sections);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void ListsSectionsInManifestOrderButLoadsThemInRegistrationOrder()
    {
        string folder = new SyntheticDump()
            .WithPoints("later-points", TestPoints.Version, TestPoints.RecordType, new Vector3(5, 5, 5))
            .WithOpaqueSection("mystery", new DumpVersion(0, 1))
            .WithPoints(new Vector3(1, 1, 1))
            .WriteTo(_temp.PathOf("dump"));
        var later = new RecordingLoader("later-points");

        WorldLoadResult result = Load(folder, new TestPointsLoader(), later);

        Assert.Equal(["later-points", "mystery", "test-points"], result.Sections.Select(s => s.Section.Name));
        Assert.Equal([SectionLoadStatus.Loaded, SectionLoadStatus.Unknown, SectionLoadStatus.Loaded], result.Sections.Select(s => s.Status));

        // The later loader saw the data of the one registered before it, and the world keeps load order.
        Assert.Equal([new Vector3(1, 1, 1)], later.SeenBefore?.Points);
        Assert.Equal(2, result.World.Sections.Count);
        Assert.Same(result.Sections[2].Data, result.World.Sections[0]);
        Assert.Same(result.Sections[0].Data, result.World.Sections[1]);
    }

    [Fact]
    public void PassesOnTheReadersNotices()
    {
        string folder = new SyntheticDump { Complete = false }.WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder, new WorldLoadOptions { AllowIncomplete = true });

        LoadMessage message = Assert.Single(result.Messages);
        Assert.Equal(LoadMessageSeverity.Warning, message.Severity);
        Assert.Contains("didn't finish", message.Message, StringComparison.Ordinal);
        Assert.Null(message.SectionName);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RejectsAnIncompleteDumpByDefault()
    {
        string folder = new SyntheticDump { Complete = false }.WriteTo(_temp.PathOf("dump"));

        DumpException error = Assert.Throws<DumpException>(() => WorldLoader.Load(folder));
        Assert.Equal(DumpErrorKind.Incomplete, error.Kind);
    }

    [Fact]
    public void UsesTheConvertersOwnLoadersByDefault()
    {
        string folder = new SyntheticDump().WithUnlockedTiles(new TileCoordinate(3, 4)).WriteTo(_temp.PathOf("dump"));

        WorldLoadResult result = WorldLoader.Load(folder);

        Assert.Empty(result.Sections);
        Assert.Equal(SectionLoaderRegistry.CreateDefault().Loaders.Select(l => l.SectionName), result.MissingSections);
        Assert.Equal([new TileCoordinate(3, 4)], result.World.UnlockedTiles);
    }

    [Fact]
    public void TreatsALoaderThatReturnsNothingAsABug()
    {
        string folder = new SyntheticDump().WithPoints(Vector3.One).WriteTo(_temp.PathOf("dump"));

        Assert.Throws<InvalidOperationException>(() => Load(folder, new NullLoader()));
    }

    private static WorldLoadResult Load(string folder, params ISectionLoader[] loaders)
    {
        var registry = new SectionLoaderRegistry();
        foreach (ISectionLoader loader in loaders)
        {
            registry.Add(loader);
        }

        return WorldLoader.Load(folder, new WorldLoadOptions { Loaders = registry });
    }

    // Loads points like the test loader and records the test points loaded before it.
    private sealed class RecordingLoader(string sectionName) : ISectionLoader
    {
        private readonly TestPointsLoader _points = new() { SectionName = sectionName };

        public string SectionName => sectionName;

        public DumpVersion Version => TestPoints.Version;

        public TestPointsData? SeenBefore { get; private set; }

        public ISectionData Load(DumpSection section, SectionLoadContext context)
        {
            Assert.Equal("Synthetic City", context.Manifest.City.Name);
            Assert.Equal(sectionName, context.SectionName);
            SeenBefore = context.Find<TestPointsData>();
            Assert.Null(context.Find<UnusedData>());
            context.Inform("Recorded.");
            return _points.Load(section, context);
        }
    }

    private sealed class NullLoader : ISectionLoader
    {
        public string SectionName => TestPoints.SectionName;

        public DumpVersion Version => TestPoints.Version;

        public ISectionData Load(DumpSection section, SectionLoadContext context) => null!;
    }

    private sealed class UnusedData : ISectionData
    {
        public IReadOnlyList<SectionStatistic> Statistics => [];

        public MapRect? AreaOfInterestBounds => null;
    }
}
