using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Water;

/// <summary>Loads the <c>water</c> section into a <see cref="CityWater"/> (docs/dump-format.md, <c>water</c>).</summary>
public sealed class WaterLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => WaterSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => WaterSection.Version;

    /// <inheritdoc/>
    /// <remarks>A water source whose position isn't finite is skipped with a warning.</remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        WaterSectionContent content = WaterSectionReader.Read(section);
        var sources = new List<WaterSourceRecord>(content.Sources.Count);
        foreach (WaterSourceRecord source in content.Sources)
        {
            if (!(float.IsFinite(source.InputX) && float.IsFinite(source.InputY) && float.IsFinite(source.InputZ)
                && float.IsFinite(source.OutputX) && float.IsFinite(source.OutputY) && float.IsFinite(source.OutputZ)))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Water source {source.Id} has a position that isn't finite, so it is skipped."));
                continue;
            }

            sources.Add(source);
        }

        return new CityWater(content, sources);
    }
}
