using System.Runtime.InteropServices;
using Sharposrm.Pipeline;
using Sharposrm.Interop;
using Xunit;

namespace Sharposrm.Tests;

/// <summary>
/// Verifies that CustomizerConfig and ContractorConfig correctly marshal
/// the UpdaterConfig fields (tz_file_path, valid_now, log_edge_updates_factor)
/// through ToNative().
/// </summary>
public class PipelineConfigMarshallingTests
{
    // ── CustomizerConfig ────────────────────────────────────────────────

    [Fact]
    public void CustomizerConfig_TzFilePath_MarshalledCorrectly()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz_world.shp",
        };

        using var scope = config.ToNative();
        var native = scope.Config;

        Assert.NotEqual(IntPtr.Zero, native.tz_file_path);
        string? roundTripped = Marshal.PtrToStringAnsi(native.tz_file_path);
        Assert.Equal("/data/tz_world.shp", roundTripped);
    }

    [Fact]
    public void CustomizerConfig_TzFilePath_NullProducesZeroPtr()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = null,
        };

        using var scope = config.ToNative();
        Assert.Equal(IntPtr.Zero, scope.Config.tz_file_path);
    }

    [Fact]
    public void CustomizerConfig_ValidNow_MarshalledCorrectly()
    {
        long timestamp = 1712700000L; // 2024-04-10 approx

        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            ValidNow = timestamp,
        };

        using var scope = config.ToNative();
        Assert.Equal(timestamp, scope.Config.valid_now);
    }

    [Fact]
    public void CustomizerConfig_ValidNow_DefaultIsZero()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
        };

        using var scope = config.ToNative();
        Assert.Equal(0L, scope.Config.valid_now);
    }

    [Fact]
    public void CustomizerConfig_LogEdgeUpdatesFactor_MarshalledCorrectly()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            LogEdgeUpdatesFactor = 0.5,
        };

        using var scope = config.ToNative();
        Assert.Equal(0.5, scope.Config.log_edge_updates_factor);
    }

    [Fact]
    public void CustomizerConfig_LogEdgeUpdatesFactor_DefaultIsZero()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
        };

        using var scope = config.ToNative();
        Assert.Equal(0.0, scope.Config.log_edge_updates_factor);
    }

    [Fact]
    public void CustomizerConfig_AllUpdaterFields_MarshalledTogether()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz.shp",
            ValidNow = 1700000000L,
            LogEdgeUpdatesFactor = 1.25,
            SegmentSpeedFiles = new[] { "/data/speeds.csv" },
            TurnPenaltyFiles = new[] { "/data/penalties.csv" },
        };

        using var scope = config.ToNative();
        var n = scope.Config;

        Assert.Equal("/data/tz.shp", Marshal.PtrToStringAnsi(n.tz_file_path));
        Assert.Equal(1700000000L, n.valid_now);
        Assert.Equal(1.25, n.log_edge_updates_factor);
        Assert.Equal(1, n.segment_speed_lookup_count);
        Assert.Equal(1, n.turn_penalty_lookup_count);
    }

    // ── ContractorConfig ────────────────────────────────────────────────

    [Fact]
    public void ContractorConfig_TzFilePath_MarshalledCorrectly()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz_world.shp",
        };

        using var scope = config.ToNative();
        var native = scope.Config;

        Assert.NotEqual(IntPtr.Zero, native.tz_file_path);
        Assert.Equal("/data/tz_world.shp", Marshal.PtrToStringAnsi(native.tz_file_path));
    }

    [Fact]
    public void ContractorConfig_TzFilePath_NullProducesZeroPtr()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = null,
        };

        using var scope = config.ToNative();
        Assert.Equal(IntPtr.Zero, scope.Config.tz_file_path);
    }

    [Fact]
    public void ContractorConfig_ValidNow_MarshalledCorrectly()
    {
        long timestamp = 1712700000L;

        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            ValidNow = timestamp,
        };

        using var scope = config.ToNative();
        Assert.Equal(timestamp, scope.Config.valid_now);
    }

    [Fact]
    public void ContractorConfig_ValidNow_DefaultIsZero()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
        };

        using var scope = config.ToNative();
        Assert.Equal(0L, scope.Config.valid_now);
    }

    [Fact]
    public void ContractorConfig_LogEdgeUpdatesFactor_MarshalledCorrectly()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            LogEdgeUpdatesFactor = 0.75,
        };

        using var scope = config.ToNative();
        Assert.Equal(0.75, scope.Config.log_edge_updates_factor);
    }

    [Fact]
    public void ContractorConfig_AllUpdaterFields_MarshalledTogether()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz.shp",
            ValidNow = 1700000000L,
            LogEdgeUpdatesFactor = 2.0,
            SegmentSpeedFiles = new[] { "/data/speeds.csv" },
        };

        using var scope = config.ToNative();
        var n = scope.Config;

        Assert.Equal("/data/tz.shp", Marshal.PtrToStringAnsi(n.tz_file_path));
        Assert.Equal(1700000000L, n.valid_now);
        Assert.Equal(2.0, n.log_edge_updates_factor);
        Assert.Equal(1, n.segment_speed_lookup_count);
    }

    // ── Dispose safety ──────────────────────────────────────────────────

    [Fact]
    public void CustomizerConfig_DisposeFreesAllAllocations()
    {
        var config = new CustomizerConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz.shp",
            SegmentSpeedFiles = new[] { "/data/a.csv", "/data/b.csv" },
        };

        var scope = config.ToNative();
        scope.Dispose();
        scope.Dispose(); // double dispose must be safe
    }

    [Fact]
    public void ContractorConfig_DisposeFreesAllAllocations()
    {
        var config = new ContractorConfig
        {
            BasePath = "/data/monaco.osrm",
            RequestedThreads = 1,
            TimeZoneFilePath = "/data/tz.shp",
            TurnPenaltyFiles = new[] { "/data/p.csv" },
        };

        var scope = config.ToNative();
        scope.Dispose();
        scope.Dispose(); // double dispose must be safe
    }
}
