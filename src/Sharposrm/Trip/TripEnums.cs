namespace Sharposrm.Trip;

/// <summary>
/// Source type for the Trip service.
/// Controls which coordinate is used as the start of the trip.
/// </summary>
public enum SourceType
{
    /// <summary>Any coordinate can be the source (default).</summary>
    Any = 0,

    /// <summary>Only the first coordinate can be the source.</summary>
    First = 1,
}

/// <summary>
/// Destination type for the Trip service.
/// Controls which coordinate is used as the end of the trip.
/// </summary>
public enum DestinationType
{
    /// <summary>Any coordinate can be the destination (default).</summary>
    Any = 0,

    /// <summary>Only the last coordinate can be the destination.</summary>
    Last = 1,
}
