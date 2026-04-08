namespace Sharposrm.Trip;

/// <summary>
/// A snapped waypoint from the OSRM Trip response.
/// Extends the standard waypoint with <see cref="WaypointIndex"/> and <see cref="TripsIndex"/>
/// to identify which trip each waypoint belongs to and its position within that trip.
/// See: https://project-osrm.org/docs/v5.24.0/api/#trip-waypoint-object
/// </summary>
public sealed class TripWaypoint
{
    /// <summary>A [longitude, latitude] pair.</summary>
    public double[]? Location { get; set; }

    /// <summary>Name of the street the coordinate snapped to.</summary>
    public string? Name { get; set; }

    /// <summary>Internal hint for OSRM to speed up future queries to this location.</summary>
    public string? Hint { get; set; }

    /// <summary>
    /// Index of the corresponding trip waypoint in the response's waypoints array.
    /// Identifies which waypoint in the ordered trip this corresponds to.
    /// </summary>
    public int WaypointIndex { get; set; }

    /// <summary>
    /// Index of the trip that contains this waypoint.
    /// When <c>roundtrip=false</c>, multiple trips may be returned.
    /// </summary>
    public int TripsIndex { get; set; }
}
