using System.Runtime.InteropServices;
using Sharposrm.Interop;
using Sharposrm.Route;

namespace Sharposrm.Match;

/// <summary>
/// Managed parameters for the OSRM Map Matching service.
/// Extends Route-like fields with match-specific <see cref="Timestamps"/>,
/// <see cref="Gaps"/>, and <see cref="Tidy"/> options.
/// Convert to a native params struct via <see cref="ToNative"/> for interop calls.
/// </summary>
public sealed class MatchParameters
{
    /// <summary>
    /// Coordinates for the match query (longitude-first OSRM convention).
    /// Must contain at least 2 coordinate pairs.
    /// </summary>
    public required IReadOnlyList<(double Longitude, double Latitude)> Coordinates { get; init; }

    /// <summary>
    /// Return route steps per leg. Default is <c>false</c>.
    /// </summary>
    public bool Steps { get; init; } = false;

    /// <summary>
    /// Try to find alternative routes. Default is <c>false</c>.
    /// </summary>
    public bool Alternatives { get; init; } = false;

    /// <summary>
    /// Maximum number of alternative routes to compute. Default is 0 (OSRM default applies).
    /// </summary>
    public uint NumberOfAlternatives { get; init; } = 0;

    /// <summary>
    /// Enable annotations in the response. Default is <c>false</c>.
    /// </summary>
    public bool Annotations { get; init; } = false;

    /// <summary>
    /// Which annotation values to include when <see cref="Annotations"/> is <c>true</c>.
    /// Default is <see cref="AnnotationsType.All"/>.
    /// </summary>
    public AnnotationsType AnnotationTypes { get; init; } = AnnotationsType.All;

    /// <summary>
    /// Geometry encoding format for the response. Default is <see cref="GeometriesType.Polyline"/>.
    /// </summary>
    public GeometriesType Geometries { get; init; } = GeometriesType.Polyline;

    /// <summary>
    /// Overview geometry simplification level. Default is <see cref="OverviewType.Simplified"/>.
    /// </summary>
    public OverviewType Overview { get; init; } = OverviewType.Simplified;

    /// <summary>
    /// Force the route to keep going straight at waypoints.
    /// <c>null</c> means unset (OSRM default applies).
    /// </summary>
    public bool? ContinueStraight { get; init; }

    /// <summary>
    /// Optional search radius per coordinate in meters.
    /// <c>null</c> means OSRM defaults apply (unlimited).
    /// If provided, the count must match <see cref="Coordinates"/>.
    /// </summary>
    public IReadOnlyList<double>? Radiuses { get; init; }

    /// <summary>
    /// Add hints to the response that can be used for faster subsequent queries. Default is <c>true</c>.
    /// </summary>
    public bool GenerateHints { get; init; } = true;

    /// <summary>
    /// Remove the waypoints array from the response. Default is <c>false</c>.
    /// </summary>
    public bool SkipWaypoints { get; init; } = false;

    // ── Match-specific fields ─────────────────────────────────────────────

    /// <summary>
    /// Optional per-coordinate Unix timestamps.
    /// If provided, the count must match <see cref="Coordinates"/>.
    /// <c>null</c> means no timestamps (OSRM default applies).
    /// </summary>
    public IReadOnlyList<uint>? Timestamps { get; init; }

    /// <summary>
    /// How gaps in the trace are handled. Default is <see cref="GapsType.Split"/>.
    /// </summary>
    public GapsType Gaps { get; init; } = GapsType.Split;

    /// <summary>
    /// Whether to tidy the input coordinates (remove duplicates, clusters).
    /// Default is <c>false</c>.
    /// </summary>
    public bool Tidy { get; init; } = false;

