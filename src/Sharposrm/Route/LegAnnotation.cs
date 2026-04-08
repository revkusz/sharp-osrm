namespace Sharposrm.Route;

/// <summary>
/// Annotations for a route leg, containing per-segment metadata.
/// See: https://project-osrm.org/docs/v5.24.0/api/#annotation-object
/// </summary>
public sealed class LegAnnotation
{
    /// <summary>Speed in km/h for each segment.</summary>
    public List<double>? Speed { get; set; }

    /// <summary>Duration in seconds for each segment.</summary>
    public List<double>? Duration { get; set; }

    /// <summary>Distance in meters for each segment.</summary>
    public List<double>? Distance { get; set; }

    /// <summary>Weight for each segment.</summary>
    public List<double>? Weight { get; set; }

    /// <summary>Datasource index for each segment.</summary>
    public List<int>? Datasources { get; set; }

    /// <summary>OSM node IDs for each segment endpoint.</summary>
    public List<long>? Nodes { get; set; }
}
