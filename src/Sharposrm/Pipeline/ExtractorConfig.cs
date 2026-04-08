using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm.Pipeline;

/// <summary>
/// Managed configuration for the OSRM extraction pipeline stage.
/// Mirrors the fields of <c>SharposrmExtractorConfig</c>.
/// </summary>
public sealed class ExtractorConfig
{
    /// <summary>
    /// Path to the input <c>.osm.pbf</c> or <c>.osm.xml</c> file (required).
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the Lua profile script, e.g. <c>car.lua</c> (required).
    /// </summary>
    public string ProfilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional base path for output files. If <c>null</c>, derived from <see cref="InputPath"/>.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Number of threads to use. 0 = hardware concurrency.
    /// </summary>
    public uint RequestedThreads { get; set; }

    /// <summary>
    /// Small component size threshold. Default is 1000.
    /// </summary>
    public uint SmallComponentSize { get; set; } = 1000;

    /// <summary>
    /// Whether to extract metadata from the input file.
    /// </summary>
    public bool UseMetadata { get; set; }

    /// <summary>
    /// Whether to parse conditional restrictions.
    /// </summary>
    public bool ParseConditionals { get; set; }

    /// <summary>
    /// Whether to use the locations cache. Default is <c>true</c>.
    /// </summary>
    public bool UseLocationsCache { get; set; } = true;

    /// <summary>
    /// Whether to dump the NBG graph.
    /// </summary>
    public bool DumpNbgGraph { get; set; }

    /// <summary>
    /// Converts this managed config to a blittable native struct for interop.
    /// String fields are allocated via <c>Marshal.StringToHGlobalAnsi</c>.
    /// <para>
    /// <b>Caller owns the allocated memory.</b> Dispose the returned
    /// <see cref="NativePipelineConfigScope{NativeExtractorConfig}"/> to free it.
    /// </para>
    /// </summary>
    internal NativePipelineConfigScope<NativeExtractorConfig> ToNative()
    {
        if (string.IsNullOrEmpty(InputPath))
            throw new ArgumentNullException(nameof(InputPath), "InputPath is required.");
        if (string.IsNullOrEmpty(ProfilePath))
            throw new ArgumentNullException(nameof(ProfilePath), "ProfilePath is required.");

        var scope = new NativePipelineConfigScope<NativeExtractorConfig>();

        scope.Config = new NativeExtractorConfig
        {
            input_path = Marshal.StringToHGlobalAnsi(InputPath),
            profile_path = Marshal.StringToHGlobalAnsi(ProfilePath),
            output_path = OutputPath is not null ? Marshal.StringToHGlobalAnsi(OutputPath) : IntPtr.Zero,
            requested_num_threads = RequestedThreads,
            small_component_size = SmallComponentSize,
            use_metadata = UseMetadata ? 1 : 0,
            parse_conditionals = ParseConditionals ? 1 : 0,
            use_locations_cache = UseLocationsCache ? 1 : 0,
            dump_nbg_graph = DumpNbgGraph ? 1 : 0,
        };

        scope.AddAllocation(scope.Config.input_path);
        scope.AddAllocation(scope.Config.profile_path);
        if (scope.Config.output_path != IntPtr.Zero)
            scope.AddAllocation(scope.Config.output_path);

        return scope;
    }
}
