using Sharposrm.Interop;

namespace Sharposrm.Pipeline;

/// <summary>
/// Static class exposing OSRM's four data pipeline functions as .NET methods.
/// No engine instance is needed — these operate on the data files directly.
/// <para>
/// Pipeline order: <see cref="Extract"/> → <see cref="Partition"/> → <see cref="Customize"/> (MLD)
///                   or <see cref="Extract"/> → <see cref="Contract"/> (CH).
/// </para>
/// </summary>
public static class OsrmPipeline
{
    // ── Extract ──────────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM extraction pipeline stage.
    /// Converts the input <c>.osm.pbf</c>/<c>.osm.xml</c> using the given Lua profile
    /// into the <c>.osrm</c> base file and associated sidecar files.
    /// </summary>
    /// <param name="config">Extractor configuration. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OsrmException">Extraction failed.</exception>
    public static void Extract(ExtractorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var scope = config.ToNative();
        var nativeConfig = scope.Config;

        int result = NativeMethods.sharposrm_extract(ref nativeConfig);
        if (result != 0)
        {
            throw OsrmException.FromLastError();
        }
    }

    /// <summary>
    /// Run the OSRM extraction pipeline stage asynchronously.
    /// Offloads the blocking <see cref="Extract"/> call to the thread pool.
    /// </summary>
    /// <param name="config">Extractor configuration. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="OsrmException">Extraction failed.</exception>
    public static Task ExtractAsync(ExtractorConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() => Extract(config), ct);
    }

    // ── Partition ────────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM graph partitioning pipeline stage.
    /// Partitions the graph extracted by <see cref="Extract"/> into a multi-level
    /// domain (MLD) partition required for the MLD algorithm.
    /// </summary>
    /// <param name="config">Partitioner configuration. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OsrmException">Partitioning failed.</exception>
    public static void Partition(PartitionerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var scope = config.ToNative();
        var nativeConfig = scope.Config;

        int result = NativeMethods.sharposrm_partition(ref nativeConfig);
        if (result != 0)
        {
            throw OsrmException.FromLastError();
        }
    }

    /// <summary>
    /// Run the OSRM graph partitioning pipeline stage asynchronously.
    /// Offloads the blocking <see cref="Partition"/> call to the thread pool.
    /// </summary>
    /// <param name="config">Partitioner configuration. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="OsrmException">Partitioning failed.</exception>
    public static Task PartitionAsync(PartitionerConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() => Partition(config), ct);
    }

    // ── Customize ────────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM MLD customization pipeline stage.
    /// Customizes the partitioned graph for MLD routing. Must be run after <see cref="Partition"/>.
    /// </summary>
    /// <param name="config">Customizer configuration. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OsrmException">Customization failed.</exception>
    public static void Customize(CustomizerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var scope = config.ToNative();
        var nativeConfig = scope.Config;

        int result = NativeMethods.sharposrm_customize(ref nativeConfig);
        if (result != 0)
        {
            throw OsrmException.FromLastError();
        }
    }

    /// <summary>
    /// Run the OSRM MLD customization pipeline stage asynchronously.
    /// Offloads the blocking <see cref="Customize"/> call to the thread pool.
    /// </summary>
    /// <param name="config">Customizer configuration. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="OsrmException">Customization failed.</exception>
    public static Task CustomizeAsync(CustomizerConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() => Customize(config), ct);
    }

    // ── Contract ─────────────────────────────────────────────────────────

    /// <summary>
    /// Run the OSRM CH contraction pipeline stage.
    /// Contracts the graph extracted by <see cref="Extract"/> for Contraction Hierarchies routing.
    /// </summary>
    /// <param name="config">Contractor configuration. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OsrmException">Contraction failed.</exception>
    public static void Contract(ContractorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var scope = config.ToNative();
        var nativeConfig = scope.Config;

        int result = NativeMethods.sharposrm_contract(ref nativeConfig);
        if (result != 0)
        {
            throw OsrmException.FromLastError();
        }
    }

    /// <summary>
    /// Run the OSRM CH contraction pipeline stage asynchronously.
    /// Offloads the blocking <see cref="Contract"/> call to the thread pool.
    /// </summary>
    /// <param name="config">Contractor configuration. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="OsrmException">Contraction failed.</exception>
    public static Task ContractAsync(ContractorConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() => Contract(config), ct);
    }
}
