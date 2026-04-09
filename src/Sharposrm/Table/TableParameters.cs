using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm.Table;

/// <summary>
/// Managed parameters for the OSRM Table (distance matrix) service.
/// Convert to a native params struct via <see cref="ToNative"/> for interop calls.
/// </summary>
public sealed class TableParameters
{
    /// <summary>
    /// Coordinates for the table query (longitude-first OSRM convention).
    /// Must contain at least 2 coordinate pairs.
    /// </summary>
    public required IReadOnlyList<(double Longitude, double Latitude)> Coordinates { get; init; }

    /// <summary>
    /// Indices into <see cref="Coordinates"/> to use as sources for the table.
    /// <c>null</c> means use all coordinates as sources (OSRM default).
    /// </summary>
    public IReadOnlyList<int>? Sources { get; init; }

    /// <summary>
    /// Indices into <see cref="Coordinates"/> to use as destinations for the table.
    /// <c>null</c> means use all coordinates as destinations (OSRM default).
    /// </summary>
    public IReadOnlyList<int>? Destinations { get; init; }

    /// <summary>
    /// Fallback speed in m/s used when no route is found between source and destination.
    /// Defaults to <see cref="double.MaxValue"/> (disabled / OSRM's INVALID_FALLBACK_SPEED).
    /// Must be greater than 0 when explicitly set to a finite value.
    /// </summary>
    public double FallbackSpeed { get; init; } = double.MaxValue;

    /// <summary>
    /// Coordinate type used for fallback speed computation.
    /// Default is <see cref="FallbackCoordinateType.Input"/>.
    /// </summary>
    public FallbackCoordinateType FallbackCoordinateType { get; init; } = FallbackCoordinateType.Input;

    /// <summary>
    /// Which annotations to include in the response.
    /// Default is <see cref="TableAnnotationsType.Duration"/>.
    /// </summary>
    public TableAnnotationsType AnnotationsType { get; init; } = TableAnnotationsType.Duration;

    /// <summary>
    /// Scale factor to apply to table values. Default is 1.0.
    /// </summary>
    public double ScaleFactor { get; init; } = 1.0;

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
    /// A <see cref="NativeTableParamsScope"/> containing the native struct and
    /// owning the allocated memory. Dispose when done (after the native call returns).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="Coordinates"/> has fewer than 2 entries.
    /// </exception>
    internal NativeTableParamsScope ToNative()
    {
        if (Coordinates is null || Coordinates.Count < 2)
        {
            throw new ArgumentException(
                "At least 2 coordinates are required for a table query.",
                nameof(Coordinates));
        }

        var scope = new NativeTableParamsScope();
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

        // Allocate sources array (int → size_t, so each element is IntPtr.Size bytes)
        IntPtr sourcesPtr = IntPtr.Zero;
        int sourceCount = 0;
        if (Sources is not null && Sources.Count > 0)
        {
            sourceCount = Sources.Count;
            int sourcesBytes = IntPtr.Size * sourceCount;
            sourcesPtr = Marshal.AllocHGlobal(sourcesBytes);

            for (int i = 0; i < sourceCount; i++)
            {
                IntPtr value = new(Sources[i]);
                if (IntPtr.Size == 8)
                    Marshal.WriteInt64(sourcesPtr, i * IntPtr.Size, value.ToInt64());
                else
                    Marshal.WriteInt32(sourcesPtr, i * IntPtr.Size, value.ToInt32());
            }

            scope.AddAllocation(sourcesPtr);
        }

        // Allocate destinations array (int → size_t)
        IntPtr destinationsPtr = IntPtr.Zero;
        int destCount = 0;
        if (Destinations is not null && Destinations.Count > 0)
        {
            destCount = Destinations.Count;
            int destsBytes = IntPtr.Size * destCount;
            destinationsPtr = Marshal.AllocHGlobal(destsBytes);

            for (int i = 0; i < destCount; i++)
            {
                IntPtr value = new(Destinations[i]);
                if (IntPtr.Size == 8)
                    Marshal.WriteInt64(destinationsPtr, i * IntPtr.Size, value.ToInt64());
                else
                    Marshal.WriteInt32(destinationsPtr, i * IntPtr.Size, value.ToInt32());
            }

            scope.AddAllocation(destinationsPtr);
        }

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

        scope.Params = new NativeTableParams
        {
            longitudes = longitudesPtr,
            latitudes = latitudesPtr,
            coordinate_count = count,
            sources = sourcesPtr,
            source_count = sourceCount,
            destinations = destinationsPtr,
            destination_count = destCount,
            fallback_speed = FallbackSpeed,
            fallback_coordinate_type = (int)FallbackCoordinateType,
            annotations_type = (int)AnnotationsType,
            scale_factor = ScaleFactor,
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
/// Owning handle for a <see cref="NativeTableParams"/> and its allocated arrays.
/// Dispose to free all unmanaged memory. Must not be disposed until the native call returns.
/// </summary>
internal sealed class NativeTableParamsScope : IDisposable
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    public NativeTableParams Params;

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
