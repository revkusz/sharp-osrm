using System.Diagnostics;
using Sharposrm;
using Sharposrm.Pipeline;
using Sharposrm.Route;
using Xunit;

namespace Sharposrm.IntegrationTests;

/// <summary>
/// Integration tests that consume Sharposrm via NuGet PackageReference
/// (not ProjectReference) to prove the distributable package works end-to-end:
/// managed API loads, native library resolves from runtimes/{rid}/native/,
/// pipeline functions work, and routing returns valid results.
/// </summary>
public class NuGetPackageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _chBasePath;

    // Two Monaco coordinates for a short route
    private static readonly (double Longitude, double Latitude)[] MonacoCoordinates =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
    };

    public NuGetPackageTests()
    {
        // Find repo root by walking up from the test assembly directory
        string assemblyDir = AppContext.BaseDirectory;
        string? repoRoot = assemblyDir;
        while (repoRoot is not null)
        {
            if (File.Exists(Path.Combine(repoRoot, "Sharposrm.sln")))
                break;
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (repoRoot is null)
            throw new InvalidOperationException(
                "Could not locate repo root (Sharposrm.sln not found). " +
                "Ensure the repository is checked out with submodules.");

        string monacoPbf = Path.Combine(repoRoot, "osrm-backend", "test", "data", "monaco.osm.pbf");
        string carProfile = Path.Combine(repoRoot, "osrm-backend", "profiles", "car.lua");

        if (!File.Exists(monacoPbf))
            throw new InvalidOperationException(
                $"Monaco OSM PBF not found at '{monacoPbf}'. " +
                "Ensure the osrm-backend submodule is initialized.");

        if (!File.Exists(carProfile))
            throw new InvalidOperationException(
                $"Car profile not found at '{carProfile}'. " +
                "Ensure the osrm-backend submodule is initialized.");

        // Create temp directory for CH pipeline output
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharposrm-inttest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        string chDir = Path.Combine(_tempDir, "ch");
        Directory.CreateDirectory(chDir);
        _chBasePath = Path.Combine(chDir, "monaco");

        Console.WriteLine($"[NuGetPackageTests] Temp directory: {_tempDir}");

        // ── CH path: extract → contract ──────────────────────────────
        PipelineStage("Extract (CH)", () =>
        {
            OsrmPipeline.Extract(new ExtractorConfig
            {
                InputPath = monacoPbf,
                ProfilePath = carProfile,
                OutputPath = _chBasePath,
                RequestedThreads = 1,
            });
        });

        PipelineStage("Contract (CH)", () =>
        {
            // OSRM's CH contractor is deeply recursive and needs >1MB stack.
            RunWithLargeStack(() =>
            {
                OsrmPipeline.Contract(new ContractorConfig
                {
                    BasePath = _chBasePath,
                    RequestedThreads = 1,
                });
            });
        });
    }

    [Fact]
    public async Task Route_ReturnsValidResponse()
    {
        // This single test proves the full NuGet package consumption path:
        // 1. Managed assembly loads (Sharposrm.dll)
        // 2. Native library loads from runtimes/{rid}/native/
        // 3. Pipeline functions work (extract + contract ran in ctor)
        // 4. Engine creation works
        // 5. Routing returns valid results
        var config = new EngineConfig
        {
            StoragePath = _chBasePath,
            Algorithm = Algorithm.CH,
        };

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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
                Console.WriteLine($"[NuGetPackageTests] Cleaned up temp directory: {_tempDir}");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[NuGetPackageTests] Warning: could not delete temp directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs an action on a dedicated thread with an 8MB stack to accommodate
    /// OSRM's deeply recursive contraction algorithm.
    /// </summary>
    private static void RunWithLargeStack(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        }, maxStackSize: 8 * 1024 * 1024);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (captured is not null)
            throw captured;
    }

    private static void PipelineStage(string stageName, Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
            sw.Stop();
            Console.WriteLine($"[NuGetPackageTests] {stageName} completed in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"[NuGetPackageTests] {stageName} FAILED after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            throw new InvalidOperationException(
                $"Integration test setup failed during pipeline stage '{stageName}'. " +
                $"See inner exception for details.", ex);
        }
    }
}
