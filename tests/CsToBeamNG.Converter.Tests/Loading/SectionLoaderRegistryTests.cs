using System;
using System.Collections.Generic;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Tests.Fixtures;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Loading;

public sealed class SectionLoaderRegistryTests
{
    [Fact]
    public void KeepsTheRegistrationOrder()
    {
        var first = new TestPointsLoader { SectionName = "b-points" };
        var second = new TestPointsLoader { SectionName = "a-points" };

        SectionLoaderRegistry registry = new SectionLoaderRegistry().Add(first).Add(second);

        Assert.Equal([first, second], registry.Loaders);
        Assert.Same(second, registry.Find("a-points"));
        Assert.Null(registry.Find("A-points"));
        Assert.Null(registry.Find("c-points"));
    }

    [Fact]
    public void RejectsASecondLoaderForTheSameSection()
    {
        SectionLoaderRegistry registry = new SectionLoaderRegistry().Add(new TestPointsLoader());

        ArgumentException error = Assert.Throws<ArgumentException>(() => registry.Add(new TestPointsLoader()));
        Assert.Contains("'test-points' is registered already", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultLoadersReadDistinctSections()
    {
        // Each export step registers its section's loader in CreateDefault (terrain and water since step 1.2).
        IReadOnlyList<ISectionLoader> loaders = SectionLoaderRegistry.CreateDefault().Loaders;

        Assert.Equal(loaders.Count, loaders.Select(loader => loader.SectionName).Distinct(StringComparer.Ordinal).Count());
        Assert.NotSame(SectionLoaderRegistry.CreateDefault(), SectionLoaderRegistry.CreateDefault());
    }
}
