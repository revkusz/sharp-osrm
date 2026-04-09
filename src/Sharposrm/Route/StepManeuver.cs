namespace Sharposrm.Route;

/// <summary>
/// A maneuver along a route step.
/// See: https://project-osrm.org/docs/v5.24.0/api/#stepmaneuver-object
/// </summary>
public sealed class StepManeuver
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Clockwise bearing before the maneuver (0–359).</summary>
    public short BearingBefore { get; set; }

    /// <summary>Clockwise bearing after the maneuver (0–359).</summary>
    public short BearingAfter { get; set; }

    /// <summary>Type of maneuver (e.g. "turn", "new name", "arrive").</summary>
    public string? Type { get; set; }

    /// <summary>Direction modifier (e.g. "left", "right", "straight").</summary>
    public string? Modifier { get; set; }

    /// <summary>Exit number for roundabout maneuvers (1-based).</summary>
    public int? Exit { get; set; }
}
