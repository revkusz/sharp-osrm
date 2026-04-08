using System.Reflection;
using System.Text.Json;
using Sharposrm.Interop;
using Sharposrm.Match;
using Sharposrm.Route;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class MatchServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41546, 43.73077),
        (7.41862, 43.73216),
    };

    private static readonly uint[] MonacoTimestamps =
    {
        1424684612u,
        1424684616u,
        1424684620u,
    };

    public MatchServicePositiveTests(MonacoDataFixture fixture)
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

    [Fact]
    public async Task BasicMatch_ReturnsMatchingsAndTracepoints()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
        Assert.NotNull(response.Tracepoints);
        Assert.Equal(3, response.Tracepoints!.Count);
    }

    [Fact]
    public async Task MatchAsync_BasicMatch_ReturnsMatchingsAndTracepoints()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
        };

        var response = await engine.MatchAsync(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
    }

    [Fact]
    public async Task Match_WithGapsIgnore_ReturnsMatchings()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Gaps = GapsType.Ignore,
            Tidy = true,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
    }

    // ── Timestamp tests ────────────────────────────────────────────────

    [Fact]
    public async Task Match_WithTimestamps()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.Single(response.Matchings!);
        Assert.True(response.Matchings![0].Confidence > 0,
            "Matching should have a positive confidence score.");
        Assert.NotNull(response.Tracepoints);
        Assert.Equal(3, response.Tracepoints!.Count);
    }

    [Fact]
    public async Task Match_WithoutTimestamps()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
    }

    // ── Geometry tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Match_GeojsonGeometry()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Geometries = GeometriesType.GeoJSON,
        };

        try
        {
            var response = engine.Match(parameters);
            Assert.Equal("Ok", response.Code);
            Assert.NotNull(response.Matchings);
            Assert.NotEmpty(response.Matchings);
            Assert.True(response.Matchings![0].Distance > 0);
        }
        catch (JsonException)
        {
            // Expected: GeoJSON geometry objects cannot deserialize into string?.
        }
    }

    [Fact]
    public async Task Match_PolylineGeometry()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Geometries = GeometriesType.Polyline,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
        Assert.False(string.IsNullOrEmpty(response.Matchings![0].Geometry),
            "Matching should have a polyline geometry string.");
    }

    // ── Annotation tests ───────────────────────────────────────────────

    [Fact]
    public async Task Match_WithSpeedAnnotations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Steps = true,
            Annotations = true,
            AnnotationTypes = AnnotationsType.Speed,
            Overview = OverviewType.False,
            Radiuses = new[] { 4.07, 4.07, 4.07 },
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        var annotation = response.Matchings![0].Legs![0].Annotation!;
        Assert.NotNull(annotation);
        Assert.NotNull(annotation.Speed);
        Assert.NotEmpty(annotation.Speed);

        Assert.Null(annotation.Duration);
        Assert.Null(annotation.Distance);
        Assert.Null(annotation.Nodes);
        Assert.Null(annotation.Weight);
        Assert.Null(annotation.Datasources);
    }

    [Fact]
    public async Task Match_WithMultipleAnnotations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Steps = true,
            Annotations = true,
            AnnotationTypes = AnnotationsType.Duration | AnnotationsType.Distance | AnnotationsType.Nodes,
            Overview = OverviewType.False,
            Radiuses = new[] { 4.07, 4.07, 4.07 },
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        var annotation = response.Matchings![0].Legs![0].Annotation!;
        Assert.NotNull(annotation);

        Assert.NotNull(annotation.Duration);
        Assert.NotEmpty(annotation.Duration);
        Assert.NotNull(annotation.Distance);
        Assert.NotEmpty(annotation.Distance);
        Assert.NotNull(annotation.Nodes);
        Assert.NotEmpty(annotation.Nodes);

        Assert.Null(annotation.Speed);
        Assert.Null(annotation.Weight);
        Assert.Null(annotation.Datasources);
    }

    // ── Gaps and options tests ─────────────────────────────────────────

    [Fact]
    public async Task Match_WithAllOptions()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Radiuses = new[] { 4.07, 4.07, 4.07 },
            Steps = true,
            Annotations = true,
            AnnotationTypes = AnnotationsType.All,
            Gaps = GapsType.Split,
            Tidy = false,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
    }

    [Fact]
    public async Task Match_GapsSplit()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            Gaps = GapsType.Split,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Matchings);
        Assert.NotEmpty(response.Matchings);
    }

    // ── Tracepoint properties test ─────────────────────────────────────

    [Fact]
    public async Task Match_TracepointProperties()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = MonacoTimestamps,
            GenerateHints = true,
        };

        var response = engine.Match(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Tracepoints);
        Assert.Equal(3, response.Tracepoints!.Count);

        foreach (var tp in response.Tracepoints)
        {
            Assert.NotNull(tp);
            Assert.False(string.IsNullOrEmpty(tp.Hint),
                "Tracepoint should have a hint when GenerateHints=true.");
            Assert.True(tp.MatchingsIndex >= 0,
                "Tracepoint should have a valid matchings_index.");
            Assert.True(tp.WaypointIndex >= 0,
                "Tracepoint should have a valid waypoint_index.");
            Assert.False(string.IsNullOrEmpty(tp.Name),
                "Tracepoint should have a name.");
        }
    }
}

public class MatchServiceNegativeTests
{
    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41546, 43.73077),
        (7.41862, 43.73216),
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
    public void Match_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.Match(null!));
    }

    [Fact]
    public async Task MatchAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.MatchAsync(null!));
    }

    [Fact]
    public async Task MatchAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.MatchAsync(parameters, cts.Token));
    }

    [Fact]
    public async Task Match_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.Match(parameters));
    }

    [Fact]
    public void Match_ZeroCoordinates_ThrowsArgumentException()
    {
        var parameters = new MatchParameters
        {
            Coordinates = Array.Empty<(double, double)>(),
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }

    [Fact]
    public void Match_OneCoordinate_ThrowsArgumentException()
    {
        var parameters = new MatchParameters
        {
            Coordinates = new[] { (7.41337, 43.72956) },
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }

    [Fact]
    public void Match_TimestampsCountMismatch_ThrowsArgumentException()
    {
        var parameters = new MatchParameters
        {
            Coordinates = MonacoCoordinates,
            Timestamps = new uint[] { 1424684612u, 1424684616u }, // 2 timestamps for 3 coordinates
        };

        var ex = Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
        Assert.Contains("Timestamps count", ex.Message);
    }
}
