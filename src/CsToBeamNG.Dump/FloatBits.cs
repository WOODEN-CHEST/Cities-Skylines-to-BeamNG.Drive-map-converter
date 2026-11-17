using System.Runtime.InteropServices;

namespace CsToBeamNG.Dump;

/// <summary>
/// Reinterprets a float's bits as an integer and back, without converting the value, so NaN payloads
/// survive too. .NET 3.5 lacks <c>BitConverter.SingleToInt32Bits</c>; this works on both targets and
/// doesn't allocate.
/// </summary>
internal static class FloatBits
{
    public static uint ToBits(float value) => new Overlay { Single = value }.Bits;

    public static float FromBits(uint bits) => new Overlay { Bits = bits }.Single;

    [StructLayout(LayoutKind.Explicit)]
    private struct Overlay
    {
        [FieldOffset(0)]
        public float Single;

        [FieldOffset(0)]
        public uint Bits;
    }
}
