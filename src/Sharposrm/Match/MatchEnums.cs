namespace Sharposrm.Match;

/// <summary>
/// How the map matching algorithm handles gaps in the input trace.
/// Matches <c>SharposrmGapsType</c> in the C bridge.
/// </summary>
public enum GapsType
{
    /// <summary>
    /// Split the trace into multiple matchings at gaps (default).
    /// Each gap starts a new matching sub-trace.
    /// </summary>
    Split = 0,

    /// <summary>
    /// Ignore gaps and treat the trace as one continuous sequence.
    /// </summary>
    Ignore = 1,
}
