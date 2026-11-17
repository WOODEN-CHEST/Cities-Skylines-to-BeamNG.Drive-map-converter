using System;
using System.Collections.Generic;
using ColossalFramework.Math;
using CsToBeamNG.Dump.Sections;
using CsToBeamNG.Exporter.Core;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the props placed on their own and their prefabs into the contents of the dump's <c>props</c> and
/// <c>prop-prefabs</c> sections, without changing anything (docs/research/cs1-assets.md, Placement). Create it on the
/// main thread; read on the simulation thread, where the game changes the props.
/// </summary>
/// <remarks>
/// Positions and angles come through the game's accessors, which mods that store them more precisely replace. Scale
/// and colour come from the game's randomizer seeded with the prop's id and the prefab's own <c>GetColor</c>, in the
/// order <c>PropInstance.RenderInstance</c> draws them. A prop that can't be read is skipped and counted in the
/// <see cref="ObjectFailures"/> passed in.
/// </remarks>
internal sealed class PropReader
{
    // A MultiEffect can hold MultiEffects; this bounds the search through a damaged or circular one.
    private const int MaxEffectDepth = 8;

    // The renderer's scale: the prefab's minimum plus Int32(10000) × the range × 0.0001.
    private const uint ScaleSteps = 10000;
    private const float ScaleStep = 0.0001f;

    private readonly PropManager _props;

    private PropReader(PropManager props) => _props = props;

    /// <summary>Finds the prop manager. Main thread only (<see cref="CityReader.Require{T}"/>).</summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    public static PropReader Create() => new(CityReader.Require<PropManager>());

    /// <summary>Reads every created prop. Simulation thread only.</summary>
    /// <param name="failures">Where to count the props that can't be read.</param>
    public PropsSectionContent ReadProps(ObjectFailures failures)
    {
        var content = new PropsSectionContent();
        PropInstance[] props = _props.m_props.m_buffer;
        for (int i = 0; i < props.Length; i++)
        {
            if (((PropInstance.Flags)props[i].m_flags & PropInstance.Flags.Created) == 0)
            {
                continue;
            }

            try
            {
                content.Props.Add(ReadProp(i, props[i]));
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<PropInfo>("Prop", i, props[i].m_infoIndex) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    /// <summary>Reads the prefabs that created props use. Simulation thread only.</summary>
    /// <param name="failures">Where to count the prefabs that can't be read, such as a missing asset's.</param>
    public PropPrefabsSectionContent ReadPrefabs(ObjectFailures failures)
    {
        var content = new PropPrefabsSectionContent();
        foreach (ushort index in UsedPrefabIndices())
        {
            try
            {
                PropInfo info = PrefabCollection<PropInfo>.GetPrefab(index);
                if (info == null)
                {
                    failures.Add(PrefabFields.Describe<PropInfo>("Prefab", index, index) + " isn't loaded, so its props have no prefab.");
                    continue;
                }

                PropPrefabRecord record = ReadPrefab(index, info);
                string? problem = PropPrefabsSectionWriter.Check(record);
                if (problem is not null)
                {
                    failures.Add(PrefabFields.Describe<PropInfo>("Prefab", index, index) + " can't be written: " + problem + ".");
                    continue;
                }

                content.Prefabs.Add(record);
            }
            catch (Exception e)
            {
                failures.Add(PrefabFields.Describe<PropInfo>("Prefab", index, index) + " couldn't be read: " + e);
            }
        }

        return content;
    }

    // The prop is a copy, so nothing here can change it.
    private static PropRecord ReadProp(int id, PropInstance prop)
    {
        PropInfo info = prop.Info;
        if (info == null)
        {
            throw new InvalidOperationException("its prefab isn't loaded");
        }

        // As PropInstance.RenderInstance: the randomizer seeded with the id (its int overload), the scale first, then
        // the colour from the same randomizer (docs/research/cs1-assets.md, Placement).
        var randomizer = new Randomizer(id);
        float scale = info.m_minScale + (randomizer.Int32(ScaleSteps) * (info.m_maxScale - info.m_minScale) * ScaleStep);
        Color color = info.GetColor(ref randomizer);
        return new PropRecord
        {
            Id = (uint)id,
            Prefab = prop.m_infoIndex,
            Position = PrefabFields.ToDump(prop.Position),
            Angle = prop.Angle,
            Scale = scale,
            Color = PrefabFields.ToDump(color),
            Flags = prop.m_flags,
        };
    }

    private static PropPrefabRecord ReadPrefab(ushort index, PropInfo info)
    {
        var record = new PropPrefabRecord
        {
            Index = index,
            Name = info.name ?? string.Empty,
            Class = PrefabFields.ToDump(info.m_class),
            Mesh = PrefabFields.MeshBounds(info.m_mesh),
            LodMesh = PrefabFields.MeshBounds(info.m_lodMesh),
            MinScale = info.m_minScale,
            MaxScale = info.m_maxScale,
            Decal = info.m_isDecal,
            Marker = info.m_isMarker,
            RequireHeightMap = info.m_requireHeightMap,
            RequireWaterMap = info.m_requireWaterMap,
            CreateRuining = info.m_createRuining,
            LightEffects = HasLight(info.m_effects),
            UseColorVariations = info.m_useColorVariations,
        };
        record.Colors[0] = PrefabFields.ToDump(info.m_color0);
        record.Colors[1] = PrefabFields.ToDump(info.m_color1);
        record.Colors[2] = PrefabFields.ToDump(info.m_color2);
        record.Colors[3] = PrefabFields.ToDump(info.m_color3);
        return record;
    }

    private static bool HasLight(PropInfo.Effect[]? effects)
    {
        foreach (PropInfo.Effect effect in effects ?? [])
        {
            if (HasLight(effect.m_effect, 0))
            {
                return true;
            }
        }

        return false;
    }

    // A light effect, or a MultiEffect that holds one.
    private static bool HasLight(EffectInfo? effect, int depth)
    {
        if (effect == null || depth > MaxEffectDepth)
        {
            return false;
        }

        if (effect is LightEffect)
        {
            return true;
        }

        if (effect is MultiEffect multi)
        {
            foreach (MultiEffect.SubEffect sub in multi.m_effects ?? [])
            {
                if (HasLight(sub.m_effect, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private List<ushort> UsedPrefabIndices()
    {
        var used = new Dictionary<ushort, bool>();
        PropInstance[] props = _props.m_props.m_buffer;
        for (int i = 0; i < props.Length; i++)
        {
            if (((PropInstance.Flags)props[i].m_flags & PropInstance.Flags.Created) != 0)
            {
                used[props[i].m_infoIndex] = true;
            }
        }

        var indices = new List<ushort>(used.Keys);
        indices.Sort();
        return indices;
    }
}
