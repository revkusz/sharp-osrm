using System.Reflection;
using Sharposrm.Interop;
using Sharposrm.Tile;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

[Collection("MonacoDataSet")]
public class TileServicePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    public TileServicePositiveTests(MonacoDataFixture fixture)
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
    public async Task BasicTile_ReturnsNonEmptyByteArray()
    {
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TileParameters
        {
            X = 17059,
            Y = 11948,
            Z = 15,
        };

        var result = engine.Tile(parameters);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task TileAsync_BasicTile_ReturnsNonEmptyByteArray()
    {
        var config = CreateMonacoConfig();
        await using var engine = await OsrmEngine.CreateAsync(config);

        var parameters = new TileParameters
        {
            X = 17059,
            Y = 11948,
            Z = 15,
        };

        var result = await engine.TileAsync(parameters);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Tile_ReturnsExpectedSize()
    {
        // Tile coordinates for Monaco area at zoom 15
        var config = CreateMonacoConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TileParameters
        {
            X = 17059,
            Y = 11948,
            Z = 15,
        };

        var result = engine.Tile(parameters);

        Assert.NotNull(result);
        Assert.True(result.Length > 0,
            $"Tile at [17059,11948,15] should return non-empty bytes, got {result.Length} bytes.");
    }

    [Fact]
    public async Task Tile_WithMldEngine()
    {
        var config = CreateMldConfig();
        await using var engine = OsrmEngine.Create(config);

        var parameters = new TileParameters
        {
            X = 17059,
            Y = 11948,
            Z = 15,
        };

        var result = engine.Tile(parameters);

        Assert.NotNull(result);
        Assert.True(result.Length > 0,
            "MLD engine should return non-empty tile bytes.");
    }
}

public class TileServiceNegativeTests
{
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
    public void Tile_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        Assert.Throws<ArgumentNullException>(() => engine.Tile(null!));
    }

    [Fact]
    public async Task TileAsync_NullParameters_ThrowsArgumentNullException()
    {
        var engine = CreateDummyEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.TileAsync(null!));
    }

    [Fact]
    public async Task TileAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = CreateDummyEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var parameters = new TileParameters
        {
            X = 17062,
            Y = 11939,
            Z = 15,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.TileAsync(parameters, cts.Token));
    }

    [Fact]
    public async Task Tile_DisposedEngine_ThrowsObjectDisposedException()
    {
        var engine = CreateDummyEngine();
        await engine.DisposeAsync();

        var parameters = new TileParameters
        {
            X = 17062,
            Y = 11939,
            Z = 15,
        };

        Assert.Throws<ObjectDisposedException>(() => engine.Tile(parameters));
    }

    [Fact]
    public void Tile_ZoomTooLow_ThrowsArgumentException()
    {
        var parameters = new TileParameters
        {
            X = 0,
            Y = 0,
            Z = 11,
        };

        var ex = Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
        Assert.Contains("Zoom level must be between 12 and 19", ex.Message);
    }

    [Fact]
    public void Tile_ZoomTooHigh_ThrowsArgumentException()
    {
        var parameters = new TileParameters
        {
            X = 0,
            Y = 0,
            Z = 20,
        };

        var ex = Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
        Assert.Contains("Zoom level must be between 12 and 19", ex.Message);
    }

    [Fact]
    public void Tile_XOutOfRange_ThrowsArgumentException()
    {
        var parameters = new TileParameters
        {
            X = 32768,  // max for Z=15 is 32767, so 32768 is out of range
            Y = 0,
            Z = 15,
        };

        var ex = Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
        Assert.Contains("X", ex.Message);
    }

    [Fact]
    public void Tile_YOutOfRange_ThrowsArgumentException()
    {
        var parameters = new TileParameters
        {
            X = 0,
            Y = 32768,  // max for Z=15 is 32767, so 32768 is out of range
            Z = 15,
        };

        var ex = Assert.ThrowsAny<ArgumentException>(() => parameters.ToNative());
        Assert.Contains("Y", ex.Message);
    }
}
