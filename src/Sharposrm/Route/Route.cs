namespace Sharposrm.Route;

/// <summary>
/// A route between two or more waypoints.
/// See: https://project-osrm.org/docs/v5.24.0/api/#route-object
/// </summary>
public sealed class Route
{
    /// <summary>Total distance in meters.</summary>
    public double Distance { get; set; }

    /// <summary>Total duration in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Total weight (customizable routing metric).</summary>
    public double Weight { get; set; }

    /// <summary>Name of the weight profile used (e.g. "duration", "routability").</summary>
    public string? WeightName { get; set; }

    /// <summary>Route geometry. Format depends on the geometries parameter:
    /// polyline string for Polyline/Polyline6, GeoJSON object for GeoJSON.</summary>
    public RouteGeometry? Geometry { get; set; }

    /// <summary>The legs between waypoints.</summary>
    public List<RouteLeg>? Legs { get; set; }
}
