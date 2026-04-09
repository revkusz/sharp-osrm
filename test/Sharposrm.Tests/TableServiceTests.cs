using System.Reflection;
using Sharposrm.Interop;
using Sharposrm.Table;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class TableServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
    };

    public TableServicePositiveTests(MonacoDataFixture fixture)
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
    public async Task BasicTable_ReturnsDurationsMatrix()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.Equal(2, response.Durations!.Count);
        Assert.Equal(2, response.Durations[0].Count);

        // Diagonal should be zero (same source/destination)
        Assert.Equal(0.0, response.Durations[0][0]);
        Assert.Equal(0.0, response.Durations[1][1]);
    }

    [Fact]
    public async Task TableAsync_BasicTable_ReturnsDurationsMatrix()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = await engine.TableAsync(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.NotEmpty(response.Durations);
    }

    [Fact]
    public async Task TableWithSourcesAndDestinations_ReturnsSubset()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var coords = new (double Longitude, double Latitude)[]
        {
            (7.41337, 43.72956),
            (7.41983, 43.73115),
            (7.42773, 43.73680),
        };

        var parameters = new TableParameters
        {
            Coordinates = coords,
            Sources = new[] { 0 },
            Destinations = new[] { 1, 2 },
            AnnotationsType = TableAnnotationsType.Duration | TableAnnotationsType.Distance,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.Single(response.Durations!);  // 1 source
        Assert.Equal(2, response.Durations[0].Count); // 2 destinations
        Assert.NotNull(response.Distances);
        Assert.Single(response.Distances!);
        Assert.Equal(2, response.Distances[0].Count);
    }

    [Fact]
    public async Task Table_AnnotationsDistanceOnly_ReturnsDistancesWithoutDurations()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            AnnotationsType = TableAnnotationsType.Distance,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Distances);
        Assert.Equal(2, response.Distances!.Count);
        Assert.Equal(2, response.Distances[0].Count);
        // Distances should have finite values on the off-diagonal
        Assert.True(response.Distances[0][1].HasValue);
        Assert.True(double.IsFinite(response.Distances[0][1]!.Value));
        // Durations should be null when only distance is requested
        Assert.Null(response.Durations);
    }

    [Fact]
    public async Task Table_AnnotationsDurationOnly_ReturnsDurationsWithoutDistances()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            AnnotationsType = TableAnnotationsType.Duration,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.Equal(2, response.Durations!.Count);
        // Distances should be null when only duration is requested
        Assert.Null(response.Distances);
    }

    [Fact]
    public async Task Table_AnnotationsBoth_ReturnsDurationsAndDistances()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            AnnotationsType = TableAnnotationsType.Duration | TableAnnotationsType.Distance,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.Equal(2, response.Durations!.Count);
        Assert.NotNull(response.Distances);
        Assert.Equal(2, response.Distances!.Count);
    }

    [Fact]
    public async Task Table_DefaultAnnotations_ReturnsDurationsOnly()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            // AnnotationsType not set — defaults to Duration
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        Assert.Equal(2, response.Durations!.Count);
        // Default annotation is Duration only; distances should be null
        Assert.Null(response.Distances);
    }

    [Fact]
    public async Task Table_DurationsMatrixStructure_DiagonalZeroOffDiagonalNonZero()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            AnnotationsType = TableAnnotationsType.Duration,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Durations);
        // 2x2 matrix
        Assert.Equal(2, response.Durations!.Count);
        Assert.Equal(2, response.Durations[0].Count);
        Assert.Equal(2, response.Durations[1].Count);
        // Diagonal is zero (same source and destination)
        Assert.Equal(0.0, response.Durations[0][0]!.Value);
        Assert.Equal(0.0, response.Durations[1][1]!.Value);
        // Off-diagonal is non-zero and finite
        Assert.True(response.Durations[0][1]!.Value > 0);
        Assert.True(double.IsFinite(response.Durations[0][1]!.Value));
        Assert.True(response.Durations[1][0]!.Value > 0);
        Assert.True(double.IsFinite(response.Durations[1][0]!.Value));
    }

    [Fact]
    public async Task Table_DistancesMatrixStructure_DiagonalZeroOffDiagonalNonZero()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            AnnotationsType = TableAnnotationsType.Distance,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Distances);
        // 2x2 matrix
        Assert.Equal(2, response.Distances!.Count);
        Assert.Equal(2, response.Distances[0].Count);
        Assert.Equal(2, response.Distances[1].Count);
        // Diagonal is zero
        Assert.Equal(0.0, response.Distances[0][0]!.Value);
        Assert.Equal(0.0, response.Distances[1][1]!.Value);
        // Off-diagonal is non-zero and finite
        Assert.True(response.Distances[0][1]!.Value > 0);
        Assert.True(double.IsFinite(response.Distances[0][1]!.Value));
        Assert.True(response.Distances[1][0]!.Value > 0);
        Assert.True(double.IsFinite(response.Distances[1][0]!.Value));
    }

    [Fact]
    public async Task Table_GenerateHintsTrue_WaypointsHaveHints()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            GenerateHints = true,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Sources);
        Assert.NotNull(response.Destinations);
        // All sources and destinations should have non-empty hints
        foreach (var src in response.Sources!)
        {
            Assert.False(string.IsNullOrEmpty(src.Hint));
        }

        foreach (var dest in response.Destinations!)
        {
            Assert.False(string.IsNullOrEmpty(dest.Hint));
        }
    }

    [Fact]
    public async Task Table_GenerateHintsFalse_WaypointsHaveNoHints()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            GenerateHints = false,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Sources);
        Assert.NotNull(response.Destinations);
        // All hints should be null or empty
        foreach (var src in response.Sources!)
        {
            Assert.True(string.IsNullOrEmpty(src.Hint));
        }

        foreach (var dest in response.Destinations!)
        {
            Assert.True(string.IsNullOrEmpty(dest.Hint));
        }
    }

    [Fact]
    public async Task Table_SkipWaypoints_SourcesAndDestinationsAreNull()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
            SkipWaypoints = true,
        };

        var response = engine.Table(parameters);

        Assert.Equal("Ok", response.Code);
        // When skip_waypoints is true, sources and destinations should be null
        Assert.Null(response.Sources);
        Assert.Null(response.Destinations);
        // Durations should still be present
        Assert.NotNull(response.Durations);
    }
}

public class TableServiceNegativeTests
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
            args: new object[] { handle, Environment.ProcessorCount },
            culture: null)!;
    }

    [Fact]
    public void Table_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.Table(null!));
    }

    [Fact]
    public async Task TableAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.TableAsync(null!));
    }

    [Fact]
    public async Task TableAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.TableAsync(parameters, cts.Token));
    }

    [Fact]
    public async Task Table_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.Table(parameters));
    }

    [Fact]
    public void Table_ZeroCoordinates_ThrowsArgumentException()
    {
        var parameters = new TableParameters
        {
            Coordinates = Array.Empty<(double, double)>(),
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }

    [Fact]
    public void Table_OneCoordinate_ThrowsArgumentException()
    {
        var parameters = new TableParameters
        {
            Coordinates = new[] { (7.41337, 43.72956) },
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }
}
