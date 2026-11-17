using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Props;

/// <summary>Loads the <c>props</c> section into a <see cref="CityProps"/> (docs/dump-format.md, <c>props</c>).</summary>
public sealed class PropsLoader : ISectionLoader
{
    /// <inheritdoc/>
    public string SectionName => PropsSection.Name;

    /// <inheritdoc/>
    public DumpVersion Version => PropsSection.Version;

    /// <inheritdoc/>
    /// <remarks>
    /// A prop with a value that isn't finite, or whose id was listed already, is skipped with a warning. Props whose
    /// prefab the <c>prop-prefabs</c> section lacks are kept and reported together.
    /// </remarks>
    public ISectionData Load(DumpSection section, SectionLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        PropsSectionContent content = PropsSectionReader.Read(section);
        CityPropPrefabs? prefabs = context.Find<CityPropPrefabs>();
        var unknownPrefabs = new MissingReferences("props have a prefab that the prop-prefabs section lacks");
        var props = new List<PropRecord>(content.Props.Count);
        var ids = new HashSet<uint>();
        foreach (PropRecord prop in content.Props.OrderBy(prop => prop.Id))
        {
            if (!prop.Position.IsFinite || !float.IsFinite(prop.Angle) || !float.IsFinite(prop.Scale) || !prop.Color.IsFinite)
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Prop {prop.Id} has a position, angle, scale or colour that isn't finite, so it is skipped."));
                continue;
            }

            if (!ids.Add(prop.Id))
            {
                context.Warn(string.Create(CultureInfo.InvariantCulture, $"Prop {prop.Id} is listed twice, so its second record is skipped."));
                continue;
            }

            if (prefabs is not null && !prefabs.TryGet(prop.Prefab, out _))
            {
                unknownPrefabs.Add(string.Create(CultureInfo.InvariantCulture, $"prop {prop.Id} (prefab {prop.Prefab})"));
            }

            props.Add(prop);
        }

        unknownPrefabs.Report(context);
        return new CityProps(props, prefabs);
    }
}
