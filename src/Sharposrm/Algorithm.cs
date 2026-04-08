namespace Sharposrm;

/// <summary>
/// Routing algorithm used by the OSRM engine.
/// Must match the C bridge's <c>SharposrmAlgorithm</c> enum values.
/// </summary>
public enum Algorithm
{
    /// <summary>Contraction Hierarchies — faster queries, longer preprocessing.</summary>
    CH = 0,

    /// <summary>Multi-Level Dijkstra — more flexible, shorter preprocessing.</summary>
    MLD = 1
}
