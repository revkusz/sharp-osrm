using System.Text.Json.Serialization;

namespace Sharposrm.Nearest;

/// <summary>
/// Top-level response from the OSRM Nearest service.
/// See: https://project-osrm.org/docs/v5.24.0/api/#nearest-service
/// </summary>
public sealed class NearestResponse
{
    /// <summary>Response status code (e.g. "Ok" or "NoSegment").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Error message when <see cref="Code"/> is not "Ok".</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Nearest waypoints snapped to the road network.</summary>
    [JsonPropertyName("waypoints")]
    public List<NearestWaypoint>? Waypoints { get; set; }

    /// <summary>
    /// Version of the OSM data used to compute the response (e.g. "20240101").
    /// </summary>
    public string? DataVersion { get; set; }
}
