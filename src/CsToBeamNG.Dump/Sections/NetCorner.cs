using System;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// One corner of a network segment as the game computes it with <c>NetSegment.CalculateCorner</c>: where one edge of
/// the segment's surface meets its node, the edge's direction there, and whether the edge leaves the corner smoothly
/// (docs/dump-format.md, <c>net-segments</c>; docs/research/cs1-networks.md, Corners).
/// </summary>
public struct NetCorner : IEquatable<NetCorner>
{
    /// <summary>Creates a corner.</summary>
    /// <param name="position">The corner's position, in metres.</param>
    /// <param name="direction">The edge's direction at the corner, pointing away from the node into the segment.</param>
    /// <param name="smooth">The game's smoothness output for the corner.</param>
    public NetCorner(DumpVector3 position, DumpVector3 direction, bool smooth)
    {
        Position = position;
        Direction = direction;
        Smooth = smooth;
    }

    /// <summary>The corner's position, in metres.</summary>
    public DumpVector3 Position { get; }

    /// <summary>The edge's direction at the corner, pointing away from the node into the segment.</summary>
    public DumpVector3 Direction { get; }

    /// <summary>
    /// The game's smoothness output for the corner, which it passes on to <c>NetSegment.CalculateMiddlePoints</c>:
    /// whether the corner's node is a middle node, which continues one segment into the next.
    /// </summary>
    public bool Smooth { get; }

    /// <summary>Whether the position and the direction are finite.</summary>
    public bool IsFinite => Position.IsFinite && Direction.IsFinite;

    /// <summary>Compares two corners field by field.</summary>
    public static bool operator ==(NetCorner left, NetCorner right) => left.Equals(right);

    /// <summary>The opposite of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(NetCorner left, NetCorner right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(NetCorner other) => Position == other.Position && Direction == other.Direction && Smooth == other.Smooth;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NetCorner other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (((Position.GetHashCode() * 397) ^ Direction.GetHashCode()) * 397) ^ (Smooth ? 1 : 0);
        }
    }
}
