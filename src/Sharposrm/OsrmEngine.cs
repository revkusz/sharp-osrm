using System.Runtime.InteropServices;
using System.Text.Json;
using Sharposrm.Interop;
using Sharposrm.Match;
using Sharposrm.Nearest;
using Sharposrm.Route;
using Sharposrm.Table;
using Sharposrm.Tile;
using Sharposrm.Trip;

namespace Sharposrm;

/// <summary>
/// Public OSRM engine wrapper providing async lifecycle management.
/// Wraps the native <see cref="OsrmHandle"/> SafeHandle and exposes
/// factory methods for synchronous and asynchronous engine creation.
/// </summary>
public sealed class OsrmEngine : IAsyncDisposable
{
    private readonly OsrmHandle _handle;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private volatile bool _disposed;

    /// <summary>
    /// Gets whether this engine instance has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Internal handle accessor for future service methods (S02+).
    /// Throws <see cref="ObjectDisposedException"/> if the engine is disposed.
    /// </summary>
    internal OsrmHandle Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>
    /// Private constructor. Use <see cref="Create"/> or <see cref="CreateAsync"/>
    /// factory methods to create instances.
    /// </summary>
    private OsrmEngine(OsrmHandle handle, int maxConcurrency)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <summary>
    /// Creates a new OSRM engine instance from the given configuration.
    /// This call blocks the thread while OSRM loads and processes the dataset,
    /// which can take several seconds for large datasets.
    /// </summary>
    /// <param name="config">Engine configuration. Must not be <c>null</c>.</param>
    /// <returns>A new <see cref="OsrmEngine"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OsrmException">Native engine construction failed.</exception>
    public static OsrmEngine Create(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.MaxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config.MaxConcurrency),
                config.MaxConcurrency,
                "MaxConcurrency must be at least 1.");
        }

        // ToNativeConfig validates StoragePath and allocates native strings
        using var nativeScope = config.ToNativeConfig();
        var nativeConfig = nativeScope.Config;

        IntPtr handlePtr = NativeMethods.sharposrm_create(ref nativeConfig);

        // nativeScope disposed here — strings are no longer needed after create returns

        if (handlePtr == IntPtr.Zero)
        {
            throw OsrmException.FromLastError();
        }

        return new OsrmEngine(new OsrmHandle(handlePtr), config.MaxConcurrency);
    }

    /// <summary>
    /// Creates a new OSRM engine instance asynchronously.
    /// Offloads the blocking <see cref="Create"/> call to the thread pool.
    /// </summary>
    /// <param name="config">Engine configuration. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces a new <see cref="OsrmEngine"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="OsrmException">Native engine construction failed.</exception>
    public static Task<OsrmEngine> CreateAsync(EngineConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() => Create(config), ct);
    }

    /// <summary>
    /// Executes an async service call through the concurrency semaphore with
    /// cooperative cancellation checks at entry, post-acquire, and post-call points.
    /// </summary>
    /// <typeparam name="T">Return type of the service call.</typeparam>
    /// <param name="syncCall">The synchronous native call to execute.</param>
    /// <param name="ct">Cancellation token propagated from the public method.</param>
    private async Task<T> ExecuteAsync<T>(Func<T> syncCall, CancellationToken ct)
    {
        // Fail-fast: if engine is disposed, don't wait on semaphore
        _ = Handle;

        await _concurrencySemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Post-acquire check: cancellation may have been requested while waiting
            ct.ThrowIfCancellationRequested();

            var result = syncCall();

            // Post-call check: allow caller to bail out after a long native call
            ct.ThrowIfCancellationRequested();

            return result;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Computes a route using the OSRM Route service.
    /// </summary>
    /// <param name="parameters">Route parameters. Must not be <c>null</c>.</param>
    /// <returns>The typed route response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public RouteResponse Route(RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Check disposed state via Handle property
        _ = Handle;

        // Convert managed params to native struct — coordinate arrays stay pinned until scope disposal
        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_route(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultPtr);

        // The native call has returned — scope disposal is now safe.
        // Read and free the JSON string regardless of status (OSRM returns error info in JSON too).
        string json;
        try
        {
            json = resultPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(resultPtr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (resultPtr != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultPtr);
            }
        }

        // On error, try to extract a meaningful message from the JSON
        if (status == 1)
        {
            string errorMessage = json;
            try
            {
                var errorResponse = JsonSerializer.Deserialize<RouteResponse>(json, RouteResponse.SerializerOptions);
                if (errorResponse is not null)
                {
                    errorMessage = $"OSRM error: {errorResponse.Code}" +
                        (string.IsNullOrEmpty(errorResponse.Message) ? string.Empty : $" — {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // Fall back to raw JSON as error message
            }

            // If no JSON error info, fall back to the native thread-local error
            if (string.IsNullOrEmpty(errorMessage))
            {
                IntPtr errorPtr = NativeMethods.sharposrm_get_last_error();
                if (errorPtr != IntPtr.Zero)
                {
                    errorMessage = Marshal.PtrToStringAnsi(errorPtr) ?? string.Empty;
                }
            }

            throw new OsrmException(errorMessage);
        }

        // Deserialize the successful response
        var response = JsonSerializer.Deserialize<RouteResponse>(json, RouteResponse.SerializerOptions);
        return response ?? throw new OsrmException("Failed to deserialize OSRM route response: result was null.");
    }

    /// <summary>
    /// Computes a route asynchronously using the OSRM Route service.
    /// Offloads the blocking <see cref="Route"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Route parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the typed route response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public Task<RouteResponse> RouteAsync(RouteParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Route(parameters), ct);
    }

    /// <summary>
    /// Computes a distance/time table using the OSRM Table service.
    /// </summary>
    /// <param name="parameters">Table parameters. Must not be <c>null</c>.</param>
    /// <returns>The typed table response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public TableResponse Table(TableParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_table(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultPtr);

        string json;
        try
        {
            json = resultPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(resultPtr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (resultPtr != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultPtr);
            }
        }

        if (status == 1)
        {
            string errorMessage = json;
            try
            {
                var errorResponse = JsonSerializer.Deserialize<TableResponse>(json, RouteResponse.SerializerOptions);
                if (errorResponse is not null)
                {
                    errorMessage = $"OSRM error: {errorResponse.Code}" +
                        (string.IsNullOrEmpty(errorResponse.Message) ? string.Empty : $" — {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // Fall back to raw JSON as error message
            }

            throw new OsrmException(errorMessage);
        }

        var response = JsonSerializer.Deserialize<TableResponse>(json, RouteResponse.SerializerOptions);
        return response ?? throw new OsrmException("Failed to deserialize OSRM table response: result was null.");
    }

    /// <summary>
    /// Computes a distance/time table asynchronously using the OSRM Table service.
    /// </summary>
    /// <param name="parameters">Table parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the typed table response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public Task<TableResponse> TableAsync(TableParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Table(parameters), ct);
    }

    /// <summary>
    /// Finds the nearest road network locations using the OSRM Nearest service.
    /// </summary>
    /// <param name="parameters">Nearest parameters. Must not be <c>null</c>.</param>
    /// <returns>The typed nearest response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public NearestResponse Nearest(NearestParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_nearest(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultPtr);

        string json;
        try
        {
            json = resultPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(resultPtr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (resultPtr != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultPtr);
            }
        }

        if (status == 1)
        {
            string errorMessage = json;
            try
            {
                var errorResponse = JsonSerializer.Deserialize<NearestResponse>(json, RouteResponse.SerializerOptions);
                if (errorResponse is not null)
                {
                    errorMessage = $"OSRM error: {errorResponse.Code}" +
                        (string.IsNullOrEmpty(errorResponse.Message) ? string.Empty : $" — {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // Fall back to raw JSON as error message
            }

            throw new OsrmException(errorMessage);
        }

        var response = JsonSerializer.Deserialize<NearestResponse>(json, RouteResponse.SerializerOptions);
        return response ?? throw new OsrmException("Failed to deserialize OSRM nearest response: result was null.");
    }

    /// <summary>
    /// Finds the nearest road network locations asynchronously using the OSRM Nearest service.
    /// </summary>
    /// <param name="parameters">Nearest parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the typed nearest response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public Task<NearestResponse> NearestAsync(NearestParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Nearest(parameters), ct);
    }

    /// <summary>
    /// Computes a trip (round-trip or one-way) using the OSRM Trip service.
    /// </summary>
    /// <param name="parameters">Trip parameters. Must not be <c>null</c>.</param>
    /// <returns>The typed trip response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public TripResponse Trip(TripParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_trip(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultPtr);

        string json;
        try
        {
            json = resultPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(resultPtr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (resultPtr != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultPtr);
            }
        }

        if (status == 1)
        {
            string errorMessage = json;
            try
            {
                var errorResponse = JsonSerializer.Deserialize<TripResponse>(json, RouteResponse.SerializerOptions);
                if (errorResponse is not null)
                {
                    errorMessage = $"OSRM error: {errorResponse.Code}" +
                        (string.IsNullOrEmpty(errorResponse.Message) ? string.Empty : $" — {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // Fall back to raw JSON as error message
            }

            throw new OsrmException(errorMessage);
        }

        var response = JsonSerializer.Deserialize<TripResponse>(json, RouteResponse.SerializerOptions);
        return response ?? throw new OsrmException("Failed to deserialize OSRM trip response: result was null.");
    }

    /// <summary>
    /// Computes a trip asynchronously using the OSRM Trip service.
    /// </summary>
    /// <param name="parameters">Trip parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the typed trip response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public Task<TripResponse> TripAsync(TripParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Trip(parameters), ct);
    }

    /// <summary>
    /// Performs map matching using the OSRM Match service.
    /// Matches a trace of GPS coordinates to the road network.
    /// </summary>
    /// <param name="parameters">Match parameters. Must not be <c>null</c>.</param>
    /// <returns>The typed match response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public MatchResponse Match(MatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_match(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultPtr);

        string json;
        try
        {
            json = resultPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(resultPtr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (resultPtr != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultPtr);
            }
        }

        if (status == 1)
        {
            string errorMessage = json;
            try
            {
                var errorResponse = JsonSerializer.Deserialize<MatchResponse>(json, RouteResponse.SerializerOptions);
                if (errorResponse is not null)
                {
                    errorMessage = $"OSRM error: {errorResponse.Code}" +
                        (string.IsNullOrEmpty(errorResponse.Message) ? string.Empty : $" — {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // Fall back to raw JSON as error message
            }

            throw new OsrmException(errorMessage);
        }

        var response = JsonSerializer.Deserialize<MatchResponse>(json, RouteResponse.SerializerOptions);
        return response ?? throw new OsrmException("Failed to deserialize OSRM match response: result was null.");
    }

    /// <summary>
    /// Performs map matching asynchronously using the OSRM Match service.
    /// Offloads the blocking <see cref="Match"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Match parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the typed match response from OSRM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error, or the response could not be deserialized.</exception>
    public Task<MatchResponse> MatchAsync(MatchParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Match(parameters), ct);
    }

    /// <summary>
    /// Computes a route using the OSRM Route service and returns a flatbuffer-encoded byte array.
    /// </summary>
    /// <param name="parameters">Route parameters. Must not be <c>null</c>.</param>
    /// <returns>Flatbuffer-encoded route response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] RouteFlatbuffer(RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_route_fb(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        if (status == 1)
        {
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM route service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] result;
        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Computes a route asynchronously using the OSRM Route service and returns a flatbuffer-encoded byte array.
    /// Offloads the blocking <see cref="RouteFlatbuffer"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Route parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the flatbuffer-encoded route response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> RouteFlatbufferAsync(RouteParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => RouteFlatbuffer(parameters), ct);
    }

    /// <summary>
    /// Computes a distance/time table using the OSRM Table service and returns a flatbuffer-encoded byte array.
    /// </summary>
    /// <param name="parameters">Table parameters. Must not be <c>null</c>.</param>
    /// <returns>Flatbuffer-encoded table response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] TableFlatbuffer(TableParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_table_fb(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        if (status == 1)
        {
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM table service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] result;
        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Computes a distance/time table asynchronously using the OSRM Table service and returns a flatbuffer-encoded byte array.
    /// Offloads the blocking <see cref="TableFlatbuffer"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Table parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the flatbuffer-encoded table response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> TableFlatbufferAsync(TableParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => TableFlatbuffer(parameters), ct);
    }

    /// <summary>
    /// Finds the nearest road network locations using the OSRM Nearest service and returns a flatbuffer-encoded byte array.
    /// </summary>
    /// <param name="parameters">Nearest parameters. Must not be <c>null</c>.</param>
    /// <returns>Flatbuffer-encoded nearest response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] NearestFlatbuffer(NearestParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_nearest_fb(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        if (status == 1)
        {
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM nearest service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] result;
        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Finds the nearest road network locations asynchronously using the OSRM Nearest service and returns a flatbuffer-encoded byte array.
    /// Offloads the blocking <see cref="NearestFlatbuffer"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Nearest parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the flatbuffer-encoded nearest response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> NearestFlatbufferAsync(NearestParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => NearestFlatbuffer(parameters), ct);
    }

    /// <summary>
    /// Computes a trip (round-trip or one-way) using the OSRM Trip service and returns a flatbuffer-encoded byte array.
    /// </summary>
    /// <param name="parameters">Trip parameters. Must not be <c>null</c>.</param>
    /// <returns>Flatbuffer-encoded trip response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] TripFlatbuffer(TripParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_trip_fb(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        if (status == 1)
        {
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM trip service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] result;
        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Computes a trip asynchronously using the OSRM Trip service and returns a flatbuffer-encoded byte array.
    /// Offloads the blocking <see cref="TripFlatbuffer"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Trip parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the flatbuffer-encoded trip response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> TripFlatbufferAsync(TripParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => TripFlatbuffer(parameters), ct);
    }

    /// <summary>
    /// Performs map matching using the OSRM Match service and returns a flatbuffer-encoded byte array.
    /// </summary>
    /// <param name="parameters">Match parameters. Must not be <c>null</c>.</param>
    /// <returns>Flatbuffer-encoded match response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] MatchFlatbuffer(MatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        using var scope = parameters.ToNative();
        var nativeParams = scope.Params;

        int status = NativeMethods.sharposrm_match_fb(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        if (status == 1)
        {
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM match service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] result;
        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Performs map matching asynchronously using the OSRM Match service and returns a flatbuffer-encoded byte array.
    /// Offloads the blocking <see cref="MatchFlatbuffer"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Match parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the flatbuffer-encoded match response as raw bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> MatchFlatbufferAsync(MatchParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => MatchFlatbuffer(parameters), ct);
    }

    /// <summary>
    /// Fetches a vector tile (MVT) using the OSRM Tile service.
    /// Returns raw binary MVT data — no JSON deserialization.
    /// </summary>
    /// <param name="parameters">Tile parameters. Must not be <c>null</c>.</param>
    /// <returns>Raw MVT binary data, or an empty array if the tile is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public byte[] Tile(TileParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _ = Handle;

        var nativeParams = parameters.ToNative();

        int status = NativeMethods.sharposrm_tile(
            _handle.DangerousGetHandle(),
            ref nativeParams,
            out IntPtr resultData,
            out int resultLength);

        byte[] result;
        if (status == 1)
        {
            // Free any data even on error
            if (resultData != IntPtr.Zero)
            {
                NativeMethods.sharposrm_free_result(resultData);
            }

            throw new OsrmException("OSRM tile service returned an error.");
        }

        if (resultData == IntPtr.Zero || resultLength <= 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            result = new byte[resultLength];
            Marshal.Copy(resultData, result, 0, resultLength);
        }
        finally
        {
            NativeMethods.sharposrm_free_result(resultData);
        }

        return result;
    }

    /// <summary>
    /// Fetches a vector tile (MVT) asynchronously using the OSRM Tile service.
    /// Offloads the blocking <see cref="Tile"/> call to the thread pool.
    /// </summary>
    /// <param name="parameters">Tile parameters. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token. Checked before starting work.</param>
    /// <returns>A task that produces the raw MVT binary data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before work started.</exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <exception cref="OsrmException">OSRM returned an error.</exception>
    public Task<byte[]> TileAsync(TileParameters parameters, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        return ExecuteAsync(() => Tile(parameters), ct);
    }

    /// <summary>
    /// Disposes the OSRM engine, releasing the native handle.
    /// Safe to call multiple times.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;
        _concurrencySemaphore.Dispose();
        _handle.Dispose();
        return ValueTask.CompletedTask;
    }
}
