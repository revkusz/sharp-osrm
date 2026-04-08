namespace Sharposrm.Route;

/// <summary>
/// A step of a route leg. Each step describes a maneuver.
/// See: https://project-osrm.org/docs/v5.24.0/api/#step-object
/// </summary>
public sealed class RouteStep
{
    /// <summary>Distance of this step in meters.</summary>
    public double Distance { get; set; }

    /// <summary>Duration of this step in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Weight of this step.</summary>
    public double Weight { get; set; }

    /// <summary>Name of the road or way used by this step.</summary>
    public string? Name { get; set; }

    /// <summary>Reference code or road number (e.g. "A1", "M25").</summary>
    public string? Ref { get; set; }

    /// <summary>Mode of transport (e.g. "driving", "ferry").</summary>
    public string? Mode { get; set; }

    /// <summary>Encoded geometry for this step.</summary>
    public string? Geometry { get; set; }

    /// <summary>The maneuver instruction for this step.</summary>
    public StepManeuver? Maneuver { get; set; }

    /// <summary>Intersections along this step.</summary>
    public List<Intersection>? Intersections { get; set; }
}
