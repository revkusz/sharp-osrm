using System;

namespace Sharposrm.Table;

/// <summary>
/// Bitmask flags for selecting which annotation values to include in Table service responses.
/// Matches <c>SharposrmTableAnnotationsType</c> in the C bridge.
/// Note: This is distinct from <see cref="Route.AnnotationsType"/> — Table only supports Duration and Distance.
/// </summary>
[Flags]
public enum TableAnnotationsType : uint
{
    /// <summary>No annotations.</summary>
    None = 0,
    /// <summary>Duration (travel time) annotation.</summary>
    Duration = 0x01,
    /// <summary>Distance annotation.</summary>
    Distance = 0x02,
    /// <summary>Both Duration and Distance annotations.</summary>
    All = 0x03,
}

/// <summary>
/// Coordinate type used for computing fallback speed columns in the Table service.
/// Matches <c>SharposrmFallbackCoordinateType</c> in the C bridge.
/// </summary>
public enum FallbackCoordinateType
{
    /// <summary>Use the input coordinate for fallback computation.</summary>
    Input = 0,
    /// <summary>Use the snapped coordinate for fallback computation.</summary>
    Snapped = 1,
}
