using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmExtractorConfig</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeExtractorConfig
{
    public IntPtr input_path;              // const char* — required: .osm.pbf / .osm.xml
    public IntPtr profile_path;            // const char* — required: .lua profile script
    public IntPtr output_path;             // const char* or nullptr — nullable base path for outputs

    public uint requested_num_threads;     // 0 = hardware concurrency
    public uint small_component_size;      // default 1000
    public int use_metadata;               // 0 or 1
    public int parse_conditionals;         // 0 or 1
    public int use_locations_cache;        // 0 or 1, default 1
    public int dump_nbg_graph;             // 0 or 1
}
