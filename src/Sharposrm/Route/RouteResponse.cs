using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharposrm.Route;

/// <summary>
/// Top-level response from the OSRM Route service.
/// See: https://project-osrm.org/docs/v5.24.0/api/#route-response-object
/// </summary>
public sealed class RouteResponse
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> configured for OSRM's snake_case JSON convention.
    /// Use this when deserializing: <c>JsonSerializer.Deserialize&lt;RouteResponse&gt;(json, RouteResponse.SerializerOptions)</c>.
    /// </summary>
    internal static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Response status code (e.g. "Ok" or "NoRoute").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Error message when <see cref="Code"/> is not "Ok".
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The computed route(s).
    /// </summary>
    [JsonPropertyName("routes")]
    public List<Route>? Routes { get; set; }

    /// <summary>
    /// Snapped waypoints matching the input coordinates.
    /// </summary>
    [JsonPropertyName("waypoints")]
    public List<Waypoint>? Waypoints { get; set; }
}
