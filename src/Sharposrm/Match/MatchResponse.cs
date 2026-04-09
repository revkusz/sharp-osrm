using System.Text.Json.Serialization;
using Sharposrm.Route;

namespace Sharposrm.Match;

/// <summary>
/// Top-level response from the OSRM Map Matching service.
/// See: https://project-osrm.org/docs/v5.24.0/api/#match-response-object
/// </summary>
public sealed class MatchResponse
{
    /// <summary>Response status code (e.g. "Ok" or "NoMatch").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Error message when <see cref="Code"/> is not "Ok".</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>The matched sub-traces with confidence scores.</summary>
    [JsonPropertyName("matchings")]
    public List<Matching>? Matchings { get; set; }

    /// <summary>Snapped input coordinates with matching metadata.</summary>
    [JsonPropertyName("tracepoints")]
    public List<Tracepoint>? Tracepoints { get; set; }

    /// <summary>
    /// Version of the OSM data used to compute the response (e.g. "20240101").
    /// </summary>
    public string? DataVersion { get; set; }
}
