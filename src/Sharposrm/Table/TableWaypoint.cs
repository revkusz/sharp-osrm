namespace Sharposrm.Table;

/// <summary>
/// A snapped waypoint from the OSRM Table response.
/// Represents either a source or destination coordinate after snapping to the road network.
/// </summary>
public sealed class TableWaypoint
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Name of the street the coordinate snapped to.</summary>
    public string? Name { get; set; }

    /// <summary>Internal hint for OSRM to speed up future queries to this location.</summary>
    public string? Hint { get; set; }

    /// <summary>Distance in meters from the input coordinate to the snapped point.</summary>
    public double Distance { get; set; }
}
