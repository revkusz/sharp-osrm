using System.Text.Json.Serialization;
using Sharposrm.Route;

namespace Sharposrm.Trip;

/// <summary>
/// Top-level response from the OSRM Trip service.
/// See: https://project-osrm.org/docs/v5.24.0/api/#trip-response-object
/// </summary>
public sealed class TripResponse
{
    /// <summary>Response status code (e.g. "Ok" or "NoTrips").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Error message when <see cref="Code"/> is not "Ok".</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The computed trip(s). Each trip is a standard <see cref="Route"/> object.
    /// When <c>roundtrip=true</c>, a single round-trip route is returned.
    /// When <c>roundtrip=false</c>, multiple trip fragments may be returned.
    /// </summary>
    [JsonPropertyName("trips")]
    public List<global::Sharposrm.Route.Route>? Trips { get; set; }

    /// <summary>
    /// Snapped waypoints matching the input coordinates, with trip-specific
    /// <see cref="TripWaypoint.WaypointIndex"/> and <see cref="TripWaypoint.TripsIndex"/> fields.
    /// </summary>
    [JsonPropertyName("waypoints")]
    public List<TripWaypoint>? Waypoints { get; set; }

    /// <summary>
    /// Version of the OSM data used to compute the response (e.g. "20240101").
    /// </summary>
    public string? DataVersion { get; set; }
}
