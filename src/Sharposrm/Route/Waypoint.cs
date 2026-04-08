namespace Sharposrm.Route;

/// <summary>
/// A snapped waypoint from the OSRM Route response.
/// See: https://project-osrm.org/docs/v5.24.0/api/#waypoint-object
/// </summary>
public sealed class Waypoint
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Name of the street the coordinate snapped to.</summary>
    public string? Name { get; set; }

    /// <summary>Distance in meters from the input coordinate to the snapped point.</summary>
    public double Distance { get; set; }

    /// <summary>Internal hint for OSRM to speed up future queries to this location.</summary>
    public string? Hint { get; set; }
}
