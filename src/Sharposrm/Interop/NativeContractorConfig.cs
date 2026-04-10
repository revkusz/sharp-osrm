using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmContractorConfig</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeContractorConfig
{
    public IntPtr base_path;                          // const char* — required: .osrm base file path

    public uint requested_num_threads;                // 0 = hardware concurrency

    // UpdaterConfig fields — nullable string arrays for dynamic speed updates
    public IntPtr segment_speed_lookup_paths;         // const char** — nullable array of file paths
    public int segment_speed_lookup_count;            // count (0 if array is null)
    public IntPtr turn_penalty_lookup_paths;          // const char** — nullable array of file paths
    public int turn_penalty_lookup_count;             // count (0 if array is null)

    public IntPtr tz_file_path;                       // const char* — nullable: path to time zone shapefile
    public long valid_now;                            // time_t: 0 = unset (parse-conditionals-from-now)
    public double log_edge_updates_factor;            // default 0.0
}
