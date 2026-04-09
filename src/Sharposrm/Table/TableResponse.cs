using System.Text.Json.Serialization;

namespace Sharposrm.Table;

/// <summary>
/// Top-level response from the OSRM Table (distance matrix) service.
/// See: https://project-osrm.org/docs/v5.24.0/api/#table-service
/// </summary>
public sealed class TableResponse
{
    /// <summary>Response status code (e.g. "Ok" or "NoTable").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Error message when <see cref="Code"/> is not "Ok".</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Duration matrix in seconds. <c>durations[i][j]</c> gives the travel time from source i to destination j.
    /// <c>null</c> values in the matrix indicate that no route was found.
    /// </summary>
    [JsonPropertyName("durations")]
    public List<List<double?>>? Durations { get; set; }

    /// <summary>
    /// Distance matrix in meters. <c>distances[i][j]</c> gives the distance from source i to destination j.
    /// <c>null</c> values in the matrix indicate that no route was found.
    /// </summary>
    [JsonPropertyName("distances")]
    public List<List<double?>>? Distances { get; set; }

    /// <summary>Snapped source waypoints.</summary>
    [JsonPropertyName("sources")]
    public List<TableWaypoint>? Sources { get; set; }

    /// <summary>Snapped destination waypoints.</summary>
    [JsonPropertyName("destinations")]
    public List<TableWaypoint>? Destinations { get; set; }

    /// <summary>
    /// Indices of cells in the matrix that used the fallback speed.
    /// Only populated when a fallback speed is configured.
    /// </summary>
    [JsonPropertyName("fallback_speed_cells")]
    public List<int>? FallbackSpeedCells { get; set; }

    /// <summary>
    /// Version of the OSM data used to compute the response (e.g. "20240101").
    /// </summary>
    public string? DataVersion { get; set; }
}
