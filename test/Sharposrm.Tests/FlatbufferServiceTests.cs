using System.Reflection;
using Sharposrm.Interop;
using Sharposrm.Match;
using Sharposrm.Nearest;
using Sharposrm.Route;
using Sharposrm.Table;
using Sharposrm.Trip;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class FlatbufferServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41546, 43.73077),
    };

    public FlatbufferServicePositiveTests(MonacoDataFixture fixture)
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

    /// <summary>
    /// Validates that a byte array starts with a valid flatbuffer prefix.
    /// </summary>
    private static void AssertValidFlatbuffer(byte[] data)
    {
        Assert.NotNull(data);
        Assert.True(data.Length >= 4, $"Flatbuffer should be at least 4 bytes, got {data.Length}.");
        int rootOffset = BitConverter.ToInt32(data, 0);
        Assert.True(rootOffset > 0 && rootOffset < data.Length,
            $"Flatbuffer root offset {rootOffset} should be within buffer bounds (0, {data.Length}).");
    }

    [Fact]
    public async Task RouteFlatbuffer_BasicRoute()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        byte[] result = engine.RouteFlatbuffer(parameters);

        Assert.NotEmpty(result);
        AssertValidFlatbuffer(result);
    }

    [Fact]
    public async Task TableFlatbuffer_BasicTable()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TableParameters
        {
            Coordinates = MonacoCoordinates,
        };

        byte[] result = engine.TableFlatbuffer(parameters);

        Assert.NotEmpty(result);
        AssertValidFlatbuffer(result);
    }

    [Fact]
    public async Task NearestFlatbuffer_BasicNearest()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new NearestParameters
        {
            Coordinates = new[] { MonacoCoordinates[0] },
        };

        byte[] result = engine.NearestFlatbuffer(parameters);

        Assert.NotEmpty(result);
        AssertValidFlatbuffer(result);
    }

    [Fact]
    public async Task TripFlatbuffer_BasicTrip()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TripParameters
        {
            Coordinates = MonacoCoordinates,
        };

        byte[] result = engine.TripFlatbuffer(parameters);

        Assert.NotEmpty(result);
        AssertValidFlatbuffer(result);
    }

    [Fact]
    public async Task MatchFlatbuffer_BasicMatch()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new MatchParameters
        {
            Coordinates = new[] { (7.41337, 43.72956), (7.41546, 43.73077), (7.41862, 43.73216) },
            Timestamps = new uint[] { 1424684612, 1424684616, 1424684620 },
        };

        byte[] result = engine.MatchFlatbuffer(parameters);

        Assert.NotEmpty(result);
        AssertValidFlatbuffer(result);
    }
}

public class FlatbufferServiceNegativeTests
{
    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41546, 43.73077),
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
    public void RouteFlatbuffer_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.RouteFlatbuffer(null!));
    }

    [Fact]
    public async Task RouteFlatbufferAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.RouteFlatbufferAsync(null!));
    }

    [Fact]
    public async Task RouteFlatbuffer_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.RouteFlatbuffer(parameters));
    }

    [Fact]
    public async Task RouteFlatbufferAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new RouteParameters
        {
            Coordinates = MonacoCoordinates,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.RouteFlatbufferAsync(parameters, cts.Token));
    }

    [Fact]
    public void RouteFlatbuffer_ZeroCoordinates_ThrowsArgumentException()
    {
        var parameters = new RouteParameters
        {
            Coordinates = Array.Empty<(double, double)>(),
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }

    [Fact]
    public void TableFlatbuffer_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.TableFlatbuffer(null!));
    }

    [Fact]
    public void NearestFlatbuffer_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.NearestFlatbuffer(null!));
    }

    [Fact]
    public void TripFlatbuffer_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.TripFlatbuffer(null!));
    }

    [Fact]
    public void MatchFlatbuffer_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.MatchFlatbuffer(null!));
    }
}
