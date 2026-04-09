using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm;

/// <summary>
/// Managed configuration for the OSRM engine, mirroring <c>osrm::EngineConfig</c>.
/// Convert to a native config via <see cref="ToNativeConfig"/> for interop calls.
/// </summary>
public class EngineConfig
{
    /// <summary>
    /// Path to the <c>.osrm</c> base file (required).
    /// Maps to <c>storage_config_path</c> in the native config.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Routing algorithm. Default is <see cref="Algorithm.CH"/> (Contraction Hierarchies).
    /// </summary>
    public Algorithm Algorithm { get; set; } = Algorithm.CH;

    /// <summary>
    /// Whether to use shared memory for data loading.
    /// Default is <c>false</c> (file-based loading).
    /// </summary>
    public bool UseSharedMemory { get; set; } = false;

    /// <summary>
    /// Whether to use memory-mapped file I/O.
    /// Default is <c>true</c>.
    /// </summary>
    public bool UseMmap { get; set; } = true;

    /// <summary>
    /// Optional memory file path. <c>null</c> if unused.
    /// </summary>
    public string? MemoryFile { get; set; }

    /// <summary>
    /// Optional dataset name. <c>null</c> if unused.
    /// </summary>
    public string? DatasetName { get; set; }

    // --- Service limits (-1 means unlimited) ---

    /// <summary>Maximum locations for the trip service. -1 = unlimited.</summary>
    public int MaxLocationsTrip { get; set; } = -1;

    /// <summary>Maximum locations for the viaroute service. -1 = unlimited.</summary>
    public int MaxLocationsViaroute { get; set; } = -1;

    /// <summary>Maximum locations for the distance table service. -1 = unlimited.</summary>
    public int MaxLocationsDistanceTable { get; set; } = -1;

    /// <summary>Maximum locations for the map matching service. -1 = unlimited.</summary>
    public int MaxLocationsMapMatching { get; set; } = -1;

    /// <summary>Maximum radius for map matching. -1 = unlimited.</summary>
    public double MaxRadiusMapMatching { get; set; } = -1.0;

    /// <summary>Maximum results for the nearest service. -1 = unlimited.</summary>
    public int MaxResultsNearest { get; set; } = -1;

    /// <summary>Default search radius. -1 = unlimited.</summary>
    public double DefaultRadius { get; set; } = -1.0;

    /// <summary>Maximum number of alternative routes. Default is 3.</summary>
    public int MaxAlternatives { get; set; } = 3;

    /// <summary>
    /// Maximum number of concurrent OSRM service calls allowed through the engine.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// Values below 1 are rejected at engine creation time.
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    // --- Feature dataset toggles ---

    /// <summary>
    /// Disable route steps in the response. Default is <c>false</c>.
    /// </summary>
    public bool DisableRouteSteps { get; set; } = false;

    /// <summary>
    /// Disable route geometry in the response. Default is <c>false</c>.
    /// </summary>
    public bool DisableRouteGeometry { get; set; } = false;

    /// <summary>
    /// Converts this managed config to a blittable native struct for interop.
    /// String fields are allocated via <c>Marshal.StringToHGlobalAnsi</c>.
    /// <para>
    /// <b>Caller owns the allocated strings.</b> Dispose the returned
    /// <see cref="NativeConfigScope"/> to free all unmanaged memory.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <see cref="NativeConfigScope"/> containing the native struct and
    /// owning the allocated string memory. Dispose when done.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <see cref="StoragePath"/> is null or empty.
    /// </exception>
    internal NativeConfigScope ToNativeConfig()
    {
        if (string.IsNullOrEmpty(StoragePath))
        {
            throw new ArgumentNullException(nameof(StoragePath), "StoragePath is required and must not be null or empty.");
        }

        var scope = new NativeConfigScope();

        uint disableMask = 0;
        if (DisableRouteSteps) disableMask |= 0x01;
        if (DisableRouteGeometry) disableMask |= 0x02;

        scope.Config = new NativeEngineConfig
        {
            storage_config_path = Marshal.StringToHGlobalAnsi(StoragePath),
            algorithm = (int)Algorithm,
            use_shared_memory = UseSharedMemory ? 1 : 0,
            use_mmap = UseMmap ? 1 : 0,
            memory_file = MemoryFile is not null ? Marshal.StringToHGlobalAnsi(MemoryFile) : IntPtr.Zero,
            dataset_name = DatasetName is not null ? Marshal.StringToHGlobalAnsi(DatasetName) : IntPtr.Zero,
            max_locations_trip = MaxLocationsTrip,
            max_locations_viaroute = MaxLocationsViaroute,
            max_locations_distance_table = MaxLocationsDistanceTable,
            max_locations_map_matching = MaxLocationsMapMatching,
            max_radius_map_matching = MaxRadiusMapMatching,
            max_results_nearest = MaxResultsNearest,
            default_radius = DefaultRadius,
            max_alternatives = MaxAlternatives,
            disable_feature_datasets = disableMask,
        };

        // Track allocations so they can be freed on Dispose
        scope.AddAllocation(scope.Config.storage_config_path);
        if (scope.Config.memory_file != IntPtr.Zero)
            scope.AddAllocation(scope.Config.memory_file);
        if (scope.Config.dataset_name != IntPtr.Zero)
            scope.AddAllocation(scope.Config.dataset_name);

        return scope;
    }
}

/// <summary>
/// Owning handle for a <see cref="NativeEngineConfig"/> and its allocated strings.
/// Dispose to free all unmanaged memory.
/// </summary>
internal sealed class NativeConfigScope : IDisposable
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    public NativeEngineConfig Config;

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
