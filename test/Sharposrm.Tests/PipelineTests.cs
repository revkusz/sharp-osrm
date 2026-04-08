using Sharposrm.Pipeline;
using Sharposrm.Route;
using Sharposrm.Tests.Fixtures;
using Xunit;

namespace Sharposrm.Tests;

/// <summary>
/// Pipeline tests that generate Monaco data from the PBF. Runs in the MonacoDataSet collection
/// which provides the shared MonacoDataFixture (these tests re-extract to verify pipeline stages).
/// </summary>
[Collection("MonacoDataSet")]
public class PipelinePositiveTests
{
    private readonly MonacoDataFixture _fixture;

    private const string MonacoOsmPbf = "osrm-backend/test/data/monaco.osm.pbf";

    // Prefer Homebrew's car.lua which matches the installed OSRM library version.
    // The submodule car.lua may use Lua APIs not available in the installed Sol2 binding.
    private const string HomebrewCarProfile = "/opt/homebrew/share/osrm/profiles/car.lua";
    private const string SubmoduleCarProfile = "osrm-backend/profiles/car.lua";

    private static string CarProfilePath => File.Exists(HomebrewCarProfile)
        ? HomebrewCarProfile
        : Path.Combine(RepoRoot, SubmoduleCarProfile);

    // Resolve paths relative to repo root (not test bin directory).
    private static readonly string RepoRoot = FindRepoRoot();

    public PipelinePositiveTests(MonacoDataFixture fixture)
    {
        _fixture = fixture;
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Sharposrm.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return AppContext.BaseDirectory;
    }

    private static string CreateTempOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sharposrm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetOsrmBasePath(string outputDir)
    {
        return Path.Combine(outputDir, "monaco");
    }

    private static void CleanupDirectory(string? path)
    {
        if (path is null) return;
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; don't mask test failures.
        }
    }

    [Fact]
    public void ExtractMonaco()
    {
        string? outputDir = null;
        try
        {
            outputDir = CreateTempOutputDirectory();
            var basePath = GetOsrmBasePath(outputDir);

            var config = new ExtractorConfig
            {
                InputPath = Path.Combine(RepoRoot, MonacoOsmPbf),
                ProfilePath = CarProfilePath,
                OutputPath = basePath,
                RequestedThreads = 1,
            };

            OsrmPipeline.Extract(config);

            Assert.True(File.Exists(basePath + ".osrm.cnbg"), "Expected .osrm.cnbg file after extraction.");
            Assert.True(File.Exists(basePath + ".osrm.ebg"), "Expected .osrm.ebg file after extraction.");
        }
        finally
        {
            CleanupDirectory(outputDir);
        }
    }

    [Fact]
    public void PartitionMonaco()
    {
        string? outputDir = null;
        try
        {
            outputDir = CreateTempOutputDirectory();
            var basePath = GetOsrmBasePath(outputDir);

            var extractConfig = new ExtractorConfig
            {
                InputPath = Path.Combine(RepoRoot, MonacoOsmPbf),
                ProfilePath = CarProfilePath,
                OutputPath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Extract(extractConfig);

            var partitionConfig = new PartitionerConfig
            {
                BasePath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Partition(partitionConfig);

            Assert.True(File.Exists(basePath + ".osrm.partition"), "Expected .osrm.partition file after partitioning.");
            Assert.True(File.Exists(basePath + ".osrm.cells"), "Expected .osrm.cells file after partitioning.");
        }
        finally
        {
            CleanupDirectory(outputDir);
        }
    }

    [Fact]
    public void CustomizeMonaco()
    {
        string? outputDir = null;
        try
        {
            outputDir = CreateTempOutputDirectory();
            var basePath = GetOsrmBasePath(outputDir);

            var extractConfig = new ExtractorConfig
            {
                InputPath = Path.Combine(RepoRoot, MonacoOsmPbf),
                ProfilePath = CarProfilePath,
                OutputPath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Extract(extractConfig);

            var partitionConfig = new PartitionerConfig
            {
                BasePath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Partition(partitionConfig);

            var customizerConfig = new CustomizerConfig
            {
                BasePath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Customize(customizerConfig);

            Assert.True(File.Exists(basePath + ".osrm.mldgr"), "Expected .osrm.mldgr file after customization.");
        }
        finally
        {
            CleanupDirectory(outputDir);
        }
    }

    [Fact]
    public async Task FullMldPipeline()
    {
        string? outputDir = null;
        try
        {
            outputDir = CreateTempOutputDirectory();
            var basePath = GetOsrmBasePath(outputDir);

            var extractConfig = new ExtractorConfig
            {
                InputPath = Path.Combine(RepoRoot, MonacoOsmPbf),
                ProfilePath = CarProfilePath,
                OutputPath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Extract(extractConfig);
            Assert.True(File.Exists(basePath + ".osrm.cnbg"), "Extract did not produce .osrm.cnbg file.");

            var partitionConfig = new PartitionerConfig
            {
                BasePath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Partition(partitionConfig);
            Assert.True(File.Exists(basePath + ".osrm.partition"), "Partition did not produce .osrm.partition.");

            var customizerConfig = new CustomizerConfig
            {
                BasePath = basePath,
                RequestedThreads = 1,
            };
            OsrmPipeline.Customize(customizerConfig);
            Assert.True(File.Exists(basePath + ".osrm.mldgr"), "Customize did not produce .osrm.mldgr.");

            var engineConfig = new EngineConfig
            {
                StoragePath = basePath,
                Algorithm = Algorithm.MLD,
            };
            await using var engine = OsrmEngine.Create(engineConfig);
            Assert.NotNull(engine);
            Assert.False(engine.IsDisposed);

            var routeParams = new RouteParameters
            {
                Coordinates =
                [
                    (7.4220, 43.7310),
                    (7.4197, 43.7330),
                ],
            };

            var response = engine.Route(routeParams);

            Assert.Equal("Ok", response.Code);
            Assert.NotNull(response.Routes);
            Assert.NotEmpty(response.Routes);
            Assert.True(response.Routes[0].Distance > 0, "Expected a positive route distance.");
            Assert.True(response.Routes[0].Duration > 0, "Expected a positive route duration.");
        }
        finally
        {
            CleanupDirectory(outputDir);
        }
    }
}

public class PipelineNegativeTests
{
    [Fact]
    public void ExtractInvalidPathThrows()
    {
        var config = new ExtractorConfig
        {
            InputPath = "/nonexistent/path/that/does/not/exist.osm.pbf",
            ProfilePath = "/nonexistent/car.lua",
            RequestedThreads = 1,
        };

        var ex = Assert.Throws<OsrmException>(() => OsrmPipeline.Extract(config));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void ExtractNullConfigThrows()
    {
        Assert.Throws<ArgumentNullException>(() => OsrmPipeline.Extract(null!));
    }
}
