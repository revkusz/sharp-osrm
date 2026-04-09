using System.Reflection;
using Sharposrm.Interop;
using Sharposrm.Nearest;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class NearestServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
    };

    public NearestServicePositiveTests(MonacoDataFixture fixture)
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
    public async Task BasicNearest_ReturnsWaypoints()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new NearestParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = engine.Nearest(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Waypoints);
        Assert.Single(response.Waypoints!);

        var wp = response.Waypoints![0];
        Assert.NotNull(wp.Location);
        Assert.Equal(2, wp.Location!.Length);
        Assert.False(string.IsNullOrEmpty(wp.Name));
    }

    [Fact]
    public async Task NearestAsync_BasicNearest_ReturnsWaypoints()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        var parameters = new NearestParameters
        {
            Coordinates = MonacoCoordinates,
        };

        var response = await engine.NearestAsync(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Waypoints);
        Assert.NotEmpty(response.Waypoints!);
    }

    [Fact]
    public async Task Nearest_MultipleResults_ReturnsCorrectCount()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new NearestParameters
        {
            Coordinates = MonacoCoordinates,
            NumberOfResults = 3,
        };

        var response = engine.Nearest(parameters);

        Assert.Equal("Ok", response.Code);
        Assert.NotNull(response.Waypoints);
        Assert.Equal(3, response.Waypoints!.Count);
    }
}

public class NearestServiceNegativeTests
{
    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
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
    public void Nearest_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.Nearest(null!));
    }

    [Fact]
    public async Task NearestAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.NearestAsync(null!));
    }

    [Fact]
    public async Task NearestAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new NearestParameters
        {
            Coordinates = MonacoCoordinates,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.NearestAsync(parameters, cts.Token));
    }

    [Fact]
    public async Task Nearest_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new NearestParameters
        {
            Coordinates = MonacoCoordinates,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.Nearest(parameters));
    }

    [Fact]
    public void Nearest_ZeroCoordinates_ThrowsArgumentException()
    {
        var parameters = new NearestParameters
        {
            Coordinates = Array.Empty<(double, double)>(),
        };

        Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
    }
}
