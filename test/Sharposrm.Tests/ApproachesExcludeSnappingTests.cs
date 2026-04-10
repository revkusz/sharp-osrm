using System.Runtime.InteropServices;
using Sharposrm.Interop;
using Sharposrm.Match;
using Sharposrm.Nearest;
using Sharposrm.Route;
using Sharposrm.Table;
using Sharposrm.Trip;
using Xunit;

namespace Sharposrm.Tests;

/// <summary>
/// Pure marshalling tests for the approaches, exclude, and snapping parameter fields.
/// Exercises ToNative() on all 5 service parameter classes and reads back from
/// unmanaged memory to verify the ABI boundary — no OSRM engine required.
/// </summary>
public class ApproachesExcludeSnappingTests
{
    // ── Helper coordinate sets ────────────────────────────────────────────

    private static readonly (double Longitude, double Latitude)[] TwoCoords =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
    };

    private static readonly (double Longitude, double Latitude)[] ThreeCoords =
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
        (7.42250, 43.73200),
    };

    private static readonly (double Longitude, double Latitude)[] OneCoord =
    {
        (7.41337, 43.72956),
    };

    // ── Shared assertion helpers ─────────────────────────────────────────

    private static void AssertApproachesBytes(IntPtr approachesPtr, int approachCount, params byte[] expected)
    {
        Assert.NotEqual(IntPtr.Zero, approachesPtr);
        Assert.Equal(expected.Length, approachCount);
        for (int i = 0; i < expected.Length; i++)
        {
            byte actual = Marshal.ReadByte(approachesPtr, i);
            Assert.Equal(expected[i], actual);
        }
    }

    private static void AssertExcludeStrings(IntPtr excludePtr, int excludeCount, params string[] expected)
    {
        Assert.NotEqual(IntPtr.Zero, excludePtr);
        Assert.Equal(expected.Length, excludeCount);
        for (int i = 0; i < expected.Length; i++)
        {
            IntPtr stringPtr = Marshal.ReadIntPtr(excludePtr, i * IntPtr.Size);
            Assert.NotEqual(IntPtr.Zero, stringPtr);
            string? actual = Marshal.PtrToStringAnsi(stringPtr);
            Assert.Equal(expected[i], actual);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Route service tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Route_ToNative_WithApproaches_SetsByteArray()
    {
        var params_ = new RouteParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Curb, null, ApproachType.Opposite],
        };

        using var scope = params_.ToNative();
        AssertApproachesBytes(scope.Params.approaches, scope.Params.approach_count, (byte)0, (byte)0xFF, (byte)2);
    }

    [Fact]
    public void Route_ToNative_WithExclude_SetsStringArray()
    {
        var params_ = new RouteParameters
        {
            Coordinates = TwoCoords,
            Exclude = ["motorway", "trunk"],
        };

        using var scope = params_.ToNative();
        AssertExcludeStrings(scope.Params.exclude, scope.Params.exclude_count, "motorway", "trunk");
    }

    [Fact]
    public void Route_ToNative_WithSnapping_SetsInt()
    {
        var params_ = new RouteParameters
        {
            Coordinates = TwoCoords,
            Snapping = SnappingType.Any,
        };

        using var scope = params_.ToNative();
        Assert.Equal(1, scope.Params.snapping);
    }

    [Fact]
    public void Route_ToNative_WithoutApproachesExcludeSnapping_DefaultsCorrectly()
    {
        var params_ = new RouteParameters
        {
            Coordinates = TwoCoords,
        };

        using var scope = params_.ToNative();
        Assert.Equal(0, scope.Params.approach_count);
        Assert.Equal(IntPtr.Zero, scope.Params.approaches);
        Assert.Equal(0, scope.Params.exclude_count);
        Assert.Equal(IntPtr.Zero, scope.Params.exclude);
        Assert.Equal(0, scope.Params.snapping);
    }

    [Fact]
    public void Route_ToNative_DisposeFreesApproachesAndExclude()
    {
        var params_ = new RouteParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Curb, ApproachType.Unrestricted, ApproachType.Opposite],
            Exclude = ["motorway"],
            Snapping = SnappingType.Any,
        };

        var scope = params_.ToNative();
        // Verify fields are set before dispose
        Assert.True(scope.Params.approach_count > 0);
        Assert.True(scope.Params.exclude_count > 0);

        // Dispose must not throw
        scope.Dispose();

        // Double-dispose must not throw (idempotent)
        scope.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Table service tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Table_ToNative_WithApproaches_SetsByteArray()
    {
        var params_ = new TableParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Unrestricted, null, ApproachType.Curb],
        };

        using var scope = params_.ToNative();
        AssertApproachesBytes(scope.Params.approaches, scope.Params.approach_count, (byte)1, (byte)0xFF, (byte)0);
    }

    [Fact]
    public void Table_ToNative_WithExclude_SetsStringArray()
    {
        var params_ = new TableParameters
        {
            Coordinates = TwoCoords,
            Exclude = ["ferry", "motorway"],
        };

        using var scope = params_.ToNative();
        AssertExcludeStrings(scope.Params.exclude, scope.Params.exclude_count, "ferry", "motorway");
    }

    [Fact]
    public void Table_ToNative_WithSnapping_SetsInt()
    {
        var params_ = new TableParameters
        {
            Coordinates = TwoCoords,
            Snapping = SnappingType.Any,
        };

        using var scope = params_.ToNative();
        Assert.Equal(1, scope.Params.snapping);
    }

    [Fact]
    public void Table_ToNative_WithoutApproachesExcludeSnapping_DefaultsCorrectly()
    {
        var params_ = new TableParameters
        {
            Coordinates = TwoCoords,
        };

        using var scope = params_.ToNative();
        Assert.Equal(0, scope.Params.approach_count);
        Assert.Equal(IntPtr.Zero, scope.Params.approaches);
        Assert.Equal(0, scope.Params.exclude_count);
        Assert.Equal(IntPtr.Zero, scope.Params.exclude);
        Assert.Equal(0, scope.Params.snapping);
    }

    [Fact]
    public void Table_ToNative_DisposeFreesApproachesAndExclude()
    {
        var params_ = new TableParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Opposite, null, ApproachType.Curb],
            Exclude = ["trunk"],
            Snapping = SnappingType.Any,
        };

        var scope = params_.ToNative();
        Assert.True(scope.Params.approach_count > 0);
        Assert.True(scope.Params.exclude_count > 0);
        scope.Dispose();
        scope.Dispose(); // idempotent
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Nearest service tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nearest_ToNative_WithApproaches_SetsByteArray()
    {
        var params_ = new NearestParameters
        {
            Coordinates = OneCoord,
            Approaches = [ApproachType.Curb],
        };

        using var scope = params_.ToNative();
        AssertApproachesBytes(scope.Params.approaches, scope.Params.approach_count, (byte)0);
    }

    [Fact]
    public void Nearest_ToNative_WithExclude_SetsStringArray()
    {
        var params_ = new NearestParameters
        {
            Coordinates = OneCoord,
            Exclude = ["tunnel"],
        };

        using var scope = params_.ToNative();
        AssertExcludeStrings(scope.Params.exclude, scope.Params.exclude_count, "tunnel");
    }

    [Fact]
    public void Nearest_ToNative_WithSnapping_SetsInt()
    {
        var params_ = new NearestParameters
        {
            Coordinates = OneCoord,
            Snapping = SnappingType.Any,
        };

        using var scope = params_.ToNative();
        Assert.Equal(1, scope.Params.snapping);
    }

    [Fact]
    public void Nearest_ToNative_WithoutApproachesExcludeSnapping_DefaultsCorrectly()
    {
        var params_ = new NearestParameters
        {
            Coordinates = OneCoord,
        };

        using var scope = params_.ToNative();
        Assert.Equal(0, scope.Params.approach_count);
        Assert.Equal(IntPtr.Zero, scope.Params.approaches);
        Assert.Equal(0, scope.Params.exclude_count);
        Assert.Equal(IntPtr.Zero, scope.Params.exclude);
        Assert.Equal(0, scope.Params.snapping);
    }

    [Fact]
    public void Nearest_ToNative_DisposeFreesApproachesAndExclude()
    {
        var params_ = new NearestParameters
        {
            Coordinates = OneCoord,
            Approaches = [ApproachType.Unrestricted],
            Exclude = ["motorway"],
            Snapping = SnappingType.Any,
        };

        var scope = params_.ToNative();
        Assert.True(scope.Params.approach_count > 0);
        Assert.True(scope.Params.exclude_count > 0);
        scope.Dispose();
        scope.Dispose(); // idempotent
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Trip service tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Trip_ToNative_WithApproaches_SetsByteArray()
    {
        var params_ = new TripParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [null, ApproachType.Opposite, ApproachType.Curb],
        };

        using var scope = params_.ToNative();
        AssertApproachesBytes(scope.Params.approaches, scope.Params.approach_count, (byte)0xFF, (byte)2, (byte)0);
    }

    [Fact]
    public void Trip_ToNative_WithExclude_SetsStringArray()
    {
        var params_ = new TripParameters
        {
            Coordinates = TwoCoords,
            Exclude = ["motorway", "ferry", "trunk"],
        };

        using var scope = params_.ToNative();
        AssertExcludeStrings(scope.Params.exclude, scope.Params.exclude_count, "motorway", "ferry", "trunk");
    }

    [Fact]
    public void Trip_ToNative_WithSnapping_SetsInt()
    {
        var params_ = new TripParameters
        {
            Coordinates = TwoCoords,
            Snapping = SnappingType.Any,
        };

        using var scope = params_.ToNative();
        Assert.Equal(1, scope.Params.snapping);
    }

    [Fact]
    public void Trip_ToNative_WithoutApproachesExcludeSnapping_DefaultsCorrectly()
    {
        var params_ = new TripParameters
        {
            Coordinates = TwoCoords,
        };

        using var scope = params_.ToNative();
        Assert.Equal(0, scope.Params.approach_count);
        Assert.Equal(IntPtr.Zero, scope.Params.approaches);
        Assert.Equal(0, scope.Params.exclude_count);
        Assert.Equal(IntPtr.Zero, scope.Params.exclude);
        Assert.Equal(0, scope.Params.snapping);
    }

    [Fact]
    public void Trip_ToNative_DisposeFreesApproachesAndExclude()
    {
        var params_ = new TripParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Curb, ApproachType.Unrestricted, ApproachType.Opposite],
            Exclude = ["motorway"],
            Snapping = SnappingType.Any,
        };

        var scope = params_.ToNative();
        Assert.True(scope.Params.approach_count > 0);
        Assert.True(scope.Params.exclude_count > 0);
        scope.Dispose();
        scope.Dispose(); // idempotent
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Match service tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Match_ToNative_WithApproaches_SetsByteArray()
    {
        var params_ = new MatchParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Unrestricted, null, ApproachType.Opposite],
        };

        using var scope = params_.ToNative();
        AssertApproachesBytes(scope.Params.approaches, scope.Params.approach_count, (byte)1, (byte)0xFF, (byte)2);
    }

    [Fact]
    public void Match_ToNative_WithExclude_SetsStringArray()
    {
        var params_ = new MatchParameters
        {
            Coordinates = TwoCoords,
            Exclude = ["motorway", "trunk"],
        };

        using var scope = params_.ToNative();
        AssertExcludeStrings(scope.Params.exclude, scope.Params.exclude_count, "motorway", "trunk");
    }

    [Fact]
    public void Match_ToNative_WithSnapping_SetsInt()
    {
        var params_ = new MatchParameters
        {
            Coordinates = TwoCoords,
            Snapping = SnappingType.Any,
        };

        using var scope = params_.ToNative();
        Assert.Equal(1, scope.Params.snapping);
    }

    [Fact]
    public void Match_ToNative_WithoutApproachesExcludeSnapping_DefaultsCorrectly()
    {
        var params_ = new MatchParameters
        {
            Coordinates = TwoCoords,
        };

        using var scope = params_.ToNative();
        Assert.Equal(0, scope.Params.approach_count);
        Assert.Equal(IntPtr.Zero, scope.Params.approaches);
        Assert.Equal(0, scope.Params.exclude_count);
        Assert.Equal(IntPtr.Zero, scope.Params.exclude);
        Assert.Equal(0, scope.Params.snapping);
    }

    [Fact]
    public void Match_ToNative_DisposeFreesApproachesAndExclude()
    {
        var params_ = new MatchParameters
        {
            Coordinates = ThreeCoords,
            Approaches = [ApproachType.Curb, ApproachType.Unrestricted, ApproachType.Opposite],
            Exclude = ["ferry"],
            Snapping = SnappingType.Any,
        };

        var scope = params_.ToNative();
        Assert.True(scope.Params.approach_count > 0);
        Assert.True(scope.Params.exclude_count > 0);
        scope.Dispose();
        scope.Dispose(); // idempotent
    }
}
