namespace Sharposrm;

/// <summary>
/// A bearing constraint for limiting the road segment search to a given direction.
/// Consists of a clockwise angle from true north (0–359) and a deviation range (0–180).
/// </summary>
/// <param name="Value">Clockwise bearing in degrees (0–359).</param>
/// <param name="Deviation">Allowed deviation from the bearing in degrees (0–180).</param>
public readonly record struct Bearing(short Value, short Deviation)
{
    /// <summary>
    /// Sentinel indicating no bearing constraint for this coordinate.
    /// Serialized as {-1, -1} across the FFI boundary.
    /// </summary>
    public static Bearing None { get; } = new(-1, -1);
}
