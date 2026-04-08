using System.Runtime.InteropServices;

namespace Sharposrm.Pipeline;

/// <summary>
/// Generic owning handle for a native pipeline config struct and its allocated strings/arrays.
/// Follows the <c>NativeConfigScope</c> pattern from EngineConfig.
/// Dispose to free all unmanaged memory.
/// </summary>
internal sealed class NativePipelineConfigScope<TConfig> : IDisposable
    where TConfig : struct
{
    private readonly List<IntPtr> _allocations = new();
    private bool _disposed;

    /// <summary>
    /// The blittable native config struct. Set by the caller after construction.
    /// </summary>
    public TConfig Config;

    /// <summary>
    /// Track an unmanaged allocation for cleanup on <see cref="Dispose"/>.
    /// </summary>
    internal void AddAllocation(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
            _allocations.Add(ptr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var ptr in _allocations)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _allocations.Clear();
        _disposed = true;
    }
}
