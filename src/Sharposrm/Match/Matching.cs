using Sharposrm.Route;

namespace Sharposrm.Match;

/// <summary>
/// A matching is a route-like object produced by the map matching algorithm.
/// Like a <see cref="global::Sharposrm.Route.Route"/> but with an added <see cref="Confidence"/> score.
/// See: https://project-osrm.org/docs/v5.24.0/api/#matching-object
/// </summary>
public sealed class Matching
{
    /// <summary>
    /// Confidence score for this matching (0.0 to 1.0).
    /// Higher values indicate a better fit between input trace and matched route.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>Total distance of the matching in meters.</summary>
    public double Distance { get; set; }

    /// <summary>Total duration of the matching in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Weight of the matching.</summary>
    public double Weight { get; set; }

    /// <summary>Name of the weight profile used (e.g. "duration", "routability").</summary>
    public string? WeightName { get; set; }

    /// <summary>Route geometry of the matching. Format depends on geometries parameter.</summary>
    public RouteGeometry? Geometry { get; set; }

    /// <summary>The legs comprising this matching. Same structure as route legs.</summary>
    public List<RouteLeg>? Legs { get; set; }
}
