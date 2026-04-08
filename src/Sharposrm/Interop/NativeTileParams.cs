using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmTileParams</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// Uses <c>uint</c> for all fields (x, y, z), making the struct blittable
/// so LibraryImport can pass it by reference without copying.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTileParams
{
    public uint x;                       // tile x coordinate
    public uint y;                       // tile y coordinate
    public uint z;                       // zoom level (12-19)
}
