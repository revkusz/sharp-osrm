using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm.Pipeline;

/// <summary>
/// Managed configuration for the OSRM graph partitioning pipeline stage.
/// Mirrors the fields of <c>SharposrmPartitionerConfig</c>.
/// </summary>
public sealed class PartitionerConfig
{
    /// <summary>
    /// Path to the <c>.osrm</c> base file produced by the extract stage (required).
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of threads to use. 0 = hardware concurrency.
    /// </summary>
    public uint RequestedThreads { get; set; }

    /// <summary>
    /// Balance factor for partitioning. Default is 1.2.
    /// </summary>
    public double Balance { get; set; } = 1.2;

    /// <summary>
    /// Boundary factor for partitioning. Default is 0.25.
    /// </summary>
    public double BoundaryFactor { get; set; } = 0.25;

    /// <summary>
    /// Number of optimizing cuts. Default is 10.
    /// </summary>
    public ulong NumOptimizingCuts { get; set; } = 10;

    /// <summary>
    /// Small component size threshold. Default is 1000.
    /// </summary>
    public ulong SmallComponentSize { get; set; } = 1000;

    /// <summary>
    /// Cell sizes for the 4-level MLD partition.
    /// Must contain exactly 4 elements. Defaults are [64, 32, 16, 8] but
    /// OSRM uses different defaults depending on build configuration.
    /// </summary>
    public ulong[] MaxCellSizes { get; set; } = [64, 32, 16, 8];

    /// <summary>
    /// Converts this managed config to a blittable native struct for interop.
    /// </summary>
    internal unsafe NativePipelineConfigScope<NativePartitionerConfig> ToNative()
    {
        if (string.IsNullOrEmpty(BasePath))
            throw new ArgumentNullException(nameof(BasePath), "BasePath is required.");

        if (MaxCellSizes is null || MaxCellSizes.Length != 4)
            throw new ArgumentException("MaxCellSizes must contain exactly 4 elements.", nameof(MaxCellSizes));

        var scope = new NativePipelineConfigScope<NativePartitionerConfig>();

        var config = new NativePartitionerConfig
        {
            base_path = Marshal.StringToHGlobalAnsi(BasePath),
            requested_num_threads = RequestedThreads,
            balance = Balance,
            boundary_factor = BoundaryFactor,
            num_optimizing_cuts = new UIntPtr(NumOptimizingCuts),
            small_component_size = new UIntPtr(SmallComponentSize),
        };

        for (int i = 0; i < 4; i++)
        {
            config.max_cell_sizes[i] = MaxCellSizes[i];
        }

        scope.Config = config;
        scope.AddAllocation(scope.Config.base_path);

        return scope;
    }
}
