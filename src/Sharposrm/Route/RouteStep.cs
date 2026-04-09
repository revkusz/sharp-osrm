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

    /// <summary>Route geometry for this step. Format depends on the geometries parameter.</summary>
    public RouteGeometry? Geometry { get; set; }

    /// <summary>The maneuver instruction for this step.</summary>
    public StepManeuver? Maneuver { get; set; }

    /// <summary>Intersections along this step.</summary>
    public List<Intersection>? Intersections { get; set; }

    /// <summary>SSML pronunciation hint for the step name.</summary>
    public string? Pronunciation { get; set; }

    /// <summary>Destinations for the step (e.g. "A1, A2").</summary>
    public string? Destinations { get; set; }

    /// <summary>Exit names for roundabout maneuvers.</summary>
    public string? Exits { get; set; }

    /// <summary>Name of the rotary (for rotary maneuvers).</summary>
    public string? RotaryName { get; set; }

    /// <summary>SSML pronunciation hint for the rotary name.</summary>
    public string? RotaryPronunciation { get; set; }

    /// <summary>Driving side: "left" or "right".</summary>
    public string? DrivingSide { get; set; }
}
