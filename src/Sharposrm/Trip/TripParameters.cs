using System.Runtime.InteropServices;
using Sharposrm.Interop;
using Sharposrm.Route;

namespace Sharposrm.Trip;

/// <summary>
/// Managed parameters for the OSRM Trip service.
/// Extends Route-like fields with trip-specific <see cref="Source"/>,
/// <see cref="Destination"/>, and <see cref="Roundtrip"/> options.
/// Convert to a native params struct via <see cref="ToNative"/> for interop calls.
/// </summary>
public sealed class TripParameters
{
    /// <summary>
    /// Coordinates for the trip query (longitude-first OSRM convention).
    /// Must contain at least 2 coordinate pairs.
    /// </summary>
    public required IReadOnlyList<(double Longitude, double Latitude)> Coordinates { get; init; }

    /// <summary>
    /// Return route steps per leg. Default is <c>false</c>.
    /// </summary>
    public bool Steps { get; init; } = false;

    /// <summary>
    /// Try to find alternative trips. Default is <c>false</c>.
    /// </summary>
    public bool Alternatives { get; init; } = false;

    /// <summary>
    /// Maximum number of alternative trips to compute. Default is 0 (OSRM default applies).
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
    /// Optional per-coordinate approach type to control which side of the road to use.
    /// <c>null</c> entries mean "not set" (OSRM default applies for that coordinate).
    /// If provided, the count should match <see cref="Coordinates"/>.
    /// </summary>
    public IReadOnlyList<ApproachType?>? Approaches { get; init; }

    /// <summary>
    /// Optional list of road class names to exclude from routing (e.g. "motorway", "ferry").
    /// </summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>
    /// Controls how coordinates are snapped to the road network.
    /// Default is <see cref="SnappingType.Default"/>.
    /// </summary>
    public SnappingType Snapping { get; init; } = SnappingType.Default;

    // ── Trip-specific fields ─────────────────────────────────────────────

    /// <summary>
    /// Source type — controls which coordinate can be the start of the trip.
    /// Default is <see cref="SourceType.Any"/>.
    /// </summary>
    public SourceType Source { get; init; } = SourceType.Any;

    /// <summary>
    /// Destination type — controls which coordinate can be the end of the trip.
    /// Default is <see cref="DestinationType.Any"/>.
    /// </summary>
    public DestinationType Destination { get; init; } = DestinationType.Any;

    /// <summary>
    /// Whether the trip should return to the origin (round-trip).
    /// Default is <c>true</c>. When <c>false</c>, the last coordinate is the destination.
    /// </summary>
    public bool Roundtrip { get; init; } = true;

    /// <summary>
    /// Converts these managed parameters to a blittable native struct for interop.
    /// Coordinate arrays are copied into unmanaged memory via <c>Marshal.AllocHGlobal</c>.
    /// <para>
    /// <b>Caller owns the allocated memory.</b> Dispose the returned
    /// <see cref="NativeTripParamsScope"/> to free all unmanaged allocations.
    /// The scope must not be disposed until the native call returns.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <see cref="NativeTripParamsScope"/> containing the native struct and
    /// owning the allocated memory. Dispose when done (after the native call returns).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="Coordinates"/> has fewer than 2 entries.
    /// </exception>
    internal NativeTripParamsScope ToNative()
    {
        if (Coordinates is null || Coordinates.Count < 2)
        {
            throw new ArgumentException(
                "At least 2 coordinates are required for a trip query.",
                nameof(Coordinates));
        }

        var scope = new NativeTripParamsScope();
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

        // Map nullable bool to -1/0/1 int
        int continueStraight = ContinueStraight switch
        {
            null => -1,
            false => 0,
            true => 1,
        };

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

        // Optionally allocate approaches array (byte array, one byte per coordinate, 0xFF = not set)
        IntPtr approachesPtr = IntPtr.Zero;
        int approachCount = 0;
        if (Approaches is not null && Approaches.Count > 0)
        {
            approachCount = Approaches.Count;
            approachesPtr = Marshal.AllocHGlobal(approachCount);
            for (int i = 0; i < approachCount; i++)
            {
                byte val = Approaches[i].HasValue ? (byte)Approaches[i]!.Value : (byte)0xFF;
                Marshal.WriteByte(approachesPtr, i, val);
            }
            scope.AddAllocation(approachesPtr);
        }

        // Optionally allocate exclude array (string array — non-null entries only)
        IntPtr excludePtr = IntPtr.Zero;
        int excludeCount = 0;
        if (Exclude is not null && Exclude.Count > 0)
        {
            excludeCount = Exclude.Count;
            int pointerBytes = excludeCount * IntPtr.Size;
            excludePtr = Marshal.AllocHGlobal(pointerBytes);
            for (int i = 0; i < excludeCount; i++)
            {
                IntPtr stringPtr = Marshal.StringToHGlobalAnsi(Exclude[i]);
                Marshal.WriteIntPtr(excludePtr, i * IntPtr.Size, stringPtr);
                scope.AddAllocation(stringPtr);
            }
            scope.AddAllocation(excludePtr);
        }

        scope.Params = new NativeTripParams
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
            bearings = bearingsPtr,
            bearing_count = bearingCount,
            hints = hintsPtr,
            hint_count = hintCount,
            generate_hints = GenerateHints ? 1 : 0,
            skip_waypoints = SkipWaypoints ? 1 : 0,
            approaches = approachesPtr,
            approach_count = approachCount,
            exclude = excludePtr,
            exclude_count = excludeCount,
            snapping = (int)Snapping,
            // Trip-specific fields
            source_type = (int)Source,
            destination_type = (int)Destination,
            roundtrip = Roundtrip ? 1 : 0,
        };

        return scope;
    }
}

/// <summary>
/// Owning handle for a <see cref="NativeTripParams"/> and its allocated coordinate/radius arrays.
/// Dispose to free all unmanaged memory. Must not be disposed until the native call returns.
/// </summary>
internal sealed class NativeTripParamsScope : IDisposable
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    public NativeTripParams Params;

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
