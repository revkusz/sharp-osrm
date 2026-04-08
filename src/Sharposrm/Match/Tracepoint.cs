namespace Sharposrm.Match;

/// <summary>
/// A tracepoint represents a snapped input coordinate in the map matching result.
/// Each tracepoint links back to its parent matching and indicates how many
/// alternative matchings were found at that location.
/// See: https://project-osrm.org/docs/v5.24.0/api/#tracepoint-object
/// </summary>
public sealed class Tracepoint
{
    /// <summary>Snapped coordinate [longitude, latitude].</summary>
    public double[]? Location { get; set; }

    /// <summary>Name of the street the coordinate snapped to.</summary>
    public string? Name { get; set; }

    /// <summary>Internal hint for faster subsequent queries.</summary>
    public string? Hint { get; set; }

    /// <summary>Index into the <see cref="MatchResponse.Matchings"/> list for this tracepoint.</summary>
    public int MatchingsIndex { get; set; }

    /// <summary>Index of the waypoint within the matching.</summary>
    public int WaypointIndex { get; set; }

    /// <summary>Number of alternative matchings at this tracepoint.</summary>
    public int AlternativesCount { get; set; }

    /// <summary>Distance from the input coordinate to the road segment (in meters).</summary>
    public double Distance { get; set; }
}
