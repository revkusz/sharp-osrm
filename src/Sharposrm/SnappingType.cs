namespace Sharposrm;

/// <summary>
/// Snapping type — controls how coordinates are snapped to the road network.
/// Must match the C bridge's <c>SharposrmSnappingType</c> enum values.
/// </summary>
public enum SnappingType
{
    /// <summary>Default snapping behavior.</summary>
    Default = 0,

    /// <summary>Snap to any node in the road network.</summary>
    Any = 1
}
