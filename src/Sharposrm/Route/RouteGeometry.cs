using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharposrm.Route;

/// <summary>
/// Route geometry that can be either a polyline-encoded string or a GeoJSON LineString object.
/// OSRM returns different shapes depending on the <see cref="GeometriesType"/> request parameter:
/// <list type="bullet">
///   <item><see cref="GeometriesType.Polyline"/> or <see cref="GeometriesType.Polyline6"/> → <see cref="Polyline"/> is set</item>
///   <item><see cref="GeometriesType.GeoJSON"/> → <see cref="GeoJson"/> is set</item>
/// </list>
/// Use <see cref="IsGeoJson"/> to determine which format was returned.
/// </summary>
[JsonConverter(typeof(RouteGeometryJsonConverter))]
public sealed class RouteGeometry
{
    /// <summary>
    /// Polyline-encoded geometry string (5 or 6 digit precision).
    /// Set when <see cref="GeometriesType"/> is Polyline or Polyline6.
    /// </summary>
    public string? Polyline { get; init; }

    /// <summary>
    /// GeoJSON LineString geometry.
    /// Set when <see cref="GeometriesType"/> is GeoJSON.
    /// </summary>
    public GeoJsonLineString? GeoJson { get; init; }

    /// <summary>
    /// Whether this geometry is a GeoJSON object (as opposed to a polyline string).
    /// </summary>
    [JsonIgnore]
    public bool IsGeoJson => GeoJson is not null;

    /// <summary>
    /// Implicit conversion from a polyline string to <see cref="RouteGeometry"/>.
    /// </summary>
    public static implicit operator RouteGeometry?(string? polyline) =>
        polyline is null ? null : new RouteGeometry { Polyline = polyline };

    /// <summary>
    /// Implicit conversion from <see cref="GeoJsonLineString"/> to <see cref="RouteGeometry"/>.
    /// </summary>
    public static implicit operator RouteGeometry?(GeoJsonLineString? geoJson) =>
        geoJson is null ? null : new RouteGeometry { GeoJson = geoJson };
}

/// <summary>
/// A GeoJSON LineString geometry as returned by OSRM when <see cref="GeometriesType.GeoJSON"/> is requested.
/// </summary>
public sealed class GeoJsonLineString
{
    /// <summary>Always "LineString".</summary>
    public string Type { get; set; } = "LineString";

    /// <summary>
    /// Array of [longitude, latitude] coordinate pairs.
    /// </summary>
    public List<List<double>> Coordinates { get; set; } = new();
}

/// <summary>
/// Custom JSON converter for <see cref="RouteGeometry"/> that handles both
/// polyline strings and GeoJSON objects based on the token type.
/// </summary>
internal sealed class RouteGeometryJsonConverter : JsonConverter<RouteGeometry?>
{
    public override RouteGeometry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            string? polyline = reader.GetString();
            return new RouteGeometry { Polyline = polyline };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var geoJson = JsonSerializer.Deserialize<GeoJsonLineString>(ref reader, options);
            return new RouteGeometry { GeoJson = geoJson };
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for RouteGeometry. Expected String or StartObject.");
    }

    public override void Write(Utf8JsonWriter writer, RouteGeometry? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.IsGeoJson && value.GeoJson is not null)
        {
            JsonSerializer.Serialize(writer, value.GeoJson, options);
        }
        else
        {
            writer.WriteStringValue(value.Polyline);
        }
    }
}
