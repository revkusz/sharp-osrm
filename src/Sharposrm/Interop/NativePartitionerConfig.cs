using System.Runtime.InteropServices;

namespace Sharposrm.Interop;

/// <summary>
/// Blittable struct matching the C bridge's <c>SharposrmPartitionerConfig</c> layout exactly.
/// Field order and types must correspond to the C struct in sharposrm_bridge.h.
/// <para>
/// The <c>max_cell_sizes</c> field is a fixed inline array of 4 <c>size_t</c> values.
/// On 64-bit platforms (where OSRM runs), <c>size_t</c> is 8 bytes, matching <c>ulong</c>.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativePartitionerConfig
{
    public IntPtr base_path;               // const char* — required: .osrm base file path

    public uint requested_num_threads;     // 0 = hardware concurrency
    public double balance;                 // default 1.2
    public double boundary_factor;         // default 0.25
    public UIntPtr num_optimizing_cuts;    // size_t — default 10
    public UIntPtr small_component_size;   // size_t — default 1000
    public fixed ulong max_cell_sizes[4];  // size_t[4] — 4-level partition cell sizes
}
