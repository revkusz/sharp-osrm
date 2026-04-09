using System.Reflection;
using System.Text.Json;
using Sharposrm.Interop;
using Sharposrm.Route;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

/// <summary>
/// Positive tests requiring Monaco test data. Runs in the MonacoDataSet collection
/// which provides the shared MonacoDataFixture.
/// </summary>
[Collection("MonacoDataSet")]
public class RouteServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
    };

    public RouteServicePositiveTests(MonacoDataFixture fixture)
    {
        _fixture = fixture;
    }

    private EngineConfig CreateMonacoConfig()
    {
        return new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
        };
    }

    private EngineConfig CreateMldConfig()
    {
        return new EngineConfig
        {
            StoragePath = _fixture.MldBasePath,
            Algorithm = Algorithm.MLD,
        };
    }

    [Fact]
    public async Task BasicRoute_ReturnsRouteResponse()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);

        var route = response.Routes[0];
        Assert.True(route.Duration > 0, "Route duration should be positive.");
        Assert.True(route.Distance > 0, "Route distance should be positive.");

        Assert.NotNull(response.Waypoints);
        Assert.Equal(2, response.Waypoints.Count);
    }

    [Fact]
    public async Task RouteWithSteps_ReturnsStepsInLegs()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Steps = true,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);

        var legs = response.Routes[0].Legs;
        Assert.NotNull(legs);
        Assert.NotEmpty(legs);

        var steps = legs[0].Steps;
        Assert.NotNull(steps);
        Assert.True(steps.Count >= 2, "Expected at least 2 steps in the first leg.");

        Assert.NotNull(steps[0].Maneuver);
        Assert.False(string.IsNullOrEmpty(steps[0].Maneuver!.Type));
    }

    [Fact]
    public async Task RouteWithAnnotations_ReturnsAnnotations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Annotations = true,
            AnnotationTypes = AnnotationsType.Duration | AnnotationsType.Speed,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);

        var annotation = response.Routes[0].Legs?[0]?.Annotation;
        Assert.NotNull(annotation);
        Assert.NotNull(annotation.Duration);
        Assert.NotEmpty(annotation.Duration);
        Assert.NotNull(annotation.Speed);
        Assert.NotEmpty(annotation.Speed);
    }

    [Fact]
    public async Task RouteAsync_BasicRoute_ReturnsRouteResponse()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = await engine.RouteAsync(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);
        Assert.True(response.Routes![0].Duration > 0);
    }

    // ── Geometry format tests ────────────────────────────────────────

    [Fact]
    public async Task Route_ReturnsPolylineGeometry()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Geometries = GeometriesType.Polyline,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        var route = response.Routes![0];
        Assert.False(string.IsNullOrEmpty(route.Geometry?.Polyline),
            "Polyline geometry should be a non-empty encoded string.");
    }

    [Fact]
    public async Task Route_GeojsonGeometry_Succeeds()
    {
        // When geometries=GeoJSON, OSRM returns geometry as a JSON object
        // rather than an encoded string. RouteGeometry handles both formats.
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Geometries = GeometriesType.GeoJSON,
        };

        var response = engine.Route(parameters);
        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);
        Assert.True(response.Routes[0].Distance > 0);

        // Geometry should be parsed as GeoJSON, not left null
        var geometry = response.Routes[0].Geometry;
        Assert.NotNull(geometry);
        Assert.True(geometry.IsGeoJson);
        Assert.NotNull(geometry.GeoJson);
        Assert.Equal("LineString", geometry.GeoJson.Type);
        Assert.NotEmpty(geometry.GeoJson.Coordinates);
    }

    [Fact]
    public async Task Route_Polyline6InSteps()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Geometries = GeometriesType.Polyline6,
            Overview = OverviewType.False,
            Steps = true,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        var route = response.Routes![0];

        // overview=false means no top-level geometry
        Assert.Null(route.Geometry);

        // But step geometries should be present and non-empty strings
        var steps = route.Legs![0].Steps!;
        Assert.True(steps.Count > 0, "Expected at least one step.");
        foreach (var step in steps)
        {
            Assert.False(string.IsNullOrEmpty(step.Geometry?.Polyline),
                $"Step geometry should be a non-empty Polyline6 string, but was null/empty for step '{step.Name}'.");
        }
    }

    // ── Annotation subset tests ──────────────────────────────────────

    [Fact]
    public async Task Route_WithSpeedAnnotationsOnly()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Annotations = true,
            AnnotationTypes = AnnotationsType.Speed,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        var annotation = response.Routes![0].Legs![0].Annotation!;
        Assert.NotNull(annotation);

        // Speed should be present
        Assert.NotNull(annotation.Speed);
        Assert.NotEmpty(annotation.Speed);

        // All other annotation fields should be null/absent
        Assert.Null(annotation.Duration);
        Assert.Null(annotation.Distance);
        Assert.Null(annotation.Nodes);
        Assert.Null(annotation.Weight);
        Assert.Null(annotation.Datasources);
    }

    [Fact]
    public async Task Route_WithMultipleAnnotations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Annotations = true,
            AnnotationTypes = AnnotationsType.Duration | AnnotationsType.Distance | AnnotationsType.Nodes,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        var annotation = response.Routes![0].Legs![0].Annotation!;
        Assert.NotNull(annotation);

        // Requested annotations should be present
        Assert.NotNull(annotation.Duration);
        Assert.NotEmpty(annotation.Duration);
        Assert.NotNull(annotation.Distance);
        Assert.NotEmpty(annotation.Distance);
        Assert.NotNull(annotation.Nodes);
        Assert.NotEmpty(annotation.Nodes);

        // Non-requested annotations should be absent
        Assert.Null(annotation.Speed);
        Assert.Null(annotation.Weight);
        Assert.Null(annotation.Datasources);
    }

    [Fact]
    public async Task Route_AllAnnotations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Annotations = true,
            AnnotationTypes = AnnotationsType.All,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        var annotation = response.Routes![0].Legs![0].Annotation!;
        Assert.NotNull(annotation);

        // All 6 annotation fields should be present
        Assert.NotNull(annotation.Duration);
        Assert.NotEmpty(annotation.Duration);
        Assert.NotNull(annotation.Nodes);
        Assert.NotEmpty(annotation.Nodes);
        Assert.NotNull(annotation.Distance);
        Assert.NotEmpty(annotation.Distance);
        Assert.NotNull(annotation.Weight);
        Assert.NotEmpty(annotation.Weight);
        Assert.NotNull(annotation.Datasources);
        Assert.NotEmpty(annotation.Datasources);
        Assert.NotNull(annotation.Speed);
        Assert.NotEmpty(annotation.Speed);
    }

    // ── Overview tests ───────────────────────────────────────────────

    [Fact]
    public async Task Route_OverviewFullVsSimplified()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var fullParams = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Overview = OverviewType.Full,
            Geometries = GeometriesType.Polyline,
        };
        var fullResponse = engine.Route(fullParams);

        var simplifiedParams = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Overview = OverviewType.Simplified,
            Geometries = GeometriesType.Polyline,
        };
        var simplifiedResponse = engine.Route(simplifiedParams);

        Assert.Equal("Ok", fullResponse.Code);
        Assert.Equal("Ok", simplifiedResponse.Code);

        var fullGeom = fullResponse.Routes![0].Geometry;
        var simplifiedGeom = simplifiedResponse.Routes![0].Geometry;

        Assert.NotNull(fullGeom);
        Assert.NotNull(simplifiedGeom);
        Assert.False(string.IsNullOrEmpty(fullGeom.Polyline), "Full overview should have geometry.");
        Assert.False(string.IsNullOrEmpty(simplifiedGeom.Polyline), "Simplified overview should have geometry.");

        // Full geometry encodes more detail, so it should be longer (or at least different)
        Assert.NotEqual(fullGeom.Polyline, simplifiedGeom.Polyline);
    }

    // ── Alternatives tests ───────────────────────────────────────────

    [Fact]
    public async Task Route_NoAlternativesByDefault()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Alternatives = false,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.Single(response.Routes);
    }

    [Fact]
    public async Task Route_WithAlternatives()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Alternatives = true,
            NumberOfAlternatives = 3,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.True(response.Routes.Count >= 1,
            "With alternatives=true, at least one route should be returned.");
    }

    // ── Hints tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Route_HintsGeneratedByDefault()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            GenerateHints = true,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Waypoints);

        foreach (var wp in response.Waypoints!)
        {
            Assert.False(string.IsNullOrEmpty(wp.Hint),
                $"Waypoint should have a non-empty hint, but got null/empty for '{wp.Name}'.");
        }
    }

    [Fact]
    public async Task Route_NoHintsWhenDisabled()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            GenerateHints = false,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Waypoints);

        // With hints disabled, waypoints should not have hint strings
        foreach (var wp in response.Waypoints!)
        {
            Assert.True(string.IsNullOrEmpty(wp.Hint),
                $"With GenerateHints=false, waypoint hint should be null/empty, but got '{wp.Hint}'.");
        }
    }

    // ── Radiuses test ────────────────────────────────────────────────

    [Fact]
    public async Task Route_WithRadiuses()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Radiuses = new List<double> { 100, 100 },
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);
        Assert.True(response.Routes[0].Distance > 0);
    }

    // ── MLD algorithm test ───────────────────────────────────────────

    [Fact]
    public async Task Route_MldAlgorithm()
    {
        var config = CreateMldConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);
        Assert.True(response.Routes[0].Duration > 0);
        Assert.True(response.Routes[0].Distance > 0);
    }

    // ── Waypoint indices tests ─────────────────────────────────────────

    private static readonly (double Longitude, double Latitude)[] FourMonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41546, 43.73077),
        (7.41862, 43.73216),
        (7.41983, 43.73115),
    };

    [Fact]
    public async Task RouteWithWaypointIndices_HasFewerLegs()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = FourMonacoCoordinates,
            Waypoints = new List<int> { 0, 3 },
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);

        // With waypoints=[0,3], only the first and last coordinates are stops → 1 leg.
        var legs = response.Routes[0].Legs;
        Assert.NotNull(legs);
        Assert.Single(legs);
    }

    [Fact]
    public async Task RouteWithSteps_NewFieldsDeserialize()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
            Steps = true,
        };

        var response = engine.Route(parameters);
        Assert.Equal("Ok", response.Code);

        var steps = response.Routes![0].Legs![0].Steps!;
        Assert.True(steps.Count >= 2, "Expected at least 2 steps.");

        // DrivingSide should always be present ("right" for Monaco)
        foreach (var step in steps)
        {
            Assert.Equal("right", step.DrivingSide);
        }

        // InIndex/OutIndex should deserialize via [JsonPropertyName("in"/"out")]
        // On non-departure/non-arrival intersections, InIndex and OutIndex should be non-null
        var middleStep = steps.FirstOrDefault(s =>
            s.Maneuver?.Type != "depart" && s.Maneuver?.Type != "arrive");
        if (middleStep is not null)
        {
            var intersections = middleStep.Intersections;
            Assert.NotNull(intersections);
            // At least the first intersection in a middle step should have InIndex/OutIndex
            var nonEntryIntersection = intersections.FirstOrDefault(i => i.InIndex.HasValue);
            Assert.True(nonEntryIntersection is not null,
                "Expected at least one intersection with a non-null InIndex (via [JsonPropertyName(\"in\")]).");
        }

        // Lanes and Classes properties should not cause deserialization errors
        // (they may be null on simple intersections — just verify the properties are accessible)
        foreach (var step in steps)
        {
            if (step.Intersections is null) continue;
            foreach (var intersection in step.Intersections)
            {
                // Access the properties to ensure they deserialized without error
                _ = intersection.Lanes;
                _ = intersection.Classes;
            }
        }
    }

    [Fact]
    public async Task RouteWithoutWaypoints_AllCoordinatesAreStops()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = FourMonacoCoordinates,
        };

        var response = engine.Route(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Routes);
        Assert.NotEmpty(response.Routes);

        // Without waypoints, all 4 coordinates are stops → 3 legs.
        var legs = response.Routes[0].Legs;
        Assert.NotNull(legs);
        Assert.Equal(3, legs.Count);
    }
}

/// <summary>
/// Negative tests that don't require Monaco data — always runnable.
/// </summary>
public class RouteServiceNegativeTests
{
    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
    };

    private static OsrmEngine CreateDummyEngine()
    {
        var handle = new OsrmHandle();
        return (OsrmEngine)Activator.CreateInstance(
            typeof(OsrmEngine),
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { handle },
            culture: null)!;
    }

    [Fact]
    public void Route_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.Route(null!));
    }

    [Fact]
    public async Task RouteAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.RouteAsync(null!));
    }

    [Fact]
    public async Task RouteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.RouteAsync(parameters, cts.Token));
    }

    [Fact]
    public async Task Route_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.Route(parameters));
    }

    [Fact]
    public void Route_ZeroCoordinates_ThrowsArgumentException()
    {
        var parameters = new RouteParameters
        {
            Coordinates = Array.Empty<(double, double)>(),
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }

    [Fact]
    public void Route_OneCoordinate_ThrowsArgumentException()
    {
        var parameters = new RouteParameters
        {
            Coordinates = new[] { (7.41337, 43.72956) },
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }
}
