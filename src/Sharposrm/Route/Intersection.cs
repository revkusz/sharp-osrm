namespace Sharposrm.Route;

/// <summary>
/// An intersection along a route step.
/// See: https://project-osrm.org/docs/v5.24.0/api/#intersection-object
/// </summary>
public sealed class Intersection
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Clockwise bearings (0–359) of all roads at the intersection.</summary>
    public List<int>? Bearings { get; set; }

    /// <summary>Whether each road entry is available (matches <see cref="Bearings"/> order).</summary>
    public List<bool>? Entry { get; set; }

    /// <summary>Index into <see cref="Bearings"/> for the incoming road, or null.</summary>
    public int? InIndex { get; set; }

    /// <summary>Index into <see cref="Bearings"/> for the outgoing road, or null.</summary>
    public int? OutIndex { get; set; }
}
