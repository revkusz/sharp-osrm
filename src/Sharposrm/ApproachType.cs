namespace Sharposrm;

/// <summary>
/// Approach type for routing — controls which side of the road to start/end on.
/// Must match the C bridge's <c>SharposrmApproachType</c> enum values.
/// </summary>
public enum ApproachType
{
    /// <summary>Start/end on the curb side (right-hand traffic default).</summary>
    Curb = 0,

    /// <summary>Unrestricted — either side of the road.</summary>
    Unrestricted = 1,

    /// <summary>Start/end on the opposite side of the road.</summary>
    Opposite = 2
}
