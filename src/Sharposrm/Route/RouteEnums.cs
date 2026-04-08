using System;

namespace Sharposrm.Route;

/// <summary>
/// Geometry encoding format for route responses.
/// Matches <c>SharposrmGeometriesType</c> in the C bridge.
/// </summary>
public enum GeometriesType
{
    /// <summary>Polyline encoding (5-digit precision).</summary>
    Polyline = 0,
    /// <summary>Polyline encoding (6-digit precision).</summary>
    Polyline6 = 1,
    /// <summary>GeoJSON geometry.</summary>
    GeoJSON = 2,
}

/// <summary>
/// Overview geometry simplification level.
/// Matches <c>SharposrmOverviewType</c> in the C bridge.
/// </summary>
public enum OverviewType
{
    /// <summary>Simplified overview geometry.</summary>
    Simplified = 0,
    /// <summary>Full overview geometry (no simplification).</summary>
    Full = 1,
    /// <summary>No overview geometry in response.</summary>
    False = 2,
}

/// <summary>
/// Bitmask flags for selecting which annotation values to include in route responses.
/// Matches <c>SharposrmAnnotationsType</c> in the C bridge.
/// </summary>
[Flags]
public enum AnnotationsType : uint
{
    /// <summary>No annotations.</summary>
    None = 0,
    /// <summary>Duration annotation.</summary>
    Duration = 0x01,
    /// <summary>Nodes annotation.</summary>
    Nodes = 0x02,
    /// <summary>Distance annotation.</summary>
    Distance = 0x04,
    /// <summary>Weight annotation.</summary>
    Weight = 0x08,
    /// <summary>Datasources annotation.</summary>
    Datasources = 0x10,
    /// <summary>Speed annotation.</summary>
    Speed = 0x20,
    /// <summary>All annotations.</summary>
    All = 0x3F,
}
