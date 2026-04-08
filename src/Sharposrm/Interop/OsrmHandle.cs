using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sharposrm.Interop;

/// <summary>
/// <see cref="SafeHandle"/> wrapper for the native OSRM engine pointer.
/// Ensures <c>sharposrm_destroy</c> is called exactly once when the handle is released.
/// The public parameterless constructor is required by .NET 8's LibraryImport
/// source generator for <c>SafeHandle</c> return types.
/// </summary>
internal sealed class OsrmHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Creates an uninitialized handle. Required by LibraryImport source generation.
    /// </summary>
    public OsrmHandle()
        : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Creates a handle wrapping an existing native pointer.
    /// </summary>
    /// <param name="ptr">Native engine pointer (must be non-zero).</param>
    internal OsrmHandle(IntPtr ptr)
        : base(ownsHandle: true)
    {
        SetHandle(ptr);
    }

    /// <summary>
    /// Releases the native OSRM engine by calling <c>sharposrm_destroy</c>.
    /// Safe to call even if the handle is zero or already invalid — the native
    /// function is a no-op for null pointers.
    /// </summary>
    /// <returns>Always <c>true</c> — destruction cannot fail.</returns>
    protected override bool ReleaseHandle()
    {
        NativeMethods.sharposrm_destroy(handle);
        return true;
    }
}
