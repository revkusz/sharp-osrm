using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmEngineConfig</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// Uses <c>IntPtr</c> for pointers and <c>int</c>/<c>double</c> for scalars,
/// making the struct blittable so LibraryImport can pass it by reference without copying.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeEngineConfig
{
    // --- Required ---
    public IntPtr storage_config_path;   // const char* — required

    // --- Algorithm ---
    public int algorithm;                // SharposrmAlgorithm (CH=0, MLD=1)

    // --- Memory options ---
    public int use_shared_memory;        // 0 or 1
    public int use_mmap;                 // 0 or 1

    // --- Service limits (-1 means unlimited) ---
    public int max_locations_trip;
    public int max_locations_viaroute;
    public int max_locations_distance_table;
    public int max_locations_map_matching;
    public double max_radius_map_matching;
    public int max_results_nearest;
    public double default_radius;
    public int max_alternatives;

    // --- Feature dataset disable bitmask ---
    public uint disable_feature_datasets; // combination of SharposrmFeatureDataset flags

    // --- Optional nullable strings (IntPtr.Zero = null) ---
    public IntPtr memory_file;           // const char* or nullptr
    public IntPtr dataset_name;          // const char* or nullptr
}
