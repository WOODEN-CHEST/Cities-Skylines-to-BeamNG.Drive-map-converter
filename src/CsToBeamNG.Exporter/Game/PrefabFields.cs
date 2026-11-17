using System;
using System.Globalization;
using CsToBeamNG.Dump.Sections;
using UnityEngine;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Converts what buildings, props and trees share into the dump's types: vectors, colours, mesh bounds and item
/// classes. Only reads.
/// </summary>
internal static class PrefabFields
{
    /// <summary>Copies a vector.</summary>
    public static DumpVector3 ToDump(Vector3 vector) => new(vector.x, vector.y, vector.z);

    /// <summary>Copies a colour.</summary>
    public static DumpColor ToDump(Color color) => new(color.r, color.g, color.b, color.a);

    /// <summary>Copies an item class; an empty one if the prefab has none.</summary>
    public static DumpItemClass ToDump(ItemClass? itemClass) => itemClass == null
        ? new DumpItemClass()
        : new DumpItemClass
        {
            Name = itemClass!.name ?? string.Empty,
            Service = itemClass.m_service.ToString(),
            SubService = itemClass.m_subService.ToString(),
            Level = itemClass.m_level.ToString(),
            Layer = unchecked((uint)itemClass.m_layer),
        };

    /// <summary>
    /// Returns a mesh's bounds as Unity keeps them, or <see langword="null"/> without a mesh. Reading them on the
    /// simulation thread works: step 0.7's geometry probe read network meshes there.
    /// </summary>
    public static DumpBounds? MeshBounds(Mesh? mesh)
    {
        if (mesh == null)
        {
            return null;
        }

        Bounds bounds = mesh!.bounds;
        return new DumpBounds(ToDump(bounds.min), ToDump(bounds.max));
    }

    /// <summary>Names an object and its prefab for <c>export.log</c>, such as <c>Building 12 ("L1 2x2 Detached01")</c>.</summary>
    /// <typeparam name="T">The prefab type.</typeparam>
    /// <param name="kind">What the object is, capitalised.</param>
    /// <param name="id">The object's id.</param>
    /// <param name="prefabIndex">The index of its prefab.</param>
    public static string Describe<T>(string kind, long id, ushort prefabIndex)
        where T : PrefabInfo
    {
        string name;
        try
        {
            name = PrefabCollection<T>.PrefabName(prefabIndex) ?? "an unnamed prefab";
        }
        catch (Exception e)
        {
            name = "a prefab whose name can't be read (" + e.GetType().Name + ")";
        }

        return kind + " " + id.ToString(CultureInfo.InvariantCulture) + " (\"" + name + "\")";
    }
}
