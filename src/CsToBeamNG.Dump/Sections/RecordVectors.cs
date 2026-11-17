using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Writes and reads vectors and curves as consecutive <c>f32</c> fields of a record (docs/dump-format.md, Sections).</summary>
internal static class RecordVectors
{
    /// <summary>Writes the fields X, Y and Z.</summary>
    public static void Write(RecordFileWriter records, DumpVector3 vector)
    {
        records.WriteSingle(vector.X);
        records.WriteSingle(vector.Y);
        records.WriteSingle(vector.Z);
    }

    /// <summary>Writes the twelve fields of the four control points, A to D.</summary>
    public static void Write(RecordFileWriter records, DumpBezier3 curve)
    {
        Write(records, curve.A);
        Write(records, curve.B);
        Write(records, curve.C);
        Write(records, curve.D);
    }

    /// <summary>Reads the fields X, Y and Z.</summary>
    public static DumpVector3 ReadVector(RecordFileReader records) =>
        new(records.ReadSingle(), records.ReadSingle(), records.ReadSingle());

    /// <summary>Reads the twelve fields of the four control points, A to D.</summary>
    public static DumpBezier3 ReadBezier(RecordFileReader records) =>
        new(ReadVector(records), ReadVector(records), ReadVector(records), ReadVector(records));
}
