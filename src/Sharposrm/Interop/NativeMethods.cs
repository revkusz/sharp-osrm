using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Sharposrm.Interop;

/// <summary>
/// Source-generated P/Invoke declarations for the native sharposrm bridge library.
/// Uses <c>[LibraryImport]</c> (NOT <c>[DllImport]</c>) for AOT compatibility and
/// trimming-friendly marshalling. All string parameters use <c>IntPtr</c> for explicit
/// lifetime control by the caller.
/// </summary>
[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(AnsiStringMarshaller))]
internal static partial class NativeMethods
{
    /// <summary>
    /// Create an OSRM engine instance from the given config.
    /// Returns an opaque handle (non-zero on success, zero on failure).
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial IntPtr sharposrm_create(ref NativeEngineConfig config);

    /// <summary>
    /// Destroy an OSRM engine instance. Safe to call with <c>IntPtr.Zero</c>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial void sharposrm_destroy(IntPtr engine);

    /// <summary>
    /// Validate a config without creating an engine.
    /// Returns 1 if valid, 0 if invalid.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_config_is_valid(ref NativeEngineConfig config);

    /// <summary>
    /// Get the last thread-local error message.
    /// Returns <c>IntPtr.Zero</c> if no error is set.
    /// Caller must free the returned string with <see cref="sharposrm_free_string"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial IntPtr sharposrm_get_last_error();

    /// <summary>
    /// Free a string allocated by the native library. Safe to call with <c>IntPtr.Zero</c>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial void sharposrm_free_string(IntPtr s);

    // ── Route service ──────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Route service.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the JSON string and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_route(IntPtr engine, ref NativeRouteParams routeParams, out IntPtr resultJson);

    /// <summary>
    /// Free a JSON result string previously returned by <see cref="sharposrm_route"/>.
    /// Also frees raw byte buffers returned by <see cref="sharposrm_tile"/>.
    /// Safe to call with <c>IntPtr.Zero</c>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial void sharposrm_free_result(IntPtr result);

    // ── Table service ───────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Table (distance matrix) service.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the JSON string and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_table(IntPtr engine, ref NativeTableParams tableParams, out IntPtr resultJson);

    // ── Nearest service ─────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Nearest service.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the JSON string and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_nearest(IntPtr engine, ref NativeNearestParams nearestParams, out IntPtr resultJson);

    // ── Trip service ────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Trip service.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the JSON string and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_trip(IntPtr engine, ref NativeTripParams tripParams, out IntPtr resultJson);

    // ── Match service ───────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Map Matching service.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the JSON string and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_match(IntPtr engine, ref NativeMatchParams matchParams, out IntPtr resultJson);

    // ── Flatbuffer-encoded output variants ─────────────────────────────

    /// <summary>
    /// Run the OSRM Route service and return a flatbuffer-encoded byte array.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_route_fb(IntPtr engine, ref NativeRouteParams routeParams, out IntPtr resultData, out int resultLength);

    /// <summary>
    /// Run the OSRM Table service and return a flatbuffer-encoded byte array.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_table_fb(IntPtr engine, ref NativeTableParams tableParams, out IntPtr resultData, out int resultLength);

    /// <summary>
    /// Run the OSRM Nearest service and return a flatbuffer-encoded byte array.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_nearest_fb(IntPtr engine, ref NativeNearestParams nearestParams, out IntPtr resultData, out int resultLength);

    /// <summary>
    /// Run the OSRM Trip service and return a flatbuffer-encoded byte array.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_trip_fb(IntPtr engine, ref NativeTripParams tripParams, out IntPtr resultData, out int resultLength);

    /// <summary>
    /// Run the OSRM Match service and return a flatbuffer-encoded byte array.
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_match_fb(IntPtr engine, ref NativeMatchParams matchParams, out IntPtr resultData, out int resultLength);

    // ── Tile service ────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM Tile (MVT) service.
    /// Returns raw binary MVT data (not JSON).
    /// Returns 0 on Ok, 1 on Error (matching osrm::Status).
    /// The caller owns the binary buffer and must free it with <see cref="sharposrm_free_result"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_tile(IntPtr engine, ref NativeTileParams tileParams, out IntPtr resultData, out int resultLength);

    // ── Data pipeline: extract, partition, customize, contract ─────────

    /// <summary>
    /// Run the OSRM extraction pipeline stage.
    /// Returns 0 on success, 1 on failure. On failure, call <see cref="sharposrm_get_last_error"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_extract(ref NativeExtractorConfig config);

    /// <summary>
    /// Run the OSRM graph partitioning pipeline stage.
    /// Returns 0 on success, 1 on failure. On failure, call <see cref="sharposrm_get_last_error"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_partition(ref NativePartitionerConfig config);

    /// <summary>
    /// Run the OSRM MLD customization pipeline stage.
    /// Returns 0 on success, 1 on failure. On failure, call <see cref="sharposrm_get_last_error"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_customize(ref NativeCustomizerConfig config);

    /// <summary>
    /// Run the OSRM CH contraction pipeline stage.
    /// Returns 0 on success, 1 on failure. On failure, call <see cref="sharposrm_get_last_error"/>.
    /// </summary>
    [LibraryImport("sharposrm")]
    internal static partial int sharposrm_contract(ref NativeContractorConfig config);
}
