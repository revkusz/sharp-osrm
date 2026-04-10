using System.Text.Json;
using Sharposrm.Match;
using Sharposrm.Nearest;
using Sharposrm.Route;
using Sharposrm.Table;
using Sharposrm.Trip;
using Xunit;

namespace Sharposrm.Tests;

/// <summary>
/// Pure JSON deserialization tests for OSRM response types — no engine needed.
/// Verifies that newly added properties (LegAnnotation.Metadata, DataVersion, NearestWaypoint.Nodes)
/// round-trip correctly through System.Text.Json with the snake_case naming policy.
/// </summary>
public class ResponseDeserializationTests
{
    private static readonly JsonSerializerOptions Options = RouteResponse.SerializerOptions;

    // --- LegAnnotation.Metadata with datasource_names ---

    [Fact]
    public void LegAnnotation_Deserializes_Metadata_With_DatasourceNames()
    {
        var json = """
        {
            "speed": [10.0, 20.0],
            "duration": [1.0, 2.0],
            "distance": [100.0, 200.0],
            "datasources": [0, 1],
            "nodes": [12345, 67890],
            "metadata": {
                "datasource_names": ["osm", "tomtom"]
            }
        }
        """;

        var annotation = JsonSerializer.Deserialize<LegAnnotation>(json, Options)!;

        Assert.NotNull(annotation.Metadata);
        Assert.NotNull(annotation.Metadata.DatasourceNames);
        Assert.Equal(["osm", "tomtom"], annotation.Metadata.DatasourceNames);
    }

    [Fact]
    public void LegAnnotation_Metadata_Null_When_Absent()
    {
        var json = """
        {
            "speed": [10.0],
            "duration": [1.0],
            "distance": [100.0]
        }
        """;

        var annotation = JsonSerializer.Deserialize<LegAnnotation>(json, Options)!;

        Assert.Null(annotation.Metadata);
    }

    [Fact]
    public void LegAnnotation_Metadata_DatasourceNames_Empty_When_Empty_Array()
    {
        var json = """
        {
            "metadata": {
                "datasource_names": []
            }
        }
        """;

        var annotation = JsonSerializer.Deserialize<LegAnnotation>(json, Options)!;

        Assert.NotNull(annotation.Metadata);
        Assert.NotNull(annotation.Metadata.DatasourceNames);
        Assert.Empty(annotation.Metadata.DatasourceNames);
    }

    // --- DataVersion on all 5 response types ---

    [Fact]
    public void RouteResponse_Deserializes_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20240101"
        }
        """;

        var response = JsonSerializer.Deserialize<RouteResponse>(json, Options)!;

        Assert.Equal("Ok", response.Code);
        Assert.Equal("20240101", response.DataVersion);
    }

    [Fact]
    public void MatchResponse_Deserializes_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20240215"
        }
        """;

        var response = JsonSerializer.Deserialize<MatchResponse>(json, Options)!;

        Assert.Equal("Ok", response.Code);
        Assert.Equal("20240215", response.DataVersion);
    }

    [Fact]
    public void TripResponse_Deserializes_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20250301"
        }
        """;

        var response = JsonSerializer.Deserialize<TripResponse>(json, Options)!;

        Assert.Equal("Ok", response.Code);
        Assert.Equal("20250301", response.DataVersion);
    }

    [Fact]
    public void TableResponse_Deserializes_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20250409"
        }
        """;

        var response = JsonSerializer.Deserialize<TableResponse>(json, Options)!;

        Assert.Equal("Ok", response.Code);
        Assert.Equal("20250409", response.DataVersion);
    }

    [Fact]
    public void NearestResponse_Deserializes_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20231231"
        }
        """;

        var response = JsonSerializer.Deserialize<NearestResponse>(json, Options)!;

        Assert.Equal("Ok", response.Code);
        Assert.Equal("20231231", response.DataVersion);
    }

    [Fact]
    public void DataVersion_Null_When_Absent()
    {
        var json = """{"code": "Ok"}""";

        var response = JsonSerializer.Deserialize<RouteResponse>(json, Options)!;

        Assert.Null(response.DataVersion);
    }

    // --- NearestWaypoint.Nodes ---

    [Fact]
    public void NearestWaypoint_Deserializes_Nodes()
    {
        var json = """
        {
            "location": [7.41337, 43.72956],
            "name": "Boulevard de Suisse",
            "distance": 0.5,
            "nodes": [123456789, 987654321]
        }
        """;

        var waypoint = JsonSerializer.Deserialize<NearestWaypoint>(json, Options)!;

        Assert.NotNull(waypoint.Nodes);
        Assert.Equal([123456789L, 987654321L], waypoint.Nodes);
    }

    [Fact]
    public void NearestWaypoint_Nodes_Null_When_Absent()
    {
        var json = """
        {
            "location": [7.41337, 43.72956],
            "name": "Boulevard de Suisse",
            "distance": 0.5
        }
        """;

        var waypoint = JsonSerializer.Deserialize<NearestWaypoint>(json, Options)!;

        Assert.Null(waypoint.Nodes);
    }

    [Fact]
    public void NearestWaypoint_Nodes_Single_Element()
    {
        var json = """
        {
            "nodes": [42]
        }
        """;

        var waypoint = JsonSerializer.Deserialize<NearestWaypoint>(json, Options)!;

        Assert.NotNull(waypoint.Nodes);
        Assert.Single(waypoint.Nodes);
        Assert.Equal(42L, waypoint.Nodes[0]);
    }

    // --- Integration: full response round-trip ---

    [Fact]
    public void RouteResponse_Full_RoundTrip_With_AnnotationMetadata_And_DataVersion()
    {
        var json = """
        {
            "code": "Ok",
            "data_version": "20240601",
            "routes": [
                {
                    "distance": 1000.0,
                    "duration": 120.0,
                    "legs": [
                        {
                            "distance": 1000.0,
                            "duration": 120.0,
                            "annotation": {
                                "speed": [30.0, 40.0],
                                "datasources": [0, 1],
                                "metadata": {
                                    "datasource_names": ["osm", "internal"]
                                }
                            }
                        }
                    ]
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<RouteResponse>(json, Options)!;

        Assert.Equal("20240601", response.DataVersion);
        Assert.NotNull(response.Routes);
        Assert.Single(response.Routes);
        var leg = response.Routes[0].Legs![0];
        var metadata = leg.Annotation!.Metadata!;
        Assert.NotNull(metadata);
        Assert.Equal(["osm", "internal"], metadata.DatasourceNames);
    }
}
