using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmTableParams</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// Uses <c>IntPtr</c> for pointers and <c>int</c>/<c>uint</c> for scalars,
/// making the struct blittable so LibraryImport can pass it by reference without copying.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTableParams
{
    // --- Coordinates (longitude-first OSRM convention) ---
    public IntPtr longitudes;            // const double* — required
    public IntPtr latitudes;             // const double* — required
    public int coordinate_count;         // number of coordinate pairs (min 2)

    // --- Source indices (IntPtr.Zero = null, use all coordinates) ---
    public IntPtr sources;               // const size_t* or nullptr
    public int source_count;             // number of source indices (0 if sources is null)

    // --- Destination indices (IntPtr.Zero = null, use all coordinates) ---
    public IntPtr destinations;          // const size_t* or nullptr
    public int destination_count;        // number of destination indices (0 if destinations is null)

    // --- Fallback options ---
    public double fallback_speed;        // fallback speed in m/s (> 0), use double.NaN to disable
    public int fallback_coordinate_type; // FallbackCoordinateType (Input=0, Snapped=1)

    // --- Annotations ---
    public int annotations_type;         // TableAnnotationsType bitmask (Duration=0x01, Distance=0x02)

    // --- Scale factor ---
    public double scale_factor;          // scale factor for table values (> 0, default 1)

    // --- Optional radiuses (IntPtr.Zero = null, OSRM defaults apply) ---
    public IntPtr radiuses;              // const double* or nullptr
    public int radius_count;             // number of radius values (0 if radiuses is null)

    // --- Response options ---
    public int generate_hints;           // 0 or 1: add hints to response
    public int skip_waypoints;           // 0 or 1: remove waypoints from response
}