    /// <summary>
    /// Converts these managed parameters to a blittable native struct for interop.
    /// Coordinate and timestamp arrays are copied into unmanaged memory via <c>Marshal.AllocHGlobal</c>.
    /// <para>
    /// <b>Caller owns the allocated memory.</b> Dispose the returned
    /// <see cref="NativeMatchParamsScope"/> to free all unmanaged allocations.
    /// The scope must not be disposed until the native call returns.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <see cref="NativeMatchParamsScope"/> containing the native struct and
    /// owning the allocated memory. Dispose when done (after the native call returns).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="Coordinates"/> has fewer than 2 entries, or
    /// when <see cref="Timestamps"/> count does not match coordinate count.
    /// </exception>
    internal NativeMatchParamsScope ToNative()
    {
        if (Coordinates is null || Coordinates.Count < 2)
        {
            throw new ArgumentException(
                "At least 2 coordinates are required for a match query.",
                nameof(Coordinates));
        }

        if (Timestamps is not null && Timestamps.Count != Coordinates.Count)
        {
            throw new ArgumentException(
                $"Timestamps count ({Timestamps.Count}) must match coordinates count ({Coordinates.Count}).",
                nameof(Timestamps));
        }

        var scope = new NativeMatchParamsScope();
        int count = Coordinates.Count;

        // Allocate coordinate arrays
        int coordBytes = count * sizeof(double);
        IntPtr longitudesPtr = Marshal.AllocHGlobal(coordBytes);
        IntPtr latitudesPtr = Marshal.AllocHGlobal(coordBytes);

        // Copy coordinate values into unmanaged arrays
        for (int i = 0; i < count; i++)
        {
            var (lon, lat) = Coordinates[i];
            Marshal.WriteInt64(longitudesPtr, i * sizeof(double), BitConverter.DoubleToInt64Bits(lon));
            Marshal.WriteInt64(latitudesPtr, i * sizeof(double), BitConverter.DoubleToInt64Bits(lat));
        }

        scope.AddAllocation(longitudesPtr);
        scope.AddAllocation(latitudesPtr);

        // Optionally allocate radiuses array
        IntPtr radiusesPtr = IntPtr.Zero;
        int radiusCount = 0;
        if (Radiuses is not null && Radiuses.Count > 0)
        {
            radiusCount = Radiuses.Count;
            int radiusBytes = radiusCount * sizeof(double);
            radiusesPtr = Marshal.AllocHGlobal(radiusBytes);

            for (int i = 0; i < radiusCount; i++)
            {
                Marshal.WriteInt64(radiusesPtr, i * sizeof(double), BitConverter.DoubleToInt64Bits(Radiuses[i]));
            }

            scope.AddAllocation(radiusesPtr);
        }

        // Optionally allocate timestamps array
        IntPtr timestampsPtr = IntPtr.Zero;
        int timestampCount = 0;
        if (Timestamps is not null && Timestamps.Count > 0)
        {
            timestampCount = Timestamps.Count;
            int tsBytes = timestampCount * sizeof(uint);
            timestampsPtr = Marshal.AllocHGlobal(tsBytes);

            for (int i = 0; i < timestampCount; i++)
            {
                // uint is 4 bytes
                Marshal.WriteInt32(timestampsPtr, i * sizeof(int), (int)Timestamps[i]);
            }

            scope.AddAllocation(timestampsPtr);
        }

        // Map nullable bool to -1/0/1 int
        int continueStraight = ContinueStraight switch
        {
            null => -1,
            false => 0,
            true => 1,
        };

        scope.Params = new NativeMatchParams
        {
            longitudes = longitudesPtr,
            latitudes = latitudesPtr,
            coordinate_count = count,
            steps = Steps ? 1 : 0,
            alternatives = Alternatives ? 1 : 0,
            number_of_alternatives = NumberOfAlternatives,
            annotations = Annotations ? 1 : 0,
            annotations_type = (uint)AnnotationTypes,
            geometries_type = (int)Geometries,
            overview_type = (int)Overview,
            continue_straight = continueStraight,
            radiuses = radiusesPtr,
            radius_count = radiusCount,
            generate_hints = GenerateHints ? 1 : 0,
            skip_waypoints = SkipWaypoints ? 1 : 0,
            // Match-specific fields
            timestamps = timestampsPtr,
            timestamp_count = timestampCount,
            gaps = (int)Gaps,
            tidy = Tidy ? 1 : 0,
        };

        return scope;
    }
}

/// <summary>
/// Owning handle for a <see cref="NativeMatchParams"/> and its allocated coordinate/radius/timestamp arrays.
/// Dispose to free all unmanaged memory. Must not be disposed until the native call returns.
/// </summary>
internal sealed class NativeMatchParamsScope : IDisposable
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    public NativeMatchParams Params;

    internal void AddAllocation(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
            _allocations.Add(ptr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var ptr in _allocations)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _allocations.Clear();
        _disposed = true;
    }
}
