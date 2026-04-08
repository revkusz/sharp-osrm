namespace Sharposrm.Nearest;

/// <summary>
/// A snapped waypoint from the OSRM Nearest response.
/// Includes snapped node IDs that are unique to the nearest service.
/// </summary>
public sealed class NearestWaypoint
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Name of the street the coordinate snapped to.</summary>
    public string? Name { get; set; }

    /// <summary>Internal hint for OSRM to speed up future queries to this location.</summary>
    public string? Hint { get; set; }

    /// <summary>Distance in meters from the input coordinate to the snapped point.</summary>
    public double Distance { get; set; }

    /// <summary>Nearest node IDs snapped to the road network.</summary>
    public long[]? Nodes { get; set; }
}
