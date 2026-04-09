using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmTripParams</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// <para>
/// Contains all Route fields (flat layout, no inheritance in C) plus trip-specific fields.
/// This duplication is intentional — the C struct mirrors the same flat layout.
/// </para>
/// Uses <c>IntPtr</c> for pointers and <c>int</c>/<c>uint</c> for scalars,
/// making the struct blittable so LibraryImport can pass it by reference without copying.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTripParams
{
    // ── Route base fields ────────────────────────────────────────────────

    // --- Coordinates (longitude-first OSRM convention) ---
    public IntPtr longitudes;            // const double* — required
    public IntPtr latitudes;             // const double* — required
    public int coordinate_count;         // number of coordinate pairs (min 2)

    // --- Route options ---
    public int steps;                    // 0 or 1: return route steps per leg
    public int alternatives;             // 0 or 1: try to find alternative routes
    public uint number_of_alternatives;  // max number of alternative routes

    // --- Annotations ---
    public int annotations;              // 0 or 1: enable annotations
    public uint annotations_type;        // AnnotationsType bitmask

    // --- Geometry ---
    public int geometries_type;          // GeometriesType (Polyline=0, Polyline6=1, GeoJSON=2)
    public int overview_type;            // OverviewType (Simplified=0, Full=1, False=2)

    // --- Continue straight ---
    public int continue_straight;        // -1 = not set, 0 = false, 1 = true

    // --- Optional radiuses (IntPtr.Zero = null, OSRM defaults apply) ---
    public IntPtr radiuses;              // const double* or nullptr
    public int radius_count;             // number of radius values (0 if radiuses is null)

    // --- Optional bearings ---
    public IntPtr bearings;              // const short* or nullptr
    public int bearing_count;            // number of bearing entries (0 if bearings is null)

    // --- Optional hints ---
    public IntPtr hints;                 // const char** or nullptr
    public int hint_count;               // number of hint entries (0 if hints is null)

    // --- Response options ---
    public int generate_hints;           // 0 or 1: add hints to response
    public int skip_waypoints;           // 0 or 1: remove waypoints array from response

    // --- Approaches (nullable byte array, one byte per coordinate, 0xFF = not set) ---
    public IntPtr approaches;            // const char* (byte array) or IntPtr.Zero
    public int approach_count;           // 0 if approaches is null

    // --- Exclude (nullable array of road class name strings) ---
    public IntPtr exclude;               // const char** or IntPtr.Zero
    public int exclude_count;            // 0 if exclude is null

    // --- Snapping ---
    public int snapping;                 // SnappingType (Default=0, Any=1)

    // ── Trip-specific fields ─────────────────────────────────────────────

    public int source_type;              // SourceType (Any=0, First=1)
    public int destination_type;         // DestinationType (Any=0, Last=1)
    public int roundtrip;                // 0 or 1, default 1
}
