namespace Sharposrm.Route;

/// <summary>
/// A leg of a route between two waypoints.
/// See: https://project-osrm.org/docs/v5.24.0/api/#leg-object
/// </summary>
public sealed class RouteLeg
{
    /// <summary>Distance of this leg in meters.</summary>
    public double Distance { get; set; }

    /// <summary>Duration of this leg in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Weight of this leg.</summary>
    public double Weight { get; set; }

    /// <summary>Short summary of the roads comprising this leg.</summary>
    public string? Summary { get; set; }

    /// <summary>Encoded geometry for this leg (when steps=true and overview=full).</summary>
    public string? Geometry { get; set; }

    /// <summary>The steps comprising this leg.</summary>
    public List<RouteStep>? Steps { get; set; }

    /// <summary>Per-segment annotations (speed, duration, distance, etc.).</summary>
    public LegAnnotation? Annotation { get; set; }
}
