using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm.Nearest;

/// <summary>
/// Managed parameters for the OSRM Nearest service.
/// Convert to a native params struct via <see cref="ToNative"/> for interop calls.
/// </summary>
public sealed class NearestParameters
{
    /// <summary>
    /// Coordinates for the nearest query (longitude-first OSRM convention).
    /// Must contain at least 1 coordinate pair.
    /// </summary>
    public required IReadOnlyList<(double Longitude, double Latitude)> Coordinates { get; init; }

    /// <summary>
    /// Number of nearest results to return. Default is 1.
    /// </summary>
    public uint NumberOfResults { get; init; } = 1;

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

    /// <summary>
    /// Optional per-coordinate bearing constraints to limit the road segment search direction.
    /// Each entry is a <see cref="Bearing"/> with a clockwise angle (0–359) and deviation range (0–180).
    /// Use <c>null</c> for individual coordinates that should not have a bearing constraint.
    /// If provided, the count must match <see cref="Coordinates"/>.
    /// </summary>
    public IReadOnlyList<Bearing?>? Bearings { get; init; }

    /// <summary>
    /// Optional per-coordinate hint strings for faster subsequent queries.
    /// If provided, the count must match <see cref="Coordinates"/>.
    /// Individual entries can be <c>null</c> for coordinates without hints.
    /// </summary>
    public IReadOnlyList<string?>? Hints { get; init; }

    /// <summary>
    /// Converts these managed parameters to a blittable native struct for interop.
    /// </summary>
    /// <returns>
    /// A <see cref="NativeNearestParamsScope"/> containing the native struct and
    /// owning the allocated memory. Dispose when done (after the native call returns).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="Coordinates"/> has fewer than 1 entry.
    /// </exception>
    internal NativeNearestParamsScope ToNative()
    {
        if (Coordinates is null || Coordinates.Count < 1)
        {
            throw new ArgumentException(
                "At least 1 coordinate is required for a nearest query.",
                nameof(Coordinates));
        }

        var scope = new NativeNearestParamsScope();
        int count = Coordinates.Count;

        // Allocate coordinate arrays
        int coordBytes = count * sizeof(double);
        IntPtr longitudesPtr = Marshal.AllocHGlobal(coordBytes);
        IntPtr latitudesPtr = Marshal.AllocHGlobal(coordBytes);

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

        // Optionally allocate bearings array (interleaved shorts: [bearing, range, bearing, range, ...])
        IntPtr bearingsPtr = IntPtr.Zero;
        int bearingCount = 0;
        if (Bearings is not null && Bearings.Count > 0)
        {
            bearingCount = Bearings.Count;
            int bearingBytes = bearingCount * 2 * sizeof(short);
            bearingsPtr = Marshal.AllocHGlobal(bearingBytes);

            for (int i = 0; i < bearingCount; i++)
            {
                var b = Bearings[i];
                short value = b.HasValue && b.Value.Value >= 0 ? b.Value.Value : (short)-1;
                short deviation = b.HasValue && b.Value.Value >= 0 ? b.Value.Deviation : (short)-1;
                Marshal.WriteInt16(bearingsPtr, i * 2 * sizeof(short), value);
                Marshal.WriteInt16(bearingsPtr, i * 2 * sizeof(short) + sizeof(short), deviation);
            }

            scope.AddAllocation(bearingsPtr);
        }

        // Optionally allocate hints array (array of pointers to null-terminated ANSI strings)
        IntPtr hintsPtr = IntPtr.Zero;
        int hintCount = 0;
        if (Hints is not null && Hints.Count > 0)
        {
            hintCount = Hints.Count;
            int pointerBytes = hintCount * IntPtr.Size;
            hintsPtr = Marshal.AllocHGlobal(pointerBytes);

            for (int i = 0; i < hintCount; i++)
            {
                IntPtr stringPtr = Hints[i] is not null
                    ? Marshal.StringToHGlobalAnsi(Hints[i])
                    : IntPtr.Zero;
                Marshal.WriteIntPtr(hintsPtr, i * IntPtr.Size, stringPtr);
                if (stringPtr != IntPtr.Zero)
                    scope.AddAllocation(stringPtr);
            }

            scope.AddAllocation(hintsPtr);
        }

        scope.Params = new NativeNearestParams
        {
            longitudes = longitudesPtr,
            latitudes = latitudesPtr,
            coordinate_count = count,
            number_of_results = NumberOfResults,
            radiuses = radiusesPtr,
            radius_count = radiusCount,
            bearings = bearingsPtr,
            bearing_count = bearingCount,
            hints = hintsPtr,
            hint_count = hintCount,
            generate_hints = GenerateHints ? 1 : 0,
            skip_waypoints = SkipWaypoints ? 1 : 0,
        };

        return scope;
    }
}

/// <summary>
/// Owning handle for a <see cref="NativeNearestParams"/> and its allocated arrays.
/// Dispose to free all unmanaged memory. Must not be disposed until the native call returns.
/// </summary>
internal sealed class NativeNearestParamsScope : IDisposable
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    public NativeNearestParams Params;

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
