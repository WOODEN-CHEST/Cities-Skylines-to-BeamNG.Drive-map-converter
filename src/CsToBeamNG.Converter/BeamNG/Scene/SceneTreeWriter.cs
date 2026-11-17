using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// Writes a level's scene tree as the game loads it: <c>main/items.level.json</c> declares the root group, and
/// each group's children are lines of <c>items.level.json</c> in a folder named after the group
/// (docs/research/beamng-level-format.md, Scene objects).
/// </summary>
/// <remarks>
/// Each line is one JSON object: <c>name</c> (if any), <c>class</c>, <c>persistentId</c>, <c>__parent</c> (except
/// for the root) and <c>position</c> (if any), then the other fields in ordinal order, as the game's editor writes
/// them. Lines follow the order of <see cref="SimGroup.Children"/>.
/// </remarks>
public static class SceneTreeWriter
{
    /// <summary>The name the root group must have; the game looks it up by this name.</summary>
    public const string RootGroupName = "MissionGroup";

    /// <summary>The level folder that holds the scene tree.</summary>
    public const string MainFolderName = "main";

    /// <summary>The file name of every scene file.</summary>
    public const string ItemsFileName = "items.level.json";

    /// <summary>Checks the tree and returns its files. The files are written when the caller writes them.</summary>
    /// <param name="levelName">The level's folder name, which also seeds the persistent ids.</param>
    /// <param name="rootGroup">The root group, named <see cref="RootGroupName"/>.</param>
    /// <returns>One file per group plus the root file, parents before children.</returns>
    /// <exception cref="ArgumentException">
    /// The root isn't named <see cref="RootGroupName"/>, names or keys repeat, or an object appears twice.
    /// </exception>
    public static IReadOnlyList<OutputFile> CreateFiles(string levelName, SimGroup rootGroup)
    {
        BeamNGNames.ThrowIfNotGeneratedName(levelName, nameof(levelName));
        ArgumentNullException.ThrowIfNull(rootGroup);
        if (!string.Equals(rootGroup.Name, RootGroupName, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The root group must be named {RootGroupName}, not '{rootGroup.Name}'.", nameof(rootGroup));
        }

        var snapshot = new Dictionary<SimGroup, SceneObject[]>(ReferenceEqualityComparer.Instance);
        Validate(rootGroup, snapshot);

        VirtualPath main = VirtualPath.ForLevel(levelName).Combine(MainFolderName);
        var files = new List<OutputFile>
        {
            new(main.Combine(ItemsFileName), stream => WriteLines(stream, levelName, [rootGroup], parentName: null)),
        };
        AddGroupFiles(files, main.Combine(RootGroupName), rootGroup, levelName, snapshot);
        return files;
    }

    private static void Validate(SimGroup rootGroup, Dictionary<SimGroup, SceneObject[]> snapshot)
    {
        // The game finds objects by name without regard to case, and group names are also folder names.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<SceneObject>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<SceneObject>();
        pending.Push(rootGroup);
        while (pending.Count > 0)
        {
            SceneObject current = pending.Pop();
            if (!seen.Add(current))
            {
                throw new ArgumentException($"The object with key '{current.Key}' appears more than once in the tree.", nameof(rootGroup));
            }

            if (!keys.Add(current.Key))
            {
                throw new ArgumentException($"The key '{current.Key}' is used more than once.", nameof(rootGroup));
            }

            if (current.Name is not null && !names.Add(current.Name))
            {
                throw new ArgumentException($"The name '{current.Name}' is used more than once (names ignore case).", nameof(rootGroup));
            }

            if (current is SimGroup group)
            {
                SceneObject[] children = [.. group.Children];
                snapshot[group] = children;
                for (int i = children.Length - 1; i >= 0; i--)
                {
                    pending.Push(children[i] ?? throw new ArgumentException($"Group '{group.Name}' has a null child.", nameof(rootGroup)));
                }
            }
        }
    }

    private static void AddGroupFiles(
        List<OutputFile> files, VirtualPath folder, SimGroup group, string levelName, Dictionary<SimGroup, SceneObject[]> snapshot)
    {
        SceneObject[] children = snapshot[group];
        string groupName = group.Name!;
        files.Add(new OutputFile(folder.Combine(ItemsFileName), stream => WriteLines(stream, levelName, children, groupName)));
        foreach (SceneObject child in children)
        {
            if (child is SimGroup childGroup)
            {
                AddGroupFiles(files, folder.Combine(childGroup.Name!), childGroup, levelName, snapshot);
            }
        }
    }

    private static void WriteLines(Stream stream, string levelName, IEnumerable<SceneObject> objects, string? parentName)
    {
        using var writer = new Utf8JsonWriter(stream, BeamNGJson.LineOptions);
        foreach (SceneObject sceneObject in objects)
        {
            WriteObject(writer, sceneObject, levelName, parentName);
            writer.Flush();
            stream.WriteByte((byte)'\n');
            writer.Reset();
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, SceneObject sceneObject, string levelName, string? parentName)
    {
        writer.WriteStartObject();
        if (sceneObject.Name is not null)
        {
            writer.WriteString("name", sceneObject.Name);
        }

        writer.WriteString("class", sceneObject.ClassName);
        writer.WriteString("persistentId", PersistentId.ForObject(levelName, sceneObject.Key));
        if (parentName is not null)
        {
            writer.WriteString("__parent", parentName);
        }

        if (sceneObject.ScenePosition is Vector3 position)
        {
            writer.WritePropertyName("position");
            BeamNGJson.ToJson(position).WriteTo(writer);
        }

        var fields = new SceneFields();
        sceneObject.AddFields(fields);
        foreach (KeyValuePair<string, JsonNode?> field in fields.Entries)
        {
            writer.WritePropertyName(field.Key);
            if (field.Value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                field.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}
