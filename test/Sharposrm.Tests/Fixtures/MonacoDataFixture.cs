using System.Diagnostics;
using Sharposrm.Pipeline;

namespace Sharposrm.Tests.Fixtures;

/// <summary>
/// xUnit assembly-level fixture that generates both CH and MLD OSRM test data
/// from the Monaco OSM PBF file once per test run.
/// <para>
/// Uses <see cref="ICollectionFixture{MonacoDataFixture}"/> via
/// <see cref="MonacoFixtureCollection"/> to share data across all test classes
/// in the "MonacoDataSet" collection.
/// </para>
/// </summary>
public class MonacoDataFixture : IDisposable
{
    /// <summary>
    /// Base path (without extension) for CH-contracted Monaco data.
    /// Suitable for <c>EngineConfig.StoragePath</c> with <c>Algorithm.CH</c>.
    /// </summary>
    public string ChBasePath { get; }

    /// <summary>
    /// Base path (without extension) for MLD-customized Monaco data.
    /// Suitable for <c>EngineConfig.StoragePath</c> with <c>Algorithm.MLD</c>.
    /// </summary>
    public string MldBasePath { get; }

    /// <summary>
    /// Temp directory containing all generated data. Deleted on dispose.
    /// </summary>
    private readonly string _tempDir;

    public MonacoDataFixture()
    {
        var sw = Stopwatch.StartNew();

        // Source paths — resolve relative to the repo root, not the test bin directory.
        // Walk up from the test assembly location to find the repo root (identified by the .sln file).
        string assemblyDir = AppContext.BaseDirectory;
        string? repoRoot = assemblyDir;
        while (repoRoot is not null)
        {
            if (File.Exists(Path.Combine(repoRoot, "Sharposrm.sln")))
                break;
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (repoRoot is null)
            throw new InvalidOperationException("Could not locate repo root (Sharposrm.sln not found).");

        string monacoPbf = Path.Combine(repoRoot, "osrm-backend", "test", "data", "monaco.osm.pbf");

        // Prefer the Homebrew-installed car.lua which matches the compiled OSRM library version.
        // The submodule's car.lua may reference Lua APIs (e.g., Obstacle.new with 4 args for barrier_penalties)
        // that are only available in the OSRM version it was bundled with, not the installed one.
        string homebrewProfile = "/opt/homebrew/share/osrm/profiles/car.lua";
        string submoduleProfile = Path.Combine(repoRoot, "osrm-backend", "profiles", "car.lua");
        string carProfile = File.Exists(homebrewProfile) ? homebrewProfile : submoduleProfile;

        if (!File.Exists(monacoPbf))
            throw new InvalidOperationException(
                $"Monaco OSM PBF not found at '{monacoPbf}'. Ensure the osrm-backend submodule is initialized.");

        if (!File.Exists(carProfile))
            throw new InvalidOperationException(
                $"Car profile not found at '{carProfile}'. Ensure the osrm-backend submodule is initialized or Homebrew osrm-backend is installed.");

        // Create a unique temp directory to avoid collisions across parallel runs
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharposrm-monaco-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        Console.WriteLine($"[MonacoDataFixture] Temp directory: {_tempDir}");

        // ── CH path: extract → contract ──────────────────────────────────
        string chDir = Path.Combine(_tempDir, "ch");
        Directory.CreateDirectory(chDir);
        ChBasePath = Path.Combine(chDir, "monaco");

        PipelineStage("Extract (CH)", () =>
        {
            OsrmPipeline.Extract(new ExtractorConfig
            {
                InputPath = monacoPbf,
                ProfilePath = carProfile,
                OutputPath = ChBasePath,
                RequestedThreads = 1,
            });
        });

        PipelineStage("Contract (CH)", () =>
        {
            // OSRM's CH contractor can overflow the default .NET thread stack (1MB on macOS).
            // Run it on a dedicated thread with an 8MB stack to avoid StackOverflowException.
            RunWithLargeStack(() =>
            {
                OsrmPipeline.Contract(new ContractorConfig
                {
                    BasePath = ChBasePath,
                    RequestedThreads = 1,
                });
            });
        });

        // ── MLD path: extract → partition → customize ────────────────────
        string mldDir = Path.Combine(_tempDir, "mld");
        Directory.CreateDirectory(mldDir);
        MldBasePath = Path.Combine(mldDir, "monaco");

        PipelineStage("Extract (MLD)", () =>
        {
            OsrmPipeline.Extract(new ExtractorConfig
            {
                InputPath = monacoPbf,
                ProfilePath = carProfile,
                OutputPath = MldBasePath,
                RequestedThreads = 1,
            });
        });

        PipelineStage("Partition (MLD)", () =>
        {
            OsrmPipeline.Partition(new PartitionerConfig
            {
                BasePath = MldBasePath,
                RequestedThreads = 1,
            });
        });

        PipelineStage("Customize (MLD)", () =>
        {
            OsrmPipeline.Customize(new CustomizerConfig
            {
                BasePath = MldBasePath,
                RequestedThreads = 1,
            });
        });

        sw.Stop();
        Console.WriteLine($"[MonacoDataFixture] All pipeline stages completed in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Runs an action on a dedicated thread with an 8MB stack.
    /// OSRM's contraction algorithm is deeply recursive and overflows the default .NET thread stack.
    /// </summary>
    private static void RunWithLargeStack(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        }, maxStackSize: 8 * 1024 * 1024); // 8MB stack
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (captured is not null)
            throw captured; // Re-throw on the calling thread for proper error reporting
    }

    /// <summary>
    /// Runs a pipeline stage, catching errors and wrapping them with stage-specific context.
    /// </summary>
    private static void PipelineStage(string stageName, Action action)
    {
        var stageSw = Stopwatch.StartNew();
        try
        {
            action();
            stageSw.Stop();
            Console.WriteLine($"[MonacoDataFixture] {stageName} completed in {stageSw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            stageSw.Stop();
            Console.WriteLine($"[MonacoDataFixture] {stageName} FAILED after {stageSw.ElapsedMilliseconds}ms: {ex.Message}");
            throw new InvalidOperationException(
                $"MonacoDataFixture failed during pipeline stage '{stageName}'. " +
                $"See inner exception for details.", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
                Console.WriteLine($"[MonacoDataFixture] Cleaned up temp directory: {_tempDir}");
            }
        }
        catch (IOException ex)
        {
            // Best-effort cleanup; don't mask test failures.
            Console.WriteLine($"[MonacoDataFixture] Warning: could not delete temp directory: {ex.Message}");
        }
    }
}
