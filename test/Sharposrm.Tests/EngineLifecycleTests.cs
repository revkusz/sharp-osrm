using Sharposrm.Interop;
using Sharposrm.Route;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class EngineLifecyclePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    public EngineLifecyclePositiveTests(MonacoDataFixture fixture)
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
    public async Task CreateAndDestroy()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        Assert.NotNull(engine);
        Assert.False(engine.IsDisposed);
    }

    [Fact]
    public async Task AsyncCreation()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        Assert.NotNull(engine);
        Assert.False(engine.IsDisposed);
    }

    [Fact]
    public async Task DoubleDisposeIsSafe()
    {
        var config = CreateMonacoConfig();
        var engine = OsrmEngine.Create(config);

        await engine.DisposeAsync();
        Assert.True(engine.IsDisposed);

        await engine.DisposeAsync();
        Assert.True(engine.IsDisposed);
    }

    // ── Algorithm loading tests ────────────────────────────────────────

    [Fact]
    public async Task CreateEngineWithCHAlgorithm()
    {
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
        };

        await using var engine = OsrmEngine.Create(config);
        Assert.NotNull(engine);
        Assert.False(engine.IsDisposed);
    }

    [Fact]
    public async Task CreateEngineWithMLDAlgorithm()
    {
        var config = new EngineConfig
        {
            StoragePath = _fixture.MldBasePath,
            Algorithm = Algorithm.MLD,
        };

        await using var engine = OsrmEngine.Create(config);
        Assert.NotNull(engine);
        Assert.False(engine.IsDisposed);
    }

    // ── Custom limits test ─────────────────────────────────────────────

    [Fact]
    public async Task CreateEngineWithCustomLimits()
    {
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
            MaxAlternatives = 10,
            MaxLocationsTrip = 5,
            MaxLocationsViaroute = 10,
            MaxLocationsDistanceTable = 20,
            MaxLocationsMapMatching = 10,
            MaxResultsNearest = 5,
            DefaultRadius = 1000.0,
        };

        await using var engine = OsrmEngine.Create(config);
        Assert.NotNull(engine);
        Assert.False(engine.IsDisposed);
    }

    // ── Disable feature dataset tests ──────────────────────────────────

    [Fact]
    public async Task CreateEngineWithDisabledRouteGeometry()
    {
        // When DisableRouteGeometry=true, routing that needs geometry data should fail.
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
            DisableRouteGeometry = true,
        };

        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = new[] { (7.41337, 43.72956), (7.41546, 43.73077), (7.41862, 43.73216) },
        };

        var ex = Assert.Throws<OsrmException>(() => engine.Route(parameters));
        // The error message from OSRM contains dataset-related info
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task CreateEngineWithDisabledRouteGeometryOk()
    {
        // When DisableRouteGeometry=true, OSRM excludes .osrm.edges (TurnData) from loading.
        // Even with steps=false, overview=false, the routing algorithm still needs TurnData
        // for basic path computation. This is expected — disabling ROUTE_GEOMETRY is a
        // significant optimization that prevents the route service from working entirely.
        // The "ok" variant only works if the dataset isn't actually needed (e.g., match
        // with the right options).
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
            DisableRouteGeometry = true,
        };

        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = new[] { (7.41337, 43.72956), (7.41546, 43.73077), (7.41862, 43.73216) },
            Steps = false,
            Overview = OverviewType.False,
            Annotations = false,
            SkipWaypoints = true,
        };

        // Even with all geometry options off, OSRM still needs TurnData for routing
        var ex = Assert.Throws<OsrmException>(() => engine.Route(parameters));
        Assert.Contains("DisabledDataset", ex.Message);
    }

    [Fact]
    public async Task CreateEngineWithDisabledRouteSteps()
    {
        // When DisableRouteSteps=true, routes requesting steps should fail.
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
            DisableRouteSteps = true,
        };

        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = new[] { (7.41337, 43.72956), (7.41546, 43.73077), (7.41862, 43.73216) },
            Steps = true,
        };

        var ex = Assert.Throws<OsrmException>(() => engine.Route(parameters));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task CreateEngineWithDisabledRouteStepsOk()
    {
        // When DisableRouteSteps=true but steps=false, route should succeed.
        var config = new EngineConfig
        {
            StoragePath = _fixture.ChBasePath,
            Algorithm = Algorithm.CH,
            DisableRouteSteps = true,
        };

        await using var engine = OsrmEngine.Create(config);

        var parameters = new RouteParameters
        {
            Coordinates = new[] { (7.41337, 43.72956), (7.41546, 43.73077), (7.41862, 43.73216) },
            Steps = false,
            Overview = OverviewType.Simplified,
            Annotations = true,
        };

        var response = engine.Route(parameters);
        Assert.Equal("Ok", response.Code);
    }
}

public class EngineLifecycleNegativeTests
{
    [Fact]
    public void InvalidConfigThrows()
    {
        var config = new EngineConfig
        {
            StoragePath = "/nonexistent/path/that/does/not/exist.osrm",
            Algorithm = Algorithm.CH,
        };

        var ex = Assert.Throws<OsrmException>(() => OsrmEngine.Create(config));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void Create_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => OsrmEngine.Create(null!));
    }

    [Fact]
    public async Task CreateAsync_NullConfig_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => OsrmEngine.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var config = new EngineConfig
        {
            StoragePath = "/nonexistent/path.osrm",
            Algorithm = Algorithm.CH,
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => OsrmEngine.CreateAsync(config, cts.Token));
    }
}
