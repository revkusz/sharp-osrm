namespace Sharposrm.Route;

/// <summary>
/// A lane at an intersection.
/// See: https://project-osrm.org/docs/v5.24.0/api/#lane-object
/// </summary>
public sealed class Lane
{
    /// <summary>Lane indications (e.g. ["left", "straight"]).</summary>
    public List<string>? Indications { get; set; }

    /// <summary>Whether this lane applies to the route.</summary>
    public bool Valid { get; set; }
}
