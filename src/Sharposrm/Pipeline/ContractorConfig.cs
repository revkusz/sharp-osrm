using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm.Pipeline;

/// <summary>
/// Managed configuration for the OSRM CH contraction pipeline stage.
/// Mirrors the fields of <c>SharposrmContractorConfig</c>.
/// </summary>
public sealed class ContractorConfig
{
    /// <summary>
    /// Path to the <c>.osrm</c> base file (required).
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of threads to use. 0 = hardware concurrency.
    /// </summary>
    public uint RequestedThreads { get; set; }

    /// <summary>
    /// Optional paths to segment speed CSV files for dynamic speed updates.
    /// <c>null</c> or empty means no segment speed updates.
    /// </summary>
    public string[]? SegmentSpeedFiles { get; set; }

    /// <summary>
    /// Optional paths to turn penalty CSV files for dynamic penalty updates.
    /// <c>null</c> or empty means no turn penalty updates.
    /// </summary>
    public string[]? TurnPenaltyFiles { get; set; }

    /// <summary>
    /// Converts this managed config to a blittable native struct for interop.
    /// Allocates unmanaged memory for string arrays (individual strings + pointer arrays).
    /// </summary>
    internal NativePipelineConfigScope<NativeContractorConfig> ToNative()
    {
        if (string.IsNullOrEmpty(BasePath))
            throw new ArgumentNullException(nameof(BasePath), "BasePath is required.");

        var scope = new NativePipelineConfigScope<NativeContractorConfig>();

        var (segSpeedPtr, segSpeedCount) = AllocateStringArray(scope, SegmentSpeedFiles);
        var (turnPenaltyPtr, turnPenaltyCount) = AllocateStringArray(scope, TurnPenaltyFiles);

        scope.Config = new NativeContractorConfig
        {
            base_path = Marshal.StringToHGlobalAnsi(BasePath),
            requested_num_threads = RequestedThreads,
            segment_speed_lookup_paths = segSpeedPtr,
            segment_speed_lookup_count = segSpeedCount,
            turn_penalty_lookup_paths = turnPenaltyPtr,
            turn_penalty_lookup_count = turnPenaltyCount,
        };

        scope.AddAllocation(scope.Config.base_path);

        return scope;
    }

    /// <summary>
    /// Allocates unmanaged memory for a string array: each string gets its own
    /// <c>Marshal.StringToHGlobalAnsi</c> allocation, plus an array of pointers
    /// allocated via <c>Marshal.AllocHGlobal</c>.
    /// </summary>
    private static (IntPtr arrayPtr, int count) AllocateStringArray(
        NativePipelineConfigScope<NativeContractorConfig> scope, string[]? strings)
    {
        if (strings is null || strings.Length == 0)
            return (IntPtr.Zero, 0);

        int count = strings.Length;
        IntPtr[] pointers = new IntPtr[count];

        for (int i = 0; i < count; i++)
        {
            pointers[i] = Marshal.StringToHGlobalAnsi(strings[i]);
            scope.AddAllocation(pointers[i]);
        }

        // Allocate the array of pointers itself
        int pointerArraySize = count * IntPtr.Size;
        IntPtr arrayPtr = Marshal.AllocHGlobal(pointerArraySize);
        Marshal.Copy(pointers, 0, arrayPtr, count);
        scope.AddAllocation(arrayPtr);

        return (arrayPtr, count);
    }
}
