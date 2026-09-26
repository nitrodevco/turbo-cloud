using System;
using System.Collections.Generic;

namespace Turbo.Rooms.Wired;

/// <summary>
/// A projectile's flight as the client is drawing it. The server moves furni at once and the
/// client animates from the old tile to the new one over the animation time it was sent, so
/// this is that same animation followed on the server: where the furni looks to be, how far it
/// has come, and what it has passed over.
///
/// The clock is the one thing here the client confirms, because the animation time is in the
/// movement packet. What counts as a collision is this server's reading: whoever stood on a
/// tile when the flight began is counted once the animation reaches that tile.
/// </summary>
internal sealed record WiredProjectileFlight
{
    /// <summary>The tiles the animation crosses, the first being where it started.</summary>
    public required IReadOnlyList<int> Path { get; init; }

    /// <summary>
    /// How many avatars, and how many floor items, stood on each tile of <see cref="Path"/> when
    /// the flight began — one count per tile, not the objects themselves. A collision is counted
    /// from what was there at the start, so something that walks in mid-flight is not hit.
    /// </summary>
    public required IReadOnlyList<int> AvatarCountsOnPath { get; init; }
    public required IReadOnlyList<int> ItemCountsOnPath { get; init; }

    public required int SourceX { get; init; }
    public required int SourceY { get; init; }
    public required int SourceZ { get; init; }
    public required int TargetX { get; init; }
    public required int TargetY { get; init; }
    public required int TargetZ { get; init; }

    public required long StartedAtMs { get; init; }
    public required int DurationMs { get; init; }

    /// <summary>How far along the animation is, from zero at the start to one once it is over.</summary>
    public double GetProgress(long nowMs) =>
        DurationMs <= 0 ? 1 : Math.Clamp((nowMs - StartedAtMs) / (double)DurationMs, 0, 1);

    public bool IsTravelling(long nowMs) => GetProgress(nowMs) < 1;

    /// <summary>Tiles behind it, counting the one it is on; the whole path once it has landed.</summary>
    public int GetTilesTravelled(long nowMs) =>
        (int)Math.Floor(GetProgress(nowMs) * Math.Max(0, Path.Count - 1));

    public int GetX(long nowMs) => Interpolate(SourceX, TargetX, nowMs);

    public int GetY(long nowMs) => Interpolate(SourceY, TargetY, nowMs);

    /// <summary>Hundredths of a tile, as altitudes are counted everywhere else.</summary>
    public int GetAltitude(long nowMs) => Interpolate(SourceZ, TargetZ, nowMs);

    public int GetAvatarCollisions(long nowMs) => CountUpToNow(AvatarCountsOnPath, nowMs);

    public int GetItemCollisions(long nowMs) => CountUpToNow(ItemCountsOnPath, nowMs);

    private int Interpolate(int from, int to, long nowMs) =>
        (int)Math.Round(from + ((to - from) * GetProgress(nowMs)));

    private int CountUpToNow(IReadOnlyList<int> perTile, long nowMs)
    {
        var reached = GetTilesTravelled(nowMs);
        var total = 0;

        // The tile it started on is not something it flew into.
        for (var tile = 1; tile <= reached && tile < perTile.Count; tile++)
            total += perTile[tile];

        return total;
    }

    /// <summary>The tiles a straight flight crosses, the longer axis one step at a time.</summary>
    public static List<int> BuildPath(
        Func<int, int, int> toIdx,
        int sourceX,
        int sourceY,
        int targetX,
        int targetY
    )
    {
        var (dx, dy) = (targetX - sourceX, targetY - sourceY);
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var path = new List<int>(steps + 1);

        for (var step = 0; step <= steps; step++)
        {
            var at = steps == 0 ? 0d : step / (double)steps;

            path.Add(
                toIdx((int)Math.Round(sourceX + (dx * at)), (int)Math.Round(sourceY + (dy * at)))
            );
        }

        return path;
    }
}
