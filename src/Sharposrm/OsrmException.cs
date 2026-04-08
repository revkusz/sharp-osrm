using System.Runtime.InteropServices;
using Sharposrm.Interop;

namespace Sharposrm;

/// <summary>
/// Exception thrown when the OSRM native engine encounters an error.
/// Use <see cref="FromLastError"/> to create an exception from the
/// thread-local native error message.
/// </summary>
public sealed class OsrmException : Exception
{
    /// <summary>
    /// Creates a new <see cref="OsrmException"/> with the specified message.
    /// </summary>
    public OsrmException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="OsrmException"/> with the specified message
    /// and inner exception.
    /// </summary>
    public OsrmException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an <see cref="OsrmException"/> from the thread-local native error.
    /// Calls <c>sharposrm_get_last_error()</c>, marshals the result to a managed string,
    /// then frees the native string. If no error is set, returns an exception with an
    /// empty message.
    /// </summary>
    public static OsrmException FromLastError()
    {
        IntPtr errorPtr = NativeMethods.sharposrm_get_last_error();
        if (errorPtr == IntPtr.Zero)
        {
            return new OsrmException(string.Empty);
        }

        string message = Marshal.PtrToStringAnsi(errorPtr) ?? string.Empty;
        NativeMethods.sharposrm_free_string(errorPtr);
        return new OsrmException(message);
    }
}
