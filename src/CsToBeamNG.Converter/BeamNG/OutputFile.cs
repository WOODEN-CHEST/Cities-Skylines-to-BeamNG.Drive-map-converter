using System;
using System.IO;

namespace CsToBeamNG.Converter.BeamNG;

/// <summary>A generated file at a virtual path. Its content is produced when it is written.</summary>
/// <remarks>
/// Writers return these instead of writing to disk themselves, so the installer decides where and how the
/// files are written, and tests can write them to memory. Writing the same file twice gives the same bytes.
/// </remarks>
public sealed class OutputFile
{
    private readonly Action<Stream> _write;

    /// <summary>Creates a file.</summary>
    /// <param name="path">Where the file goes in the virtual file system, such as <c>/levels/x/info.json</c>.</param>
    /// <param name="write">Writes the content to a stream, without closing it.</param>
    public OutputFile(VirtualPath path, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(write);
        if (path.Segments.Count == 0)
        {
            throw new ArgumentException("A file can't be the root.", nameof(path));
        }

        Path = path;
        _write = write;
    }

    /// <summary>Where the file goes in the virtual file system.</summary>
    public VirtualPath Path { get; }

    /// <summary>Writes the content to <paramref name="stream"/>, leaving it open.</summary>
    /// <param name="stream">The destination.</param>
    public void WriteTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _write(stream);
    }

    /// <summary>The content as bytes.</summary>
    /// <returns>A new array.</returns>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        _write(stream);
        return stream.ToArray();
    }

    /// <inheritdoc/>
    public override string ToString() => Path.Value;
}
