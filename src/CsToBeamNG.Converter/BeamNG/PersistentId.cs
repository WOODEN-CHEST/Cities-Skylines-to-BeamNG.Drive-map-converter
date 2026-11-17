using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CsToBeamNG.Converter.BeamNG;

/// <summary>
/// Creates the <c>persistentId</c>s of scene objects and materials: name-based UUIDs (version 5, RFC 9562), so the
/// same level and object key always give the same id.
/// </summary>
/// <remarks>
/// BeamNG.drive identifies objects by these UUIDs, for example in the editor. Random ids would make every
/// conversion differ from the last (agents/conventions.md, determinism).
/// </remarks>
public static class PersistentId
{
    /// <summary>The namespace of every id this tool creates. It was chosen at random once and must never change.</summary>
    public static readonly Guid Namespace = new("7f355653-9e13-40ad-9d43-4394bb6f9725");

    private const int VersionByte = 6;
    private const int VariantByte = 8;
    private const byte Version5 = 0x50;
    private const byte VariantRfc = 0x80;

    /// <summary>The id of an object in a level.</summary>
    /// <param name="levelName">The level's folder name.</param>
    /// <param name="key">A key that is unique within the level and stable across conversions of the same input.</param>
    /// <returns>The id in the form the game writes: lowercase, with hyphens.</returns>
    public static string ForObject(string levelName, string key)
    {
        BeamNGNames.ThrowIfNotGeneratedName(levelName, nameof(levelName));
        ArgumentNullException.ThrowIfNull(key);

        // Level names can't contain ':', so the first ':' separates the two parts unambiguously.
        return Format(CreateVersion5(Namespace, levelName + ":" + key));
    }

    /// <summary>A name-based UUID, version 5 (SHA-1), as RFC 9562 section 5.5 defines it.</summary>
    /// <param name="namespaceId">The namespace.</param>
    /// <param name="name">The name, hashed as UTF-8.</param>
    /// <returns>The UUID.</returns>
    public static Guid CreateVersion5(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] input = new byte[16 + nameBytes.Length];
        if (!namespaceId.TryWriteBytes(input, bigEndian: true, out _))
        {
            throw new InvalidOperationException("A GUID didn't fit into 16 bytes.");
        }

        nameBytes.CopyTo(input, 16);

#pragma warning disable CA5350 // SHA-1 is part of the UUID version 5 definition; no security depends on it.
        byte[] hash = SHA1.HashData(input);
#pragma warning restore CA5350

        hash[VersionByte] = (byte)((hash[VersionByte] & 0x0F) | Version5);
        hash[VariantByte] = (byte)((hash[VariantByte] & 0x3F) | VariantRfc);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>Formats an id as the game writes it, such as <c>3c675967-d586-4aac-a00d-e29ee7927753</c>.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The lowercase text with hyphens.</returns>
    public static string Format(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);
}
