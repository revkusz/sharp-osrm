using Sharposrm.Interop;

namespace Sharposrm.Tile;

/// <summary>
/// Managed parameters for the OSRM Tile (MVT) service.
/// Identifies a single map tile by its x/y/z coordinates.
/// No unmanaged memory allocations are needed — just 3 uint values.
/// </summary>
public sealed class TileParameters
{
    /// <summary>
    /// Tile X coordinate (column index).
    /// Must be in range [0, 2^<see cref="Z"/>).
    /// </summary>
    public uint X { get; init; }

    /// <summary>
    /// Tile Y coordinate (row index).
    /// Must be in range [0, 2^<see cref="Z"/>).
    /// </summary>
    public uint Y { get; init; }

    /// <summary>
    /// Zoom level. Must be in range [12, 19] (OSRM supports tiles at these levels).
    /// </summary>
    public uint Z { get; init; }

    /// <summary>
    /// Converts these managed parameters to a blittable native struct for interop.
    /// No allocations needed — the struct is copied directly.
    /// </summary>
    /// <returns>A <see cref="NativeTileParams"/> with the x/y/z values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="Z"/> is outside [12, 19], or when
    /// <see cref="X"/>/<see cref="Y"/> are outside [0, 2^Z).
    /// </exception>
    internal NativeTileParams ToNative()
    {
        if (Z < 12 || Z > 19)
        {
            throw new ArgumentException(
                $"Zoom level must be between 12 and 19 inclusive, got {Z}.",
                nameof(Z));
        }

        uint maxTile = (1u << (int)Z) - 1u;
        if (X > maxTile)
        {
            throw new ArgumentException(
                $"X ({X}) must be in range [0, {maxTile}] for zoom level {Z}.",
                nameof(X));
        }

        if (Y > maxTile)
        {
            throw new ArgumentException(
                $"Y ({Y}) must be in range [0, {maxTile}] for zoom level {Z}.",
                nameof(Y));
        }

        return new NativeTileParams
        {
            x = X,
            y = Y,
            z = Z,
        };
    }
}
